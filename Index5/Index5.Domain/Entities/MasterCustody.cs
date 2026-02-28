namespace Index5.Domain.Entities;

public class MasterCustody
{
    public int Id { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal AveragePrice { get; set; }
    public string? Origin { get; set; }
}
