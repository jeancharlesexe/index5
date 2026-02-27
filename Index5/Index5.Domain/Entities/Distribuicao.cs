namespace Index5.Domain.Entities;

public class Distribuicao
{
    public int Id { get; set; }
    public int OrdemCompraId { get; set; }
    public int ClienteId { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public int Quantidade { get; set; }
    public DateTime DataDistribuicao { get; set; }

    public OrdemCompra? OrdemCompra { get; set; }
    public Cliente? Cliente { get; set; }
}
