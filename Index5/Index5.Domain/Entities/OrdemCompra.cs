namespace Index5.Domain.Entities;

public class OrdemCompra
{
    public int Id { get; set; }
    public DateTime DataExecucao { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public int QuantidadeTotal { get; set; }
    public decimal PrecoUnitario { get; set; }
    public decimal ValorTotal { get; set; }

    public ICollection<Distribuicao> Distribuicoes { get; set; } = new List<Distribuicao>();
}
