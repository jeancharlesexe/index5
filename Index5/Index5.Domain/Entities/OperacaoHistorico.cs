namespace Index5.Domain.Entities;

public class OperacaoHistorico
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public string TipoOperacao { get; set; } = string.Empty; // COMPRA / VENDA
    public int Quantidade { get; set; }
    public decimal PrecoUnitario { get; set; }
    public decimal ValorTotal { get; set; }
    public DateTime DataOperacao { get; set; }
    public string Motivo { get; set; } = string.Empty; // COMPRA_PROGRAMADA / REBALANCEAMENTO

    public Cliente? Cliente { get; set; }
}
