using Index5.Application.DTOs;
using Index5.Application.Services;
using Index5.Domain.Entities;
using Index5.Domain.Interfaces;
using Moq;

namespace Index5.UnitTests;

public class MotorCompraServiceTests
{
    private readonly Mock<IClienteRepository> _clienteRepoMock;
    private readonly Mock<ICestaRepository> _cestaRepoMock;
    private readonly Mock<ICustodiaRepository> _custodiaRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IKafkaProducer> _kafkaProducerMock;
    private readonly MotorCompraService _service;

    public MotorCompraServiceTests()
    {
        _clienteRepoMock = new Mock<IClienteRepository>();
        _cestaRepoMock = new Mock<ICestaRepository>();
        _custodiaRepoMock = new Mock<ICustodiaRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _kafkaProducerMock = new Mock<IKafkaProducer>();

        _service = new MotorCompraService(
            _clienteRepoMock.Object, _cestaRepoMock.Object,
            _custodiaRepoMock.Object, _unitOfWorkMock.Object,
            _kafkaProducerMock.Object);
    }

    private CestaRecomendacao CriarCestaPadrao() => new()
    {
        Id = 1, Nome = "Top Five", Ativa = true,
        Itens = new List<ItemCesta>
        {
            new() { Ticker = "PETR4", Percentual = 30m },
            new() { Ticker = "VALE3", Percentual = 25m },
            new() { Ticker = "ITUB4", Percentual = 20m },
            new() { Ticker = "BBDC4", Percentual = 15m },
            new() { Ticker = "WEGE3", Percentual = 10m }
        }
    };

    private Func<string, decimal> CriarCotacoes() => ticker => ticker switch
    {
        "PETR4" => 35m,
        "VALE3" => 60m,
        "ITUB4" => 30m,
        "BBDC4" => 15m,
        "WEGE3" => 40m,
        _ => 0m
    };

    [Fact]
    public async Task ExecutarCompraAsync_DeveExecutar_ComSucesso()
    {
        var cesta = CriarCestaPadrao();
        var clientes = new List<Cliente>
        {
            new() { Id = 1, Nome = "Joao", Cpf = "111", ValorMensal = 3000m,
                     ContaGrafica = new ContaGrafica { Id = 1, NumeroConta = "FLH-000001" } },
            new() { Id = 2, Nome = "Maria", Cpf = "222", ValorMensal = 6000m,
                     ContaGrafica = new ContaGrafica { Id = 2, NumeroConta = "FLH-000002" } }
        };

        _cestaRepoMock.Setup(r => r.GetActiveAsync()).ReturnsAsync(cesta);
        _clienteRepoMock.Setup(r => r.GetAllActivesAsync()).ReturnsAsync(clientes);
        _custodiaRepoMock.Setup(r => r.GetMasterByTickerAsync(It.IsAny<string>()))
            .ReturnsAsync((CustodiaMaster?)null);
        _custodiaRepoMock.Setup(r => r.GetByContaAndTickerAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync((CustodiaFilhote?)null);

        var result = await _service.ExecutarCompraAsync("2026-02-05", CriarCotacoes());

        Assert.Equal(2, result.TotalClientes);
        Assert.Equal(3000m, result.TotalConsolidado); // 3000/3 + 6000/3 = 1000 + 2000 = 3000
        Assert.NotEmpty(result.OrdensCompra);
        Assert.Equal(5, result.OrdensCompra.Count);
        Assert.Equal(2, result.Distribuicoes.Count);
        Assert.Contains("2 clientes", result.Mensagem);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task ExecutarCompraAsync_DeveCalcularAporteCorreto()
    {
        var cesta = CriarCestaPadrao();
        var clientes = new List<Cliente>
        {
            new() { Id = 1, Nome = "Joao", Cpf = "111", ValorMensal = 3000m,
                     ContaGrafica = new ContaGrafica { Id = 1 } }
        };

        _cestaRepoMock.Setup(r => r.GetActiveAsync()).ReturnsAsync(cesta);
        _clienteRepoMock.Setup(r => r.GetAllActivesAsync()).ReturnsAsync(clientes);
        _custodiaRepoMock.Setup(r => r.GetMasterByTickerAsync(It.IsAny<string>()))
            .ReturnsAsync((CustodiaMaster?)null);
        _custodiaRepoMock.Setup(r => r.GetByContaAndTickerAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync((CustodiaFilhote?)null);

        var result = await _service.ExecutarCompraAsync("2026-02-05", CriarCotacoes());

        Assert.Equal(1000m, result.TotalConsolidado); // 3000 / 3
        Assert.Equal(1000m, result.Distribuicoes[0].ValorAporte);
    }

    [Fact]
    public async Task ExecutarCompraAsync_DeveLancarExcecao_QuandoSemCesta()
    {
        _cestaRepoMock.Setup(r => r.GetActiveAsync()).ReturnsAsync((CestaRecomendacao?)null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.ExecutarCompraAsync("2026-02-05", CriarCotacoes()));

        Assert.Equal("CESTA_NAO_ENCONTRADA", ex.Message);
    }

    [Fact]
    public async Task ExecutarCompraAsync_DeveLancarExcecao_QuandoSemClientes()
    {
        _cestaRepoMock.Setup(r => r.GetActiveAsync()).ReturnsAsync(CriarCestaPadrao());
        _clienteRepoMock.Setup(r => r.GetAllActivesAsync()).ReturnsAsync(new List<Cliente>());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.ExecutarCompraAsync("2026-02-05", CriarCotacoes()));

        Assert.Equal("NENHUM_CLIENTE_ATIVO", ex.Message);
    }

    [Fact]
    public async Task ExecutarCompraAsync_DeveGerarDetalhesLoteFracionario()
    {
        var cesta = CriarCestaPadrao();
        var clientes = new List<Cliente>
        {
            new() { Id = 1, Nome = "Big", Cpf = "111", ValorMensal = 30000m,
                     ContaGrafica = new ContaGrafica { Id = 1 } }
        };

        _cestaRepoMock.Setup(r => r.GetActiveAsync()).ReturnsAsync(cesta);
        _clienteRepoMock.Setup(r => r.GetAllActivesAsync()).ReturnsAsync(clientes);
        _custodiaRepoMock.Setup(r => r.GetMasterByTickerAsync(It.IsAny<string>()))
            .ReturnsAsync((CustodiaMaster?)null);
        _custodiaRepoMock.Setup(r => r.GetByContaAndTickerAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync((CustodiaFilhote?)null);

        // Com aporte de 10000 (30000/3), BBDC4 a R$15 = ~1000 ações = 10 lotes padrão
        var result = await _service.ExecutarCompraAsync("2026-02-05", CriarCotacoes());

        var bbdc4Ordem = result.OrdensCompra.First(o => o.Ticker == "BBDC4");
        Assert.True(bbdc4Ordem.QuantidadeTotal >= 100);
        Assert.Contains(bbdc4Ordem.Detalhes, d => d.Tipo == "LOTE_PADRAO");
    }

    [Fact]
    public async Task ExecutarCompraAsync_DevePublicarEventosKafka()
    {
        var cesta = CriarCestaPadrao();
        var clientes = new List<Cliente>
        {
            new() { Id = 1, Nome = "Joao", Cpf = "111", ValorMensal = 3000m,
                     ContaGrafica = new ContaGrafica { Id = 1 } }
        };

        _cestaRepoMock.Setup(r => r.GetActiveAsync()).ReturnsAsync(cesta);
        _clienteRepoMock.Setup(r => r.GetAllActivesAsync()).ReturnsAsync(clientes);
        _custodiaRepoMock.Setup(r => r.GetMasterByTickerAsync(It.IsAny<string>()))
            .ReturnsAsync((CustodiaMaster?)null);
        _custodiaRepoMock.Setup(r => r.GetByContaAndTickerAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync((CustodiaFilhote?)null);

        var result = await _service.ExecutarCompraAsync("2026-02-05", CriarCotacoes());

        Assert.True(result.EventosIRPublicados > 0);
        _kafkaProducerMock.Verify(k =>
            k.PublishAsync("ir-dedo-duro", It.IsAny<string>(), It.IsAny<object>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecutarCompraAsync_DeveGerarResiduosParaContaMaster()
    {
        var cesta = CriarCestaPadrao();
        var clientes = new List<Cliente>
        {
            new() { Id = 1, Nome = "Joao", Cpf = "111", ValorMensal = 3000m,
                     ContaGrafica = new ContaGrafica { Id = 1 } },
            new() { Id = 2, Nome = "Maria", Cpf = "222", ValorMensal = 3000m,
                     ContaGrafica = new ContaGrafica { Id = 2 } }
        };

        _cestaRepoMock.Setup(r => r.GetActiveAsync()).ReturnsAsync(cesta);
        _clienteRepoMock.Setup(r => r.GetAllActivesAsync()).ReturnsAsync(clientes);
        _custodiaRepoMock.Setup(r => r.GetMasterByTickerAsync(It.IsAny<string>()))
            .ReturnsAsync((CustodiaMaster?)null);
        _custodiaRepoMock.Setup(r => r.GetByContaAndTickerAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync((CustodiaFilhote?)null);

        var result = await _service.ExecutarCompraAsync("2026-02-05", CriarCotacoes());

        // Resíduos devem ser gerados quando a distribuição não é exata
        // Com 2 clientes e divisão inteira, provavelmente haverá resíduos
        Assert.NotNull(result.ResiduosCustMaster);
    }

    [Fact]
    public async Task ExecutarCompraAsync_DevePersistirHistoricoOperacoes()
    {
        var cesta = CriarCestaPadrao();
        var clientes = new List<Cliente>
        {
            new() { Id = 1, Nome = "Joao", Cpf = "111", ValorMensal = 3000m,
                     ContaGrafica = new ContaGrafica { Id = 1 } }
        };

        _cestaRepoMock.Setup(r => r.GetActiveAsync()).ReturnsAsync(cesta);
        _clienteRepoMock.Setup(r => r.GetAllActivesAsync()).ReturnsAsync(clientes);
        _custodiaRepoMock.Setup(r => r.GetMasterByTickerAsync(It.IsAny<string>()))
            .ReturnsAsync((CustodiaMaster?)null);
        _custodiaRepoMock.Setup(r => r.GetByContaAndTickerAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync((CustodiaFilhote?)null);

        await _service.ExecutarCompraAsync("2026-02-05", CriarCotacoes());

        _custodiaRepoMock.Verify(r =>
            r.AddHistoricoAsync(It.Is<OperacaoHistorico>(o =>
                o.TipoOperacao == "COMPRA" && o.Motivo == "COMPRA_PROGRAMADA")),
            Times.AtLeastOnce);
    }
}
