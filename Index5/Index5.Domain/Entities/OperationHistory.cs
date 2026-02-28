using System.Text.Json.Serialization;

namespace Index5.Domain.Entities;

public class OperationHistory
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public string OperationType { get; set; } = string.Empty; // COMPRA/VENDA -> BUY/SELL
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalValue { get; set; }
    public DateTime OperationDate { get; set; }
    public string Reason { get; set; } = string.Empty;

    [JsonIgnore]
    public Client? Client { get; set; }
}
