using System.Text.Json.Serialization;

namespace Index5.Domain.Entities;

public class GraphicAccount
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    [JsonIgnore]
    public Client? Client { get; set; }

    public ICollection<ChildCustody>? Custodies { get; set; }
}
