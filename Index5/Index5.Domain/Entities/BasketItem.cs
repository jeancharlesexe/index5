using System.Text.Json.Serialization;

namespace Index5.Domain.Entities;

public class BasketItem
{
    public int Id { get; set; }
    public int RecommendationBasketId { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public decimal Percentage { get; set; }

    [JsonIgnore]
    public RecommendationBasket? RecommendationBasket { get; set; }
}
