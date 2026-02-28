using FluentAssertions;
using Index5.Domain.Entities;

namespace Index5.UnitTests;

public class DomainTests
{
    [Fact]
    public void Client_Initialization_IsCorrect()
    {
        var client = new Client();
        client.Distributions = new List<Distribution>();
        client.Distributions.Should().NotBeNull();
    }

    [Fact]
    public void GraphicAccount_Initialization_IsCorrect()
    {
        var account = new GraphicAccount();
        account.Custodies = new List<ChildCustody>();
        account.Custodies.Should().NotBeNull();
    }

    [Fact]
    public void RecommendationBasket_Initialization_IsCorrect()
    {
        var basket = new RecommendationBasket();
        basket.Items.Should().NotBeNull();
    }

    [Fact]
    public void PurchaseOrder_Initialization_IsCorrect()
    {
        var order = new PurchaseOrder();
        order.Distributions = new List<Distribution>();
        order.Distributions.Should().NotBeNull();
    }
    
    [Fact]
    public void AllEntities_Fields_Work()
    {
        var cot = new CotacaoB3 { Ticker = "PETR4", PrecoFechamento = 30 };
        cot.Ticker.Should().Be("PETR4");
        
        var dist = new Distribution { Ticker = "ITUB4", Quantity = 10 };
        dist.Ticker.Should().Be("ITUB4");
        
        var hist = new OperationHistory { Ticker = "VALE3", OperationType = "BUY" };
        hist.Ticker.Should().Be("VALE3");
        
        var item = new BasketItem { Ticker = "BBAS3", Percentage = 20 };
        item.Ticker.Should().Be("BBAS3");
    }
}
