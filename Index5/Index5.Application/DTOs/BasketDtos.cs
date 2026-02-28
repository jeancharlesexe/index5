namespace Index5.Application.DTOs;

public class BasketRequest
{
    public string Name { get; set; } = string.Empty;
    public List<BasketItemDto> Items { get; set; } = new();
}

public class BasketItemDto
{
    public string Ticker { get; set; } = string.Empty;
    public decimal Percentage { get; set; }
    public decimal? CurrentQuote { get; set; }
}

public class BasketResponse
{
    public int BasketId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Active { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<BasketItemDto> Items { get; set; } = new();

    // Rebalancing details
    public PreviousBasketDto? PreviousBasketDeactivated { get; set; }
    public bool RebalancingTriggered { get; set; }
    public List<string>? RemovedAssets { get; set; }
    public List<string>? AddedAssets { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class PreviousBasketDto
{
    public int BasketId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime DeactivatedAt { get; set; }
}

public class BasketHistoryResponse
{
    public List<BasketHistoryDto> Baskets { get; set; } = new();
}

public class BasketHistoryDto
{
    public int BasketId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Active { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DeactivatedAt { get; set; }
    public List<BasketItemDto> Items { get; set; } = new();
}
