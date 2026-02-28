using System.Text.Json.Serialization;

namespace Index5.Domain.Entities;

public class Distribution
{
    public int Id { get; set; }
    public int PurchaseOrderId { get; set; }
    public int ClientId { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public int Quantity { get; set; }

    [JsonIgnore]
    public PurchaseOrder? PurchaseOrder { get; set; }

    [JsonIgnore]
    public Client? Client { get; set; }
}
