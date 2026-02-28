using FluentAssertions;
using Index5.Application.DTOs;
using Index5.Application.Services;
using Index5.Domain.Entities;
using Index5.Domain.Interfaces;
using Moq;
using Microsoft.Extensions.Configuration;

namespace Index5.UnitTests;

public class BasketServiceTests
{
    private readonly Mock<IBasketRepository> _basketRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ICotahistParser> _cotahistParserMock;
    private readonly Mock<IClientRepository> _clientRepoMock;
    private readonly Mock<ICustodyRepository> _custodyRepoMock;
    private readonly Mock<IKafkaProducer> _kafkaProducerMock;
    
    private readonly RebalancingService _rebalancingService;
    private readonly BasketService _service;

    public BasketServiceTests()
    {
        _basketRepoMock = new Mock<IBasketRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _cotahistParserMock = new Mock<ICotahistParser>();
        _clientRepoMock = new Mock<IClientRepository>();
        _custodyRepoMock = new Mock<ICustodyRepository>();
        _kafkaProducerMock = new Mock<IKafkaProducer>();

        _rebalancingService = new RebalancingService(
            _clientRepoMock.Object,
            _custodyRepoMock.Object,
            _unitOfWorkMock.Object,
            _kafkaProducerMock.Object);

        var configMock = new Mock<IConfiguration>();
        configMock.SetupGet(x => x["Cotacoes:Folder"]).Returns("mockfolder");

        _service = new BasketService(
            _basketRepoMock.Object,
            _unitOfWorkMock.Object,
            _rebalancingService,
            _cotahistParserMock.Object,
            configMock.Object);
            
        _clientRepoMock.Setup(r => r.GetAllActiveAsync()).ReturnsAsync(new List<Client>());
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_CreatesBasket()
    {
        var request = new BasketRequest {
            Name = "New Basket",
            Items = new List<BasketItemDto> {
                new() { Ticker = "T1", Percentage = 20 }, new() { Ticker = "T2", Percentage = 20 },
                new() { Ticker = "T3", Percentage = 20 }, new() { Ticker = "T4", Percentage = 20 },
                new() { Ticker = "T5", Percentage = 20 }
            }
        };
        _basketRepoMock.Setup(repo => repo.GetActiveAsync()).ReturnsAsync((RecommendationBasket?)null);

        var result = await _service.CreateAsync(request);

        result.Active.Should().BeTrue();
        result.Items.Count.Should().Be(5);
    }

    [Fact]
    public async Task CreateAsync_InvalidPercentageSum_ThrowsException()
    {
        var request = new BasketRequest {
            Name = "Invalid",
            Items = new List<BasketItemDto> { new() { Ticker = "T1", Percentage = 50 } } 
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateAsync(request));
    }

    [Fact]
    public async Task GetActiveAsync_NoBasketFound_ReturnsNull()
    {
        _basketRepoMock.Setup(r => r.GetActiveAsync()).ReturnsAsync((RecommendationBasket?)null);
        var result = await _service.GetActiveAsync();
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsMappedList()
    {
        var baskets = new List<RecommendationBasket> { new() { Items = new List<BasketItem>() } };
        _basketRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(baskets);
        var result = await _service.GetHistoryAsync();
        result.Baskets.Should().HaveCount(1);
    }

    [Fact]
    public async Task CreateAsync_InvalidAssetCount_ThrowsException()
    {
        var request = new BasketRequest { Items = new List<BasketItemDto> { new() { Ticker = "A", Percentage = 100 } } };
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateAsync(request));
    }

    [Fact]
    public async Task CreateAsync_ExistingBasket_TriggersRebalance()
    {
        var prevItem = new BasketItem { Ticker = "OLD", Percentage = 100 };
        var currentBasket = new RecommendationBasket { Id = 1, Name = "Previous", Active = true, Items = new List<BasketItem> { prevItem } };

        _basketRepoMock.Setup(repo => repo.GetActiveAsync()).ReturnsAsync(currentBasket);

        var request = new BasketRequest {
            Name = "New",
            Items = new List<BasketItemDto> {
                new() { Ticker = "T1", Percentage = 20 }, new() { Ticker = "T2", Percentage = 20 },
                new() { Ticker = "T3", Percentage = 20 }, new() { Ticker = "T4", Percentage = 20 },
                new() { Ticker = "T5", Percentage = 20 }
            }
        };

        var result = await _service.CreateAsync(request);

        result.PreviousBasketDeactivated.Should().NotBeNull();
        currentBasket.Active.Should().BeFalse();
    }
}
