namespace Index5.Domain.Entities;

public class PurchaseOrder
{
    public int Id { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalValue { get; set; }
    public string ReferenceDate { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public ICollection<Distribution>? Distributions { get; set; }
}
