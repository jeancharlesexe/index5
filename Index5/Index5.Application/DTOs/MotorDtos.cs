namespace Index5.Application.DTOs;

public class ExecutarCompraRequest
{
    public string DataReferencia { get; set; } = string.Empty;
}

public class ExecutarCompraResponse
{
    public DateTime DataExecucao { get; set; }
    public int TotalClientes { get; set; }
    public decimal TotalConsolidado { get; set; }
    public List<OrdemCompraDto> OrdensCompra { get; set; } = new();
    public List<DistribuicaoClienteDto> Distribuicoes { get; set; } = new();
    public List<ResiduoMasterDto> ResiduosCustMaster { get; set; } = new();
    public int EventosIRPublicados { get; set; }
    public string Mensagem { get; set; } = string.Empty;
}

public class OrdemCompraDto
{
    public string Ticker { get; set; } = string.Empty;
    public int QuantidadeTotal { get; set; }
    public List<DetalheOrdemDto> Detalhes { get; set; } = new();
    public decimal PrecoUnitario { get; set; }
    public decimal ValorTotal { get; set; }
}

public class DetalheOrdemDto
{
    public string Tipo { get; set; } = string.Empty;
    public string Ticker { get; set; } = string.Empty;
    public int Quantidade { get; set; }
}

public class DistribuicaoClienteDto
{
    public int ClienteId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public decimal ValorAporte { get; set; }
    public List<AtivoDistribuidoDto> Ativos { get; set; } = new();
}

public class AtivoDistribuidoDto
{
    public string Ticker { get; set; } = string.Empty;
    public int Quantidade { get; set; }
}

public class ResiduoMasterDto
{
    public string Ticker { get; set; } = string.Empty;
    public int Quantidade { get; set; }
}

public class CustodiaMasterResponse
{
    public ContaMasterDto ContaMaster { get; set; } = new();
    public List<CustodiaMasterItemDto> Custodia { get; set; } = new();
    public decimal ValorTotalResiduo { get; set; }
}

public class ContaMasterDto
{
    public int Id { get; set; }
    public string NumeroConta { get; set; } = string.Empty;
    public string Tipo { get; set; } = "MASTER";
}

public class CustodiaMasterItemDto
{
    public string Ticker { get; set; } = string.Empty;
    public int Quantidade { get; set; }
    public decimal PrecoMedio { get; set; }
    public decimal ValorAtual { get; set; }
    public string Origem { get; set; } = string.Empty;
}
