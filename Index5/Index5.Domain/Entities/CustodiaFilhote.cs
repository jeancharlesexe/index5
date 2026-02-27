namespace Index5.Domain.Entities;

public class CustodiaFilhote
{
    public int Id { get; set; }
    public int ContaGraficaId { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public int Quantidade { get; set; }
    public decimal PrecoMedio { get; set; }

    public ContaGrafica? ContaGrafica { get; set; }
}
