using Index5.Application.DTOs;
using Index5.Domain.Entities;
using Index5.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Index5.Application.Services;

public class PurchaseEngineService
{
    private readonly IClientRepository _clientRepo;
    private readonly IBasketRepository _basketRepo;
    private readonly ICustodyRepository _custodyRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IKafkaProducer _kafkaProducer;
    private readonly ICotahistParser _cotahistParser;
    private readonly IConfiguration _configuration;

    public PurchaseEngineService(
        IClientRepository clientRepo,
        IBasketRepository basketRepo,
        ICustodyRepository custodyRepo,
        IUnitOfWork unitOfWork,
        IKafkaProducer kafkaProducer,
        ICotahistParser cotahistParser,
        IConfiguration configuration)
    {
        _clientRepo = clientRepo;
        _basketRepo = basketRepo;
        _custodyRepo = custodyRepo;
        _unitOfWork = unitOfWork;
        _kafkaProducer = kafkaProducer;
        _cotahistParser = cotahistParser;
        _configuration = configuration;
    }

    /// <summary>
    /// Versão para execução automática (usada pelo BackgroundService)
    /// </summary>
    public async Task<ExecutePurchaseResponse> ExecutePurchaseAsync()
    {
        var folder = _configuration["Cotacoes:Folder"] ?? "";
        return await ExecutePurchaseAsync(DateTime.UtcNow.ToString("yyyy-MM-dd"), (ticker) =>
        {
            var q = _cotahistParser.GetClosingQuote(folder, ticker);
            return q?.PrecoFechamento ?? 0;
        });
    }

    public async Task<ExecutePurchaseResponse> ExecutePurchaseAsync(
        string referenceDate,
        Func<string, decimal> getQuote)
    {
        var basket = await _basketRepo.GetActiveAsync();
        if (basket == null)
            throw new InvalidOperationException("BASKET_NOT_FOUND");

        var clients = await _clientRepo.GetAllActiveAsync();
        if (clients.Count == 0)
            throw new InvalidOperationException("NO_ACTIVE_CLIENTS");

        var clientContributions = clients
            .Select(c => new { Client = c, Contribution = Math.Round(c.MonthlyValue / 3, 2) })
            .ToList();

        var totalConsolidated = clientContributions.Sum(x => x.Contribution);

        var purchaseOrders = new List<PurchaseOrderDto>();
        var quantitiesPerTicker = new Dictionary<string, int>();
        var usedFromMasterMap = new Dictionary<string, int>();
        var executionId = Guid.NewGuid().ToString("N"); // Identificador único desta execução

        // 1. Planejamento de Compra e Abatimento de Master
        foreach (var item in basket.Items)
        {
            var valueForAsset = totalConsolidated * (item.Percentage / 100m);
            var quote = getQuote(item.Ticker);
            if (quote <= 0) continue;

            // Quantidade necessária total (o que os clientes 'comprariam' com o dinheiro novo)
            var calculatedQuantity = (int)Math.Truncate(valueForAsset / quote);

            // RN-029: Verificar saldo na custodia master
            var masterCustody = await _custodyRepo.GetMasterByTickerAsync(item.Ticker);
            var masterBalance = masterCustody?.Quantity ?? 0;

            // RN-030: Se houver saldo master, descontar da quantidade a comprar
            var quantityToBuy = Math.Max(0, calculatedQuantity - masterBalance);
            
            // RN-037: Quantidade total disponível = compradas + saldo master anterior
            // Aqui garantimos que o que será distribuído é exatamente o que calculamos como necessidade,
            // ou o que temos em master se master for maior que a necessidade.
            var usedFromMaster = Math.Min(masterBalance, calculatedQuantity);
            var availableQuantity = quantityToBuy + usedFromMaster;

            quantitiesPerTicker[item.Ticker] = availableQuantity;
            usedFromMasterMap[item.Ticker] = usedFromMaster;

            if (quantityToBuy > 0)
            {
                var details = CalculateLotDetails(item.Ticker, quantityToBuy);

                purchaseOrders.Add(new PurchaseOrderDto
                {
                    Ticker = item.Ticker,
                    TotalQuantity = quantityToBuy,
                    UsedFromMaster = usedFromMaster,
                    Details = details,
                    UnitPrice = quote,
                    TotalValue = quantityToBuy * quote
                });

                for (int i = 0; i < details.Count; i++)
                {
                    var det = details[i];
                    await _custodyRepo.AddPurchaseOrderAsync(new PurchaseOrder
                    {
                        Ticker = det.Ticker,
                        Quantity = det.Quantity,
                        UnitPrice = quote,
                        TotalValue = det.Quantity * quote,
                        ReferenceDate = referenceDate,
                        ExecutionId = executionId,
                        UsedFromMaster = (i == 0) ? usedFromMaster : 0,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
            else if (usedFromMaster > 0)
            {
                // Registro informativo de uso exclusivo da Master
                await _custodyRepo.AddPurchaseOrderAsync(new PurchaseOrder
                {
                    Ticker = item.Ticker,
                    Quantity = 0,
                    UnitPrice = quote,
                    TotalValue = 0,
                    ReferenceDate = referenceDate,
                    ExecutionId = executionId,
                    UsedFromMaster = usedFromMaster,
                    CreatedAt = DateTime.UtcNow
                });

                purchaseOrders.Add(new PurchaseOrderDto
                {
                    Ticker = item.Ticker,
                    TotalQuantity = 0,
                    UsedFromMaster = usedFromMaster,
                    UnitPrice = quote,
                    TotalValue = 0
                });
            }

            // Atualiza Master (Consome o que foi usado do estoque antigo)
            if (usedFromMaster > 0 && masterCustody != null)
            {
                masterCustody.Quantity -= usedFromMaster;
                _custodyRepo.UpdateMaster(masterCustody);
            }
        }

        var distributions = new List<ClientDistributionDto>();
        var finalResidues = new Dictionary<string, int>();
        int irEvents = 0;

        foreach (var ticker in quantitiesPerTicker.Keys)
        {
            var totalAvailable = quantitiesPerTicker[ticker];
            if (totalAvailable <= 0) continue;

            var quote = getQuote(ticker);
            var residueForTicker = totalAvailable;

            foreach (var vc in clientContributions)
            {
                var client = vc.Client;
                var proportion = (decimal)vc.Contribution / totalConsolidated;
                
                // RN-036: Quantidade por cliente = TRUNCAR(Proporcao x Quantidade Total Disponivel)
                var clientQty = (int)Math.Truncate(totalAvailable * proportion);

                if (clientQty <= 0) continue;

                residueForTicker -= clientQty;

                // Localiza ou cria o DTO de distribuição do cliente
                var distDto = distributions.FirstOrDefault(d => d.ClientId == client.Id);
                if (distDto == null)
                {
                    distDto = new ClientDistributionDto
                    {
                        ClientId = client.Id,
                        Name = client.Name,
                        ContributionValue = Math.Round(client.MonthlyValue / 3, 2),
                        Assets = new List<DistributedAssetDto>()
                    };
                    distributions.Add(distDto);
                }

                distDto.Assets.Add(new DistributedAssetDto
                {
                    Ticker = ticker,
                    Quantity = clientQty
                });

                var graphicAccountId = client.GraphicAccount?.Id ?? 0;
                var custody = await _custodyRepo.GetByAccountAndTickerAsync(graphicAccountId, ticker);

                if (custody != null)
                {
                    var prevAvgPrice = custody.AveragePrice;
                    var prevQty = custody.Quantity;
                    custody.AveragePrice = (prevQty * prevAvgPrice + clientQty * quote) / (prevQty + clientQty);
                    custody.Quantity += clientQty;
                    _custodyRepo.Update(custody);
                }
                else
                {
                    await _custodyRepo.AddAsync(new ChildCustody
                    {
                        GraphicAccountId = graphicAccountId,
                        Ticker = ticker,
                        Quantity = clientQty,
                        AveragePrice = quote
                    });
                }

                await _custodyRepo.AddHistoryAsync(new OperationHistory
                {
                    ClientId = client.Id,
                    Ticker = ticker,
                    OperationType = "BUY",
                    Quantity = clientQty,
                    UnitPrice = quote,
                    TotalValue = clientQty * quote,
                    OperationDate = DateTime.UtcNow,
                    Reason = "COMPRA_PROGRAMADA"
                });

                // IR Dedo-duro
                var operationValue = clientQty * quote;
                var irValue = Math.Round(operationValue * 0.00005m, 2);
                try
                {
                    await _kafkaProducer.PublishAsync("ir-dedo-duro", client.Cpf, new
                    {
                        clientId = client.Id,
                        cpf = client.Cpf,
                        ticker = ticker,
                        operationValue = operationValue,
                        irValue = irValue,
                        date = DateTime.UtcNow
                    });
                    irEvents++;
                }
                catch { }
            }

            // RN-039: Ações não distribuídas permanecem na custodia master
            if (residueForTicker > 0)
            {
                finalResidues[ticker] = residueForTicker;
            }
        }

        // 3. Persistência de Resíduos e Retorno
        var residuesResponse = new List<MasterResidueDto>();
        foreach (var (ticker, residue) in finalResidues)
        {
            var masterCustody = await _custodyRepo.GetMasterByTickerAsync(ticker);
            var quote = getQuote(ticker);

            if (masterCustody != null)
            {
                var prevAvgPrice = masterCustody.AveragePrice;
                var prevQty = masterCustody.Quantity;
                masterCustody.AveragePrice = prevQty + residue > 0
                    ? (prevQty * prevAvgPrice + residue * quote) / (prevQty + residue)
                    : quote;
                masterCustody.Quantity += residue;
                _custodyRepo.UpdateMaster(masterCustody);
            }
            else
            {
                await _custodyRepo.AddMasterAsync(new MasterCustody
                {
                    Ticker = ticker,
                    Quantity = residue,
                    AveragePrice = quote,
                    Origin = $"Distribution residue {referenceDate}"
                });
            }

            residuesResponse.Add(new MasterResidueDto { Ticker = ticker, Quantity = residue });
        }

        await _unitOfWork.SaveChangesAsync();

        return new ExecutePurchaseResponse
        {
            ExecutionDate = DateTime.UtcNow,
            TotalClients = clients.Count,
            TotalConsolidated = totalConsolidated,
            PurchaseOrders = purchaseOrders,
            Distributions = distributions,
            MasterCustodyResidues = residuesResponse,
            IREventsPublished = irEvents,
            Message = $"Scheduled purchase executed successfully for {clients.Count} clients. Master deducted and residues minimized."
        };
    }

    public EngineStatusDto GetStatus()
    {
        var now = DateTime.UtcNow;
        var nextExecution = GetNextExecutionDate(now);
        
        // Simulação simples de progresso baseada no próximo ciclo
        var scheduledDates = new[] { 5, 15, 25 };
        var prevExecution = scheduledDates
            .Select(d => GetExecutionDate(now.Year, now.Month, d))
            .Where(d => d.Date < now.Date)
            .OrderByDescending(d => d)
            .FirstOrDefault();

        if (prevExecution == default)
        {
            // Pega o dia 25 do mês passado
            var prevMonth = now.AddMonths(-1);
            prevExecution = GetExecutionDate(prevMonth.Year, prevMonth.Month, 25);
        }

        var totalDays = (nextExecution - prevExecution).TotalDays;
        var elapsedDays = (now - prevExecution).TotalDays;
        
        var progress = (decimal)(elapsedDays / totalDays) * 100;
        if (progress < 0) progress = 0;
        if (progress > 100) progress = 100;

        return new EngineStatusDto
        {
            NextPurchaseDate = nextExecution,
            ProgressPercentage = Math.Round(progress, 2)
        };
    }

    public bool IsDueToday(DateTime date)
    {
        var scheduledDays = new[] { 5, 15, 25 };
        foreach (var day in scheduledDays)
        {
            var executionDate = GetExecutionDate(date.Year, date.Month, day);
            if (executionDate.Date == date.Date)
            {
                return true;
            }
        }
        return false;
    }

    public DateTime GetNextExecutionDate(DateTime fromDate)
    {
        var scheduledDays = new[] { 5, 15, 25 };
        
        // Tenta encontrar o próximo no mês atual
        foreach (var day in scheduledDays)
        {
            var executionDate = GetExecutionDate(fromDate.Year, fromDate.Month, day);
            if (executionDate.Date > fromDate.Date)
            {
                return executionDate;
            }
        }

        // Se não houver mais no mês atual, pega o dia 5 do próximo mês
        var nextMonth = fromDate.AddMonths(1);
        return GetExecutionDate(nextMonth.Year, nextMonth.Month, 5);
    }

    private DateTime GetExecutionDate(int year, int month, int day)
    {
        // Usamos meio-dia (12:00) em vez de meia-noite (00:00) 
        // para evitar que fusos horários negativos (como o do Brasil -3) 
        // façam a data retroceder um dia no frontend.
        var date = new DateTime(year, month, day, 12, 0, 0, DateTimeKind.Utc);
        
        // RN-021: Se cair no fim de semana, move para a próxima segunda
        if (date.DayOfWeek == DayOfWeek.Saturday)
        {
            date = date.AddDays(2);
        }
        else if (date.DayOfWeek == DayOfWeek.Sunday)
        {
            date = date.AddDays(1);
        }
        
        return date;
    }

    private List<OrderDetailDto> CalculateLotDetails(string ticker, int quantity)
    {
        var details = new List<OrderDetailDto>();

        var standardLots = quantity / 100;
        var fractional = quantity % 100;

        if (standardLots > 0)
        {
            details.Add(new OrderDetailDto
            {
                Type = "STANDARD_LOT",
                Ticker = ticker,
                Quantity = standardLots * 100
            });
        }

        if (fractional > 0)
        {
            details.Add(new OrderDetailDto
            {
                Type = "FRACTIONAL",
                Ticker = ticker + "F",
                Quantity = fractional
            });
        }

        return details;
    }
}
