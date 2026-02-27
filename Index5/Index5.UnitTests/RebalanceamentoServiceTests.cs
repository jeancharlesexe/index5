using Index5.Application.Services;
using Index5.Domain.Entities;
using Index5.Domain.Interfaces;
using Moq;

namespace Index5.UnitTests;

public class RebalanceamentoServiceTests
{
    private readonly Mock<IClienteRepository> _clienteRepoMock;
    private readonly Mock<ICustodiaRepository> _custodiaRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IKafkaProducer> _kafkaProducerMock;
    private readonly RebalanceamentoService _service;

    public RebalanceamentoServiceTests()
    {
        _clienteRepoMock = new Mock<IClienteRepository>();
        _custodiaRepoMock = new Mock<ICustodiaRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _kafkaProducerMock = new Mock<IKafkaProducer>();

        _service = new RebalanceamentoService(
            _clienteRepoMock.Object, _custodiaRepoMock.Object,
            _unitOfWorkMock.Object, _kafkaProducerMock.Object);
    }

    [Fact]
    public async Task RebalancearTodosClientesAsync_DeveIdentificarAtivosRemovidosEAdicionados()
    {
        var cestaAntiga = new CestaRecomendacao
        {
            Itens = new List<ItemCesta>
            {
                new() { Ticker = "PETR4", Percentual = 30m },
                new() { Ticker = "VALE3", Percentual = 25m },
                new() { Ticker = "ITUB4", Percentual = 20m },
                new() { Ticker = "BBDC4", Percentual = 15m },
                new() { Ticker = "WEGE3", Percentual = 10m }
            }
        };

        var novaCesta = new CestaRecomendacao
        {
            Itens = new List<ItemCesta>
            {
                new() { Ticker = "PETR4", Percentual = 25m },
                new() { Ticker = "VALE3", Percentual = 20m },
                new() { Ticker = "ITUB4", Percentual = 20m },
                new() { Ticker = "ABEV3", Percentual = 20m },
                new() { Ticker = "RENT3", Percentual = 15m }
            }
        };

        _clienteRepoMock.Setup(r => r.GetAllActivesAsync()).ReturnsAsync(new List<Cliente>());

        var result = await _service.RebalancearTodosClientesAsync(novaCesta, cestaAntiga, _ => 50m);

        Assert.Contains("BBDC4", result.AtivosRemovidos);
        Assert.Contains("WEGE3", result.AtivosRemovidos);
        Assert.Contains("ABEV3", result.AtivosAdicionados);
        Assert.Contains("RENT3", result.AtivosAdicionados);
        Assert.Equal(0, result.ClientesAfetados); // No active clients
    }

    [Fact]
    public async Task RebalancearTodosClientesAsync_DeveProcessarTodosClientesAtivos()
    {
        var clientes = new List<Cliente>
        {
            new() { Id = 1, Nome = "Joao",
                     ContaGrafica = new ContaGrafica { Id = 1 } },
            new() { Id = 2, Nome = "Maria",
                     ContaGrafica = new ContaGrafica { Id = 2 } }
        };

        _clienteRepoMock.Setup(r => r.GetAllActivesAsync()).ReturnsAsync(clientes);
        _custodiaRepoMock.Setup(r => r.GetByContaGraficaIdAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<CustodiaFilhote>());

        var novaCesta = new CestaRecomendacao
        {
            Itens = new List<ItemCesta>
            {
                new() { Ticker = "PETR4", Percentual = 50m },
                new() { Ticker = "VALE3", Percentual = 20m },
                new() { Ticker = "ITUB4", Percentual = 15m },
                new() { Ticker = "BBDC4", Percentual = 10m },
                new() { Ticker = "WEGE3", Percentual = 5m }
            }
        };

        var result = await _service.RebalancearTodosClientesAsync(novaCesta, null, _ => 50m);

        Assert.Equal(2, result.ClientesAfetados);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task RebalancearTodosClientesAsync_DeveVenderAtivosRemovidos()
    {
        var cliente = new Cliente
        {
            Id = 1, Nome = "Joao", Cpf = "111",
            ContaGrafica = new ContaGrafica { Id = 1 }
        };

        var custodiaAtual = new List<CustodiaFilhote>
        {
            new() { Ticker = "BBDC4", Quantidade = 10, PrecoMedio = 15m, ContaGraficaId = 1 }
        };

        _clienteRepoMock.Setup(r => r.GetAllActivesAsync())
            .ReturnsAsync(new List<Cliente> { cliente });
        _custodiaRepoMock.Setup(r => r.GetByContaGraficaIdAsync(1))
            .ReturnsAsync(custodiaAtual);

        // Nova cesta NÃO contém BBDC4 → deve vender
        var novaCesta = new CestaRecomendacao
        {
            Itens = new List<ItemCesta>
            {
                new() { Ticker = "PETR4", Percentual = 30m },
                new() { Ticker = "VALE3", Percentual = 25m },
                new() { Ticker = "ITUB4", Percentual = 20m },
                new() { Ticker = "ABEV3", Percentual = 15m },
                new() { Ticker = "RENT3", Percentual = 10m }
            }
        };

        await _service.RebalancearTodosClientesAsync(novaCesta, null, _ => 20m);

        // Verifica que registrou a venda no histórico
        _custodiaRepoMock.Verify(r =>
            r.AddHistoricoAsync(It.Is<OperacaoHistorico>(o =>
                o.Ticker == "BBDC4" && o.TipoOperacao == "VENDA" && o.Motivo == "REBALANCEAMENTO")),
            Times.Once);
    }

    [Fact]
    public async Task RebalancearTodosClientesAsync_DeveComprarNovosAtivos()
    {
        var cliente = new Cliente
        {
            Id = 1, Nome = "Joao", Cpf = "111",
            ContaGrafica = new ContaGrafica { Id = 1 }
        };

        var custodiaAtual = new List<CustodiaFilhote>
        {
            new() { Ticker = "PETR4", Quantidade = 10, PrecoMedio = 35m, ContaGraficaId = 1 }
        };

        _clienteRepoMock.Setup(r => r.GetAllActivesAsync())
            .ReturnsAsync(new List<Cliente> { cliente });
        _custodiaRepoMock.Setup(r => r.GetByContaGraficaIdAsync(1))
            .ReturnsAsync(custodiaAtual);

        // Nova cesta contém ABEV3 (novo)
        var novaCesta = new CestaRecomendacao
        {
            Itens = new List<ItemCesta>
            {
                new() { Ticker = "PETR4", Percentual = 50m },
                new() { Ticker = "ABEV3", Percentual = 20m },
                new() { Ticker = "VALE3", Percentual = 15m },
                new() { Ticker = "ITUB4", Percentual = 10m },
                new() { Ticker = "RENT3", Percentual = 5m }
            }
        };

        await _service.RebalancearTodosClientesAsync(novaCesta, null, _ => 35m);

        // Verifica que adicionou ABEV3 à custódia
        _custodiaRepoMock.Verify(r =>
            r.AddAsync(It.Is<CustodiaFilhote>(c => c.Ticker == "ABEV3")),
            Times.Once);
    }
}
