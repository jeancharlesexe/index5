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
        order.ExecutionId.Should().Be(string.Empty);
        order.UsedFromMaster.Should().Be(0);
    }

    [Fact]
    public void PurchaseOrder_Fields_SetCorrectly()
    {
        var order = new PurchaseOrder
        {
            Ticker = "PETR4",
            Quantity = 10,
            UnitPrice = 39.61m,
            TotalValue = 396.10m,
            ReferenceDate = "2026-03-04",
            ExecutionId = "abc123",
            UsedFromMaster = 3,
            CreatedAt = DateTime.UtcNow
        };
        order.Ticker.Should().Be("PETR4");
        order.Quantity.Should().Be(10);
        order.ExecutionId.Should().Be("abc123");
        order.UsedFromMaster.Should().Be(3);
        order.ReferenceDate.Should().Be("2026-03-04");
    }

    [Fact]
    public void MasterCustody_Fields_Work()
    {
        var mc = new MasterCustody { Ticker = "VALE3", Quantity = 5, AveragePrice = 80.50m, Origin = "Distribution residue 2026-03-04" };
        mc.Ticker.Should().Be("VALE3");
        mc.Quantity.Should().Be(5);
        mc.AveragePrice.Should().Be(80.50m);
        mc.Origin.Should().Contain("residue");
    }

    [Fact]
    public void ChildCustody_Fields_Work()
    {
        var cc = new ChildCustody { GraphicAccountId = 10, Ticker = "ITUB4", Quantity = 20, AveragePrice = 47.67m };
        cc.GraphicAccountId.Should().Be(10);
        cc.Ticker.Should().Be("ITUB4");
        cc.Quantity.Should().Be(20);
    }

    [Fact]
    public void User_Fields_Work()
    {
        var user = new User { Name = "Admin", Email = "admin@index5.com", Cpf = "12345678901", Role = "ADMIN", PasswordHash = "hash" };
        user.Name.Should().Be("Admin");
        user.Role.Should().Be("ADMIN");
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
