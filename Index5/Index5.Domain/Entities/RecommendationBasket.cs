namespace Index5.Domain.Entities;

public class RecommendationBasket
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Active { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DeactivatedAt { get; set; }

    public ICollection<BasketItem> Items { get; set; } = new List<BasketItem>();
}
