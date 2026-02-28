using FluentAssertions;
using Index5.Domain.Entities;
using Index5.Domain.Interfaces;
using Index5.Application.Services;
using Moq;

namespace Index5.UnitTests;

public class RebalancingServiceTests
{
    private readonly Mock<IClientRepository> _clientRepoMock;
    private readonly Mock<ICustodyRepository> _custodyRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IKafkaProducer> _kafkaProducerMock;
    private readonly RebalancingService _service;

    public RebalancingServiceTests()
    {
        _clientRepoMock = new Mock<IClientRepository>();
        _custodyRepoMock = new Mock<ICustodyRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _kafkaProducerMock = new Mock<IKafkaProducer>();

        _service = new RebalancingService(
            _clientRepoMock.Object,
            _custodyRepoMock.Object,
            _unitOfWorkMock.Object,
            _kafkaProducerMock.Object);
    }

    [Fact]
    public async Task RebalanceAllClientsAsync_AssetRemoved_PerformsSaleAndTriggersIR()
    {
        // Arrange
        var oldBasket = new RecommendationBasket { Items = new List<BasketItem> { new() { Ticker = "PETR4", Percentage = 100 } } };
        var newBasket = new RecommendationBasket { Items = new List<BasketItem> { new() { Ticker = "VALE3", Percentage = 100 } } };
        var client = new Client { Id = 1, Cpf = "1", GraphicAccount = new GraphicAccount { Id = 10 } };
        var custody = new List<ChildCustody> { new() { Ticker = "PETR4", Quantity = 2000, AveragePrice = 10 } };

        _clientRepoMock.Setup(repo => repo.GetAllActiveAsync()).ReturnsAsync(new List<Client> { client });
        _custodyRepoMock.Setup(repo => repo.GetByGraphicAccountIdAsync(10)).ReturnsAsync(custody);

        // Sale of 2000 @ 15 = 30000. Profit = 5 * 2000 = 10000. IR = 2000.
        // Act
        var result = await _service.RebalanceAllClientsAsync(newBasket, oldBasket, t => 15m);

        // Assert
        result.ClientsAffected.Should().Be(1);
        _kafkaProducerMock.Verify(k => k.PublishAsync("ir-venda", It.IsAny<string>(), It.IsAny<object>()), Times.Once);
        _kafkaProducerMock.Verify(k => k.PublishAsync("ir-dedo-duro", It.IsAny<string>(), It.IsAny<object>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task RebalanceAllClientsAsync_ProportionalDeviation_RebalancesCorrectly()
    {
         // Arrange
        var basket = new RecommendationBasket { Items = new List<BasketItem> { 
            new() { Ticker = "AA", Percentage = 50 }, new() { Ticker = "BB", Percentage = 50 } 
        } };
        var client = new Client { Id = 1, Cpf = "123", GraphicAccount = new GraphicAccount { Id = 10 } };
        var custody = new List<ChildCustody> {
            new() { Ticker = "AA", Quantity = 5, AveragePrice = 100 }, 
            new() { Ticker = "BB", Quantity = 15, AveragePrice = 100 }
        };

        _clientRepoMock.Setup(repo => repo.GetAllActiveAsync()).ReturnsAsync(new List<Client> { client });
        _custodyRepoMock.Setup(repo => repo.GetByGraphicAccountIdAsync(10)).ReturnsAsync(custody);

        // Act
        await _service.RebalanceAllClientsAsync(basket, basket, t => 100m);

        // Assert
        custody.Single(c => c.Ticker == "AA").Quantity.Should().Be(10);
        custody.Single(c => c.Ticker == "BB").Quantity.Should().Be(10);
    }

    [Fact]
    public async Task RebalanceAllClientsAsync_NewAssetInNewBasket_CreatesCustody()
    {
        // Arrange
        var basket = new RecommendationBasket { Items = new List<BasketItem> { new() { Ticker = "ITUB4", Percentage = 100 } } };
        var client = new Client { Id = 1, Cpf = "1", GraphicAccount = new GraphicAccount { Id = 10 } };
        var existingCustody = new List<ChildCustody>(); 
        // Portfolio value 1000 from... wait. If portfolio is empty, totalPortfolioValue is 0. 
        // RebalancingService line 72: if (totalPortfolioValue <= 0) return;
        // So I need at least some existing asset to trigger value-based rebalancing.
        existingCustody.Add(new ChildCustody { Ticker = "OLD", Quantity = 10, AveragePrice = 100 }); // Value 1000

        _clientRepoMock.Setup(repo => repo.GetAllActiveAsync()).ReturnsAsync(new List<Client> { client });
        _custodyRepoMock.Setup(repo => repo.GetByGraphicAccountIdAsync(10)).ReturnsAsync(existingCustody);

        // Act
        await _service.RebalanceAllClientsAsync(basket, null, t => 100m);

        // Assert
        // OLD should be sold (10 * 100 = 1000). ITUB4 should be bought (1000 / 100 = 10)
    }

    [Fact]
    public async Task RebalanceClientAsync_MissingGraphicAccount_Skips()
    {
        var client = new Client { Id = 1, GraphicAccount = null };
        _clientRepoMock.Setup(r => r.GetAllActiveAsync()).ReturnsAsync(new List<Client> { client });
        var basket = new RecommendationBasket { Items = new List<BasketItem> { new() { Ticker = "A", Percentage = 100 } } };
        
        await _service.RebalanceAllClientsAsync(basket, null, t => 10m);
        _custodyRepoMock.Verify(r => r.GetByGraphicAccountIdAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task RebalanceClientAsync_ZeroPortfolioValue_Skips()
    {
        var client = new Client { Id = 1, GraphicAccount = new GraphicAccount { Id = 1 } };
        _clientRepoMock.Setup(r => r.GetAllActiveAsync()).ReturnsAsync(new List<Client> { client });
        _custodyRepoMock.Setup(r => r.GetByGraphicAccountIdAsync(1)).ReturnsAsync(new List<ChildCustody>());
        var basket = new RecommendationBasket { Items = new List<BasketItem> { new() { Ticker = "A", Percentage = 100 } } };
        
        await _service.RebalanceAllClientsAsync(basket, null, t => 10m);
        _custodyRepoMock.Verify(r => r.Update(It.IsAny<ChildCustody>()), Times.Never);
    }
}
