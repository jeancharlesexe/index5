using Index5.Domain.Entities;
using Index5.Domain.Interfaces;

namespace Index5.Application.Services;

public class RebalancingService
{
    private readonly IClientRepository _clientRepo;
    private readonly ICustodyRepository _custodyRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IKafkaProducer _kafkaProducer;

    public RebalancingService(
        IClientRepository clientRepo,
        ICustodyRepository custodyRepo,
        IUnitOfWork unitOfWork,
        IKafkaProducer kafkaProducer)
    {
        _clientRepo = clientRepo;
        _custodyRepo = custodyRepo;
        _unitOfWork = unitOfWork;
        _kafkaProducer = kafkaProducer;
    }

    public async Task<RebalancingSummary> RebalanceAllClientsAsync(
        RecommendationBasket newBasket, 
        RecommendationBasket? oldBasket,
        Func<string, decimal> getQuote)
    {
        var clients = await _clientRepo.GetAllActiveAsync();
        int affectedClients = 0;

        foreach (var client in clients)
        {
            await RebalanceClientAsync(client, newBasket, getQuote);
            affectedClients++;
        }

        await _unitOfWork.SaveChangesAsync();

        var removedAssets = oldBasket?.Items.Select(i => i.Ticker)
            .Except(newBasket.Items.Select(i => i.Ticker)).ToList() ?? new List<string>();
            
        var addedAssets = newBasket.Items.Select(i => i.Ticker)
            .Except(oldBasket?.Items.Select(i => i.Ticker) ?? new List<string>()).ToList();

        return new RebalancingSummary
        {
            ClientsAffected = affectedClients,
            RemovedAssets = removedAssets,
            AddedAssets = addedAssets
        };
    }

    private async Task RebalanceClientAsync(Client client, RecommendationBasket newBasket, Func<string, decimal> getQuote)
    {
        if (client.GraphicAccount == null) return;

        var currentCustody = await _custodyRepo.GetByGraphicAccountIdAsync(client.GraphicAccount.Id);
        
        decimal totalPortfolioValue = 0;
        var currentPrices = new Dictionary<string, decimal>();

        foreach (var item in currentCustody)
        {
            var price = getQuote(item.Ticker);
            if (price <= 0) price = item.AveragePrice; 
            currentPrices[item.Ticker] = price;
            totalPortfolioValue += item.Quantity * price;
        }

        if (totalPortfolioValue <= 0) return;

        var allTickers = currentCustody.Select(c => c.Ticker)
            .Union(newBasket.Items.Select(i => i.Ticker))
            .Distinct();

        decimal totalSalesThisMonth = 0;
        var saleDetails = new List<dynamic>();

        foreach (var ticker in allTickers)
        {
            var basketItem = newBasket.Items.FirstOrDefault(i => i.Ticker == ticker);
            var custodyItem = currentCustody.FirstOrDefault(c => c.Ticker == ticker);

            decimal targetPercentage = basketItem?.Percentage ?? 0;
            decimal currentPrice = currentPrices.ContainsKey(ticker) ? currentPrices[ticker] : getQuote(ticker);
            
            if (currentPrice <= 0) continue;

            int currentQty = custodyItem?.Quantity ?? 0;
            decimal targetValue = totalPortfolioValue * (targetPercentage / 100m);
            int targetQty = (int)Math.Truncate(targetValue / currentPrice);

            int diff = targetQty - currentQty;

            if (diff < 0) // Sell
            {
                int saleQty = Math.Abs(diff);
                totalSalesThisMonth += saleQty * currentPrice;
                
                if (custodyItem != null)
                {
                    saleDetails.Add(new { 
                        ticker = ticker, 
                        quantity = saleQty, 
                        salePrice = currentPrice, 
                        averagePrice = custodyItem.AveragePrice,
                        profit = saleQty * (currentPrice - custodyItem.AveragePrice)
                    });

                    custodyItem.Quantity -= saleQty;
                    _custodyRepo.Update(custodyItem);
                    
                    await NotifyKafkaIRSniper(client, ticker, "SELL", saleQty, currentPrice);

                    await _custodyRepo.AddHistoryAsync(new OperationHistory
                    {
                        ClientId = client.Id,
                        Ticker = ticker,
                        OperationType = "SELL",
                        Quantity = saleQty,
                        UnitPrice = currentPrice,
                        TotalValue = saleQty * currentPrice,
                        OperationDate = DateTime.UtcNow,
                        Reason = "REBALANCING"
                    });
                }
            }
            else if (diff > 0) // Buy
            {
                int purchaseQty = diff;
                if (custodyItem != null)
                {
                    var prevAvgPrice = custodyItem.AveragePrice;
                    var prevQty = custodyItem.Quantity;
                    custodyItem.AveragePrice = (prevQty * prevAvgPrice + purchaseQty * currentPrice) / (prevQty + purchaseQty);
                    custodyItem.Quantity += purchaseQty;
                    _custodyRepo.Update(custodyItem);
                }
                else
                {
                    await _custodyRepo.AddAsync(new ChildCustody
                    {
                        GraphicAccountId = client.GraphicAccount.Id,
                        Ticker = ticker,
                        Quantity = purchaseQty,
                        AveragePrice = currentPrice
                    });
                }
                
                await NotifyKafkaIRSniper(client, ticker, "BUY", purchaseQty, currentPrice);

                await _custodyRepo.AddHistoryAsync(new OperationHistory
                {
                    ClientId = client.Id,
                    Ticker = ticker,
                    OperationType = "BUY",
                    Quantity = purchaseQty,
                    UnitPrice = currentPrice,
                    TotalValue = purchaseQty * currentPrice,
                    OperationDate = DateTime.UtcNow,
                    Reason = "REBALANCING"
                });
            }
        }

        if (totalSalesThisMonth > 20000)
        {
            decimal totalProfit = saleDetails.Sum(d => (decimal)d.profit);
            if (totalProfit > 0)
            {
                decimal irValue = Math.Round(totalProfit * 0.20m, 2);
                await NotifyKafkaSaleIR(client, totalSalesThisMonth, totalProfit, irValue, saleDetails);
            }
        }
    }

    private async Task NotifyKafkaIRSniper(Client client, string ticker, string type, int qty, decimal price)
    {
        var operationValue = qty * price;
        var irValue = Math.Round(operationValue * 0.00005m, 2);

        try {
            await _kafkaProducer.PublishAsync("ir-dedo-duro", client.Cpf, new {
                type = "IR_DEDO_DURO",
                clientId = client.Id,
                cpf = client.Cpf,
                ticker = ticker,
                operationType = type,
                quantity = qty,
                unitPrice = price,
                operationValue = operationValue,
                rate = 0.00005m,
                irValue = irValue,
                operationDate = DateTime.UtcNow
            });
        } catch { }
    }

    private async Task NotifyKafkaSaleIR(Client client, decimal totalSales, decimal profit, decimal irValue, List<dynamic> details)
    {
        try {
            await _kafkaProducer.PublishAsync("ir-venda", client.Cpf, new {
                type = "IR_VENDA",
                clientId = client.Id,
                cpf = client.Cpf,
                referenceMonth = DateTime.UtcNow.ToString("yyyy-MM"),
                totalMonthlySales = totalSales,
                netProfit = profit,
                rate = 0.20m,
                irValue = irValue,
                details = details,
                calculationDate = DateTime.UtcNow
            });
        } catch { }
    }
}

public class RebalancingSummary
{
    public int ClientsAffected { get; set; }
    public List<string> RemovedAssets { get; set; } = new();
    public List<string> AddedAssets { get; set; } = new();
}
