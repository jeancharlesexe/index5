using System.Text.Json.Serialization;

namespace Index5.Domain.Entities;

public class Client
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Cpf { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public decimal MonthlyValue { get; set; }
    public bool Active { get; set; }
    public DateTime JoinDate { get; set; }
    public DateTime? ExitDate { get; set; }

    public GraphicAccount? GraphicAccount { get; set; }

    [JsonIgnore]
    public ICollection<Distribution>? Distributions { get; set; }
}
