namespace Index5.Domain.Entities;

public class ContaGrafica
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public string NumeroConta { get; set; } = string.Empty;
    public string Tipo { get; set; } = "FILHOTE";
    public DateTime DataCriacao { get; set; }

    public Cliente? Cliente { get; set; }
    public ICollection<CustodiaFilhote> Custodias { get; set; } = new List<CustodiaFilhote>();
}
