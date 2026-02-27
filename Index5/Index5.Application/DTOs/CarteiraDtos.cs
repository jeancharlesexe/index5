namespace Index5.Application.DTOs;

public class CarteiraResponse
{
    public int ClienteId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string ContaGrafica { get; set; } = string.Empty;
    public DateTime DataConsulta { get; set; }
    public ResumoCarteiraDto Resumo { get; set; } = new();
    public List<AtivoCarteiraDto> Ativos { get; set; } = new();
}

public class ResumoCarteiraDto
{
    public decimal ValorTotalInvestido { get; set; }
    public decimal ValorAtualCarteira { get; set; }
    public decimal PlTotal { get; set; }
    public decimal RentabilidadePercentual { get; set; }
}

public class AtivoCarteiraDto
{
    public string Ticker { get; set; } = string.Empty;
    public int Quantidade { get; set; }
    public decimal PrecoMedio { get; set; }
    public decimal CotacaoAtual { get; set; }
    public decimal ValorAtual { get; set; }
    public decimal Pl { get; set; }
    public decimal PlPercentual { get; set; }
    public decimal ComposicaoCarteira { get; set; }
}

public class RentabilidadeResponse
{
    public int ClienteId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public DateTime DataConsulta { get; set; }
    public ResumoCarteiraDto Rentabilidade { get; set; } = new();
    public List<AporteHistoricoDto> HistoricoAportes { get; set; } = new();
    public List<EvolucaoCarteiraDto> EvolucaoCarteira { get; set; } = new();
}

public class AporteHistoricoDto
{
    public string Data { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public string Parcela { get; set; } = string.Empty;
}

public class EvolucaoCarteiraDto
{
    public string Data { get; set; } = string.Empty;
    public decimal ValorCarteira { get; set; }
    public decimal ValorInvestido { get; set; }
    public decimal Rentabilidade { get; set; }
}
