using Index5.Application.DTOs;
using Index5.Application.Services;
using Index5.Domain.Entities;
using Index5.Domain.Interfaces;
using Moq;

namespace Index5.UnitTests;

public class CestaServiceTests
{
    private readonly Mock<ICestaRepository> _cestaRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ICotahistParser> _cotahistParserMock;
    private readonly Mock<IClienteRepository> _clienteRepoMock;
    private readonly Mock<ICustodiaRepository> _custodiaRepoMock;
    private readonly Mock<IKafkaProducer> _kafkaProducerMock;
    private readonly CestaService _service;

    public CestaServiceTests()
    {
        _cestaRepoMock = new Mock<ICestaRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _cotahistParserMock = new Mock<ICotahistParser>();
        _clienteRepoMock = new Mock<IClienteRepository>();
        _custodiaRepoMock = new Mock<ICustodiaRepository>();
        _kafkaProducerMock = new Mock<IKafkaProducer>();

        var configMock = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
        configMock.Setup(c => c["Cotacoes:Folder"]).Returns("cotacoes");

        var rebalanceamentoService = new RebalanceamentoService(
            _clienteRepoMock.Object, _custodiaRepoMock.Object,
            _unitOfWorkMock.Object, _kafkaProducerMock.Object);

        _service = new CestaService(
            _cestaRepoMock.Object, _unitOfWorkMock.Object,
            rebalanceamentoService, _cotahistParserMock.Object, configMock.Object);
    }

    // ==================== CADASTRAR CESTA ====================

    [Fact]
    public async Task CadastrarAsync_DeveCriarPrimeiraCesta_ComSucesso()
    {
        var request = new CestaRequest
        {
            Nome = "Top Five - Fevereiro 2026",
            Itens = new List<ItemCestaDto>
            {
                new() { Ticker = "PETR4", Percentual = 30m },
                new() { Ticker = "VALE3", Percentual = 25m },
                new() { Ticker = "ITUB4", Percentual = 20m },
                new() { Ticker = "BBDC4", Percentual = 15m },
                new() { Ticker = "WEGE3", Percentual = 10m }
            }
        };

        _cestaRepoMock.Setup(r => r.GetActiveAsync()).ReturnsAsync((CestaRecomendacao?)null);

        var result = await _service.CadastrarAsync(request);

        Assert.Equal("Top Five - Fevereiro 2026", result.Nome);
        Assert.True(result.Ativa);
        Assert.False(result.RebalanceamentoDisparado);
        Assert.Null(result.CestaAnteriorDesativada);
        Assert.Equal("Primeira cesta cadastrada com sucesso.", result.Mensagem);
        Assert.Equal(5, result.Itens.Count);

        _cestaRepoMock.Verify(r => r.AddAsync(It.IsAny<CestaRecomendacao>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task CadastrarAsync_DeveDesativarCestaAnterior_EDispararRebalanceamento()
    {
        var cestaAtual = new CestaRecomendacao
        {
            Id = 1, Nome = "Cesta Antiga", Ativa = true,
            Itens = new List<ItemCesta>
            {
                new() { Ticker = "PETR4", Percentual = 30m },
                new() { Ticker = "VALE3", Percentual = 25m },
                new() { Ticker = "ITUB4", Percentual = 20m },
                new() { Ticker = "BBDC4", Percentual = 15m },
                new() { Ticker = "WEGE3", Percentual = 10m }
            }
        };

        _cestaRepoMock.Setup(r => r.GetActiveAsync()).ReturnsAsync(cestaAtual);
        _clienteRepoMock.Setup(r => r.GetAllActivesAsync()).ReturnsAsync(new List<Cliente>());

        var request = new CestaRequest
        {
            Nome = "Cesta Nova",
            Itens = new List<ItemCestaDto>
            {
                new() { Ticker = "PETR4", Percentual = 25m },
                new() { Ticker = "VALE3", Percentual = 20m },
                new() { Ticker = "ITUB4", Percentual = 20m },
                new() { Ticker = "ABEV3", Percentual = 20m },
                new() { Ticker = "RENT3", Percentual = 15m }
            }
        };

        var result = await _service.CadastrarAsync(request);

        Assert.True(result.RebalanceamentoDisparado);
        Assert.NotNull(result.CestaAnteriorDesativada);
        Assert.Equal(1, result.CestaAnteriorDesativada!.CestaId);
        Assert.Contains("BBDC4", result.AtivosRemovidos!);
        Assert.Contains("WEGE3", result.AtivosRemovidos!);
        Assert.Contains("ABEV3", result.AtivosAdicionados!);
        Assert.Contains("RENT3", result.AtivosAdicionados!);
    }

    [Fact]
    public async Task CadastrarAsync_DeveLancarExcecao_QuandoNaoTem5Ativos()
    {
        var request = new CestaRequest
        {
            Nome = "Incompleta",
            Itens = new List<ItemCestaDto>
            {
                new() { Ticker = "PETR4", Percentual = 50m },
                new() { Ticker = "VALE3", Percentual = 50m }
            }
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CadastrarAsync(request));

        Assert.Equal("QUANTIDADE_ATIVOS_INVALIDA", ex.Message);
    }

    [Fact]
    public async Task CadastrarAsync_DeveLancarExcecao_QuandoPercentuaisNaoSomam100()
    {
        var request = new CestaRequest
        {
            Nome = "Errada",
            Itens = new List<ItemCestaDto>
            {
                new() { Ticker = "PETR4", Percentual = 30m },
                new() { Ticker = "VALE3", Percentual = 25m },
                new() { Ticker = "ITUB4", Percentual = 20m },
                new() { Ticker = "BBDC4", Percentual = 15m },
                new() { Ticker = "WEGE3", Percentual = 5m }
            }
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CadastrarAsync(request));

        Assert.Equal("PERCENTUAIS_INVALIDOS", ex.Message);
    }

    // ==================== CONSULTAR CESTA ATUAL ====================

    [Fact]
    public async Task GetActiveAsync_DeveRetornarCesta_QuandoExiste()
    {
        var cesta = new CestaRecomendacao
        {
            Id = 1, Nome = "Top Five", Ativa = true,
            DataCriacao = DateTime.UtcNow,
            Itens = new List<ItemCesta>
            {
                new() { Ticker = "PETR4", Percentual = 30m },
                new() { Ticker = "VALE3", Percentual = 25m },
                new() { Ticker = "ITUB4", Percentual = 20m },
                new() { Ticker = "BBDC4", Percentual = 15m },
                new() { Ticker = "WEGE3", Percentual = 10m }
            }
        };

        _cestaRepoMock.Setup(r => r.GetActiveAsync()).ReturnsAsync(cesta);

        var result = await _service.GetActiveAsync();

        Assert.NotNull(result);
        Assert.Equal("Top Five", result!.Nome);
        Assert.Equal(5, result.Itens.Count);
        Assert.True(result.Ativa);
    }

    [Fact]
    public async Task GetActiveAsync_DeveRetornarNull_QuandoNaoExiste()
    {
        _cestaRepoMock.Setup(r => r.GetActiveAsync()).ReturnsAsync((CestaRecomendacao?)null);

        var result = await _service.GetActiveAsync();

        Assert.Null(result);
    }

    // ==================== HISTÓRICO ====================

    [Fact]
    public async Task GetHistoricoAsync_DeveRetornarTodasCestas()
    {
        var cestas = new List<CestaRecomendacao>
        {
            new() { Id = 1, Nome = "Antiga", Ativa = false, DataCriacao = DateTime.UtcNow.AddMonths(-1),
                     DataDesativacao = DateTime.UtcNow, Itens = new List<ItemCesta>() },
            new() { Id = 2, Nome = "Atual", Ativa = true, DataCriacao = DateTime.UtcNow,
                     Itens = new List<ItemCesta>() }
        };

        _cestaRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(cestas);

        var result = await _service.GetHistoricoAsync();

        Assert.Equal(2, result.Cestas.Count);
        Assert.False(result.Cestas[0].Ativa);
        Assert.True(result.Cestas[1].Ativa);
    }
}
