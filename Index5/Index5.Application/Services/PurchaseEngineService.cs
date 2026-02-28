using Index5.Application.DTOs;
using Index5.Domain.Entities;
using Index5.Domain.Interfaces;

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
        var folder = _configuration.GetValue<string>("Cotacoes:Folder") ?? "";
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

        foreach (var item in basket.Items)
        {
            var valueForAsset = totalConsolidated * (item.Percentage / 100m);
            var quote = getQuote(item.Ticker);
            if (quote <= 0) continue;

            var calculatedQuantity = (int)Math.Truncate(valueForAsset / quote);

            var masterCustody = await _custodyRepo.GetMasterByTickerAsync(item.Ticker);
            var masterBalance = masterCustody?.Quantity ?? 0;

            var quantityToBuy = Math.Max(0, calculatedQuantity - masterBalance);
            var availableQuantity = calculatedQuantity;

            quantitiesPerTicker[item.Ticker] = availableQuantity;

            if (quantityToBuy > 0)
            {
                var details = CalculateLotDetails(item.Ticker, quantityToBuy);

                purchaseOrders.Add(new PurchaseOrderDto
                {
                    Ticker = item.Ticker,
                    TotalQuantity = quantityToBuy,
                    Details = details,
                    UnitPrice = quote,
                    TotalValue = quantityToBuy * quote
                });
            }

            if (masterBalance > 0 && masterCustody != null)
            {
                var usedFromMaster = Math.Min(masterBalance, calculatedQuantity);
                masterCustody.Quantity -= usedFromMaster;
                _custodyRepo.UpdateMaster(masterCustody);
            }
        }

        var distributions = new List<ClientDistributionDto>();
        var residuesMap = new Dictionary<string, int>();
        int irEvents = 0;

        foreach (var ticker in quantitiesPerTicker.Keys)
        {
            residuesMap[ticker] = quantitiesPerTicker[ticker];
        }

        foreach (var vc in clientContributions)
        {
            var client = vc.Client;
            var proportion = vc.Contribution / totalConsolidated;
            var distributedAssets = new List<DistributedAssetDto>();

            foreach (var item in basket.Items)
            {
                if (!quantitiesPerTicker.ContainsKey(item.Ticker)) continue;

                var totalAvailable = quantitiesPerTicker[item.Ticker];
                var clientQty = (int)Math.Truncate(totalAvailable * proportion);

                if (clientQty <= 0) continue;

                residuesMap[item.Ticker] -= clientQty;

                distributedAssets.Add(new DistributedAssetDto
                {
                    Ticker = item.Ticker,
                    Quantity = clientQty
                });

                var graphicAccountId = client.GraphicAccount?.Id ?? 0;
                var custody = await _custodyRepo.GetByAccountAndTickerAsync(graphicAccountId, item.Ticker);
                var quote = getQuote(item.Ticker);

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
                        Ticker = item.Ticker,
                        Quantity = clientQty,
                        AveragePrice = quote
                    });
                }

                await _custodyRepo.AddHistoryAsync(new OperationHistory
                {
                    ClientId = client.Id,
                    Ticker = item.Ticker,
                    OperationType = "BUY",
                    Quantity = clientQty,
                    UnitPrice = quote,
                    TotalValue = clientQty * quote,
                    OperationDate = DateTime.UtcNow,
                    Reason = "COMPRA_PROGRAMADA"
                });

                var operationValue = clientQty * quote;
                var irValue = Math.Round(operationValue * 0.00005m, 2);

                try
                {
                    await _kafkaProducer.PublishAsync("ir-dedo-duro", client.Cpf, new
                    {
                        clientId = client.Id,
                        cpf = client.Cpf,
                        ticker = item.Ticker,
                        operationValue = operationValue,
                        irValue = irValue,
                        date = DateTime.UtcNow
                    });
                    irEvents++;
                }
                catch { }
            }

            distributions.Add(new ClientDistributionDto
            {
                ClientId = client.Id,
                Name = client.Name,
                ContributionValue = vc.Contribution,
                Assets = distributedAssets
            });
        }

        var residuesResponse = new List<MasterResidueDto>();
        foreach (var (ticker, residue) in residuesMap)
        {
            if (residue <= 0) continue;

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
                masterCustody.Origin = $"Distribution residue {referenceDate}";
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

            residuesResponse.Add(new MasterResidueDto
            {
                Ticker = ticker,
                Quantity = residue
            });
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
            Message = $"Scheduled purchase executed successfully for {clients.Count} clients."
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
        var date = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);
        
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
