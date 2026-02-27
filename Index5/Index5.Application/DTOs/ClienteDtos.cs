namespace Index5.Application.DTOs;

public class AdesaoRequest
{
    public string Nome { get; set; } = string.Empty;
    public string Cpf { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public decimal ValorMensal { get; set; }
}

public class AdesaoResponse
{
    public int ClienteId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Cpf { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public decimal ValorMensal { get; set; }
    public bool Ativo { get; set; }
    public DateTime DataAdesao { get; set; }
    public ContaGraficaDto? ContaGrafica { get; set; }
}

public class ContaGraficaDto
{
    public int Id { get; set; }
    public string NumeroConta { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public DateTime DataCriacao { get; set; }
}

public class SaidaResponse
{
    public int ClienteId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public bool Ativo { get; set; }
    public DateTime? DataSaida { get; set; }
    public string Mensagem { get; set; } = string.Empty;
}

public class AlterarValorRequest
{
    public decimal NovoValorMensal { get; set; }
}

public class AlterarValorResponse
{
    public int ClienteId { get; set; }
    public decimal ValorMensalAnterior { get; set; }
    public decimal ValorMensalNovo { get; set; }
    public DateTime DataAlteracao { get; set; }
    public string Mensagem { get; set; } = string.Empty;
}

public class ErrorResponse
{
    public string Erro { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
}
