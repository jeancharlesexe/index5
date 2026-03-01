using FluentAssertions;
using Index5.Application.DTOs;
using Index5.Application.Services;
using Index5.Domain.Entities;
using Index5.Domain.Interfaces;
using Moq;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Index5.UnitTests;

public class PurchaseEngineServiceTests
{
    private readonly Mock<IClientRepository> _clientRepoMock;
    private readonly Mock<IBasketRepository> _basketRepoMock;
    private readonly Mock<ICustodyRepository> _custodyRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IKafkaProducer> _kafkaProducerMock;
    private readonly Mock<ICotahistParser> _cotahistParserMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly PurchaseEngineService _service;

    public PurchaseEngineServiceTests()
    {
        _clientRepoMock = new Mock<IClientRepository>();
        _basketRepoMock = new Mock<IBasketRepository>();
        _custodyRepoMock = new Mock<ICustodyRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _kafkaProducerMock = new Mock<IKafkaProducer>();
        _cotahistParserMock = new Mock<ICotahistParser>();
        _configurationMock = new Mock<IConfiguration>();

        _service = new PurchaseEngineService(
            _clientRepoMock.Object,
            _basketRepoMock.Object,
            _custodyRepoMock.Object,
            _unitOfWorkMock.Object,
            _kafkaProducerMock.Object,
            _cotahistParserMock.Object,
            _configurationMock.Object);
    }

    [Fact]
    public async Task ExecutePurchaseAsync_Residue_GoesToMaster()
    {
        // Arrange
        var basket = new RecommendationBasket { Items = new List<BasketItem> { new() { Ticker = "PETR4", Percentage = 100 } } };
        var client = new Client { Id = 1, Cpf = "1", MonthlyValue = 300, GraphicAccount = new GraphicAccount { Id = 10 } }; // 100 contribution
        _basketRepoMock.Setup(repo => repo.GetActiveAsync()).ReturnsAsync(basket);
        _clientRepoMock.Setup(repo => repo.GetAllActiveAsync()).ReturnsAsync(new List<Client> { client });
        
        // Value for asset 100. Price 30. Qty = 3. Residue = ? 
        // distributed qty = (int)(3 * 1.0) = 3. residue = 3 - 3 = 0.
        // Let's use 2 clients to force residue.
        var client2 = new Client { Id = 2, Cpf = "2", MonthlyValue = 300, GraphicAccount = new GraphicAccount { Id = 11 } };
        _clientRepoMock.Setup(repo => repo.GetAllActiveAsync()).ReturnsAsync(new List<Client> { client, client2 });
        // Total contribution 200. Value for PETR4 200. Price 30. Qty = 6.
        // client 1 (100 contrib) -> 6 * 0.5 = 3 shares. client 2 (100 contrib) -> 6 * 0.5 = 3 shares. total 6. no residue.
        
        // Let's use asymmetrical contributions.
        var clientA = new Client { Id = 1, MonthlyValue = 300, GraphicAccount = new GraphicAccount { Id = 10 } }; // 100
        var clientB = new Client { Id = 2, MonthlyValue = 150, GraphicAccount = new GraphicAccount { Id = 11 } }; // 50
        _clientRepoMock.Setup(repo => repo.GetAllActiveAsync()).ReturnsAsync(new List<Client> { clientA, clientB });
        // Total 150. Value 150. Price 29. Qty = 5.
        // clientA (100/150 = 0.66) -> 5 * 0.66 = 3 shares.
        // clientB (50/150 = 0.33) -> 5 * 0.33 = 1 share.
        // total 4 shares. residue 1 share.
        
        var result = await _service.ExecutePurchaseAsync("test", t => 29m);

        result.MasterCustodyResidues.Should().Contain(r => r.Ticker == "PETR4" && r.Quantity == 1);
        _custodyRepoMock.Verify(r => r.AddMasterAsync(It.IsAny<MasterCustody>()), Times.Once);
    }

    [Fact]
    public async Task ExecutePurchaseAsync_ExistingMasterResidue_UpdatesAveragePrice()
    {
        var basket = new RecommendationBasket { Items = new List<BasketItem> { new() { Ticker = "PETR4", Percentage = 100 } } };
        var client = new Client { Id = 1, MonthlyValue = 300, GraphicAccount = new GraphicAccount { Id = 10 } }; // 100 contrib
        _basketRepoMock.Setup(repo => repo.GetActiveAsync()).ReturnsAsync(basket);
        _clientRepoMock.Setup(repo => repo.GetAllActiveAsync()).ReturnsAsync(new List<Client> { client });
        
        // Price 30. Contribution 100. Qty = 3. 
        // Force residue by making totalConsolidated different from client sum? 
        // No, totalConsolidated is calculated from clients.
        // Let's use 2 clients where proportions don't sum to integer shares.
        var clientA = new Client { Id = 1, MonthlyValue = 300, GraphicAccount = new GraphicAccount { Id = 10 } }; // 100
        var clientB = new Client { Id = 2, MonthlyValue = 150, GraphicAccount = new GraphicAccount { Id = 11 } }; // 50
        _clientRepoMock.Setup(repo => repo.GetAllActiveAsync()).ReturnsAsync(new List<Client> { clientA, clientB });
        // Total 150. Qty = 150 / 29 = 5. A(100/150)=3.33 -> 3. B(50/150)=1.66 -> 1. Total 4. Residue 1.
        
        var existingMaster = new MasterCustody { Ticker = "PETR4", Quantity = 1, AveragePrice = 10 };
        _custodyRepoMock.Setup(repo => repo.GetMasterByTickerAsync("PETR4")).ReturnsAsync(existingMaster);

        await _service.ExecutePurchaseAsync("test", t => 30m);

        // It used the 1 from master (Quantity 0). 
        // Then residue 1 @ 30 came back. So Total 1.
        existingMaster.Quantity.Should().Be(1);
        existingMaster.AveragePrice.Should().Be(30);
    }

    [Fact]
    public async Task ExecutePurchaseAsync_LotDetails_SplitsCorrectly()
    {
        var basket = new RecommendationBasket { Items = new List<BasketItem> { new() { Ticker = "PETR4", Percentage = 100 } } };
        var client = new Client { Id = 1, MonthlyValue = 6000, GraphicAccount = new GraphicAccount { Id = 10 } }; // 2000 contrib
        _basketRepoMock.Setup(repo => repo.GetActiveAsync()).ReturnsAsync(basket);
        _clientRepoMock.Setup(repo => repo.GetAllActiveAsync()).ReturnsAsync(new List<Client> { client });

        // Total 2000. Price 15. Qty = 133. (100 standard, 33 fractional)
        var result = await _service.ExecutePurchaseAsync("test", t => 15m);
        
        var order = result.PurchaseOrders.Single();
        order.Details.Should().Contain(d => d.Type == "STANDARD_LOT" && d.Quantity == 100);
        order.Details.Should().Contain(d => d.Type == "FRACTIONAL" && d.Quantity == 33);
    }

    [Fact]
    public async Task ExecutePurchaseAsync_NoBasket_ThrowsException()
    {
        _basketRepoMock.Setup(r => r.GetActiveAsync()).ReturnsAsync((RecommendationBasket?)null);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.ExecutePurchaseAsync("t", x => 10m));
    }

    [Fact]
    public async Task ExecutePurchaseAsync_NoClients_ThrowsException()
    {
        _basketRepoMock.Setup(r => r.GetActiveAsync()).ReturnsAsync(new RecommendationBasket());
        _clientRepoMock.Setup(r => r.GetAllActiveAsync()).ReturnsAsync(new List<Client>());
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.ExecutePurchaseAsync("t", x => 10m));
    }
}
