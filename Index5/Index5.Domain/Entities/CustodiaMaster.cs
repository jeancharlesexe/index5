namespace Index5.Domain.Entities;

public class CustodiaMaster
{
    public int Id { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public int Quantidade { get; set; }
    public decimal PrecoMedio { get; set; }
    public string? Origem { get; set; }
}
