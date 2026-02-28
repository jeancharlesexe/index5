using Index5.Application.DTOs;
using Index5.Domain.Entities;
using Index5.Domain.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Index5.Application.Services;

public class BasketService
{
    private readonly IBasketRepository _basketRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly RebalancingService _rebalancingService;
    private readonly ICotahistParser _cotahistParser;
    private readonly string _quotesFolder;

    public BasketService(
        IBasketRepository basketRepo, 
        IUnitOfWork unitOfWork, 
        RebalancingService rebalancingService,
        ICotahistParser cotahistParser,
        IConfiguration config)
    {
        _basketRepo = basketRepo;
        _unitOfWork = unitOfWork;
        _rebalancingService = rebalancingService;
        _cotahistParser = cotahistParser;
        _quotesFolder = config["Cotacoes:Folder"] ?? "cotacoes";
    }

    public async Task<BasketResponse> CreateAsync(BasketRequest request)
    {
        if (request.Items.Count != 5)
            throw new InvalidOperationException("INVALID_ASSET_COUNT");

        var totalPercentage = request.Items.Sum(i => i.Percentage);
        if (totalPercentage != 100)
            throw new InvalidOperationException("INVALID_PERCENTAGES");

        var currentBasket = await _basketRepo.GetActiveAsync();
        RebalancingSummary? summary = null;
        PreviousBasketDto? previousDto = null;

        if (currentBasket != null)
        {
            previousDto = new PreviousBasketDto
            {
                BasketId = currentBasket.Id,
                Name = currentBasket.Name,
                DeactivatedAt = DateTime.UtcNow
            };

            currentBasket.Active = false;
            currentBasket.DeactivatedAt = previousDto.DeactivatedAt;
            _basketRepo.Update(currentBasket);
        }

        var newItems = request.Items.Select(i => new BasketItem
        {
            Ticker = i.Ticker,
            Percentage = i.Percentage
        }).ToList();

        var newBasket = new RecommendationBasket
        {
            Name = request.Name,
            Active = true,
            CreatedAt = DateTime.UtcNow,
            Items = newItems
        };

        await _basketRepo.AddAsync(newBasket);
        
        if (currentBasket != null)
        {
            summary = await _rebalancingService.RebalanceAllClientsAsync(
                newBasket, 
                currentBasket, 
                ticker => _cotahistParser.GetClosingQuote(_quotesFolder, ticker)?.PrecoFechamento ?? 0);
        }

        await _unitOfWork.SaveChangesAsync();

        return new BasketResponse
        {
            BasketId = newBasket.Id,
            Name = newBasket.Name,
            Active = true,
            CreatedAt = newBasket.CreatedAt,
            Items = newBasket.Items.Select(i => new BasketItemDto
            {
                Ticker = i.Ticker,
                Percentage = i.Percentage
            }).ToList(),
            PreviousBasketDeactivated = previousDto,
            RebalancingTriggered = summary != null,
            RemovedAssets = summary?.RemovedAssets,
            AddedAssets = summary?.AddedAssets,
            Message = summary != null
                ? $"Basket updated. Rebalancing triggered for {summary.ClientsAffected} active clients."
                : "First basket registered successfully."
        };
    }

    public async Task<BasketResponse?> GetActiveAsync()
    {
        var basket = await _basketRepo.GetActiveAsync();
        if (basket == null) return null;

        return new BasketResponse
        {
            BasketId = basket.Id,
            Name = basket.Name,
            Active = basket.Active,
            CreatedAt = basket.CreatedAt,
            Items = basket.Items.Select(i => new BasketItemDto
            {
                Ticker = i.Ticker,
                Percentage = i.Percentage
            }).ToList()
        };
    }

    public async Task<BasketHistoryResponse> GetHistoryAsync()
    {
        var baskets = await _basketRepo.GetAllAsync();

        return new BasketHistoryResponse
        {
            Baskets = baskets.Select(c => new BasketHistoryDto
            {
                BasketId = c.Id,
                Name = c.Name,
                Active = c.Active,
                CreatedAt = c.CreatedAt,
                DeactivatedAt = c.DeactivatedAt,
                Items = c.Items.Select(i => new BasketItemDto
                {
                    Ticker = i.Ticker,
                    Percentage = i.Percentage
                }).ToList()
            }).ToList()
        };
    }
}
