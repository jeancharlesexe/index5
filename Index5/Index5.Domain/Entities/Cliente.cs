namespace Index5.Domain.Entities;

public class Cliente
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Cpf { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public decimal ValorMensal { get; set; }
    public bool Ativo { get; set; } = true;
    public DateTime DataAdesao { get; set; }
    public DateTime? DataSaida { get; set; }

    public ContaGrafica? ContaGrafica { get; set; }
    public ICollection<Distribuicao> Distribuicoes { get; set; } = new List<Distribuicao>();
}
