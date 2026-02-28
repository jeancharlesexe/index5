using System.Text.Json.Serialization;

namespace Index5.Domain.Entities;

public class ChildCustody
{
    public int Id { get; set; }
    public int GraphicAccountId { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal AveragePrice { get; set; }

    [JsonIgnore]
    public GraphicAccount? GraphicAccount { get; set; }
}
