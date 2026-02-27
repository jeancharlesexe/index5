using Index5.Application.DTOs;
using Index5.Application.Services;
using Index5.Domain.Entities;
using Index5.Domain.Interfaces;
using Moq;

namespace Index5.UnitTests;

public class ClienteServiceTests
{
    private readonly Mock<IClienteRepository> _clienteRepoMock;
    private readonly Mock<ICustodiaRepository> _custodiaRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly ClienteService _service;

    public ClienteServiceTests()
    {
        _clienteRepoMock = new Mock<IClienteRepository>();
        _custodiaRepoMock = new Mock<ICustodiaRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _service = new ClienteService(_clienteRepoMock.Object, _custodiaRepoMock.Object, _unitOfWorkMock.Object);
    }

    // ==================== ADESÃO ====================

    [Fact]
    public async Task AderirAsync_DeveRetornar201_QuandoDadosValidos()
    {
        var request = new AdesaoRequest
        {
            Nome = "Joao da Silva",
            Cpf = "12345678901",
            Email = "joao@email.com",
            ValorMensal = 3000m
        };

        _clienteRepoMock.Setup(r => r.GetByCpfAsync(request.Cpf))
            .ReturnsAsync((Cliente?)null);

        var result = await _service.AderirAsync(request);

        Assert.Equal("Joao da Silva", result.Nome);
        Assert.Equal("12345678901", result.Cpf);
        Assert.Equal("joao@email.com", result.Email);
        Assert.Equal(3000m, result.ValorMensal);
        Assert.True(result.Ativo);
        Assert.NotNull(result.ContaGrafica);
        Assert.Equal("FILHOTE", result.ContaGrafica!.Tipo);
        Assert.StartsWith("FLH-", result.ContaGrafica.NumeroConta);

        _clienteRepoMock.Verify(r => r.AddAsync(It.IsAny<Cliente>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task AderirAsync_DeveLancarExcecao_QuandoCpfDuplicado()
    {
        var request = new AdesaoRequest { Cpf = "12345678901", ValorMensal = 1000m };

        _clienteRepoMock.Setup(r => r.GetByCpfAsync(request.Cpf))
            .ReturnsAsync(new Cliente { Cpf = request.Cpf });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.AderirAsync(request));
        
        Assert.Equal("CLIENTE_CPF_DUPLICADO", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(99.99)]
    public async Task AderirAsync_DeveLancarExcecao_QuandoValorMensalInvalido(decimal valor)
    {
        var request = new AdesaoRequest { Cpf = "99988877766", ValorMensal = valor };

        _clienteRepoMock.Setup(r => r.GetByCpfAsync(request.Cpf))
            .ReturnsAsync((Cliente?)null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.AderirAsync(request));

        Assert.Equal("VALOR_MENSAL_INVALIDO", ex.Message);
    }

    [Fact]
    public async Task AderirAsync_DeveAceitar_QuandoValorExatamente100()
    {
        var request = new AdesaoRequest
        {
            Nome = "Limite", Cpf = "11122233344",
            Email = "limite@email.com", ValorMensal = 100m
        };

        _clienteRepoMock.Setup(r => r.GetByCpfAsync(request.Cpf))
            .ReturnsAsync((Cliente?)null);

        var result = await _service.AderirAsync(request);
        Assert.Equal(100m, result.ValorMensal);
    }

    // ==================== SAÍDA ====================

    [Fact]
    public async Task SairAsync_DeveDesativarCliente_QuandoAtivo()
    {
        var cliente = new Cliente { Id = 1, Nome = "Joao", Ativo = true };

        _clienteRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(cliente);

        var result = await _service.SairAsync(1);

        Assert.Equal(1, result.ClienteId);
        Assert.False(result.Ativo);
        Assert.NotNull(result.DataSaida);
        Assert.Equal("Adesao encerrada. Sua posicao em custodia foi mantida.", result.Mensagem);
        _clienteRepoMock.Verify(r => r.Update(cliente), Times.Once);
    }

    [Fact]
    public async Task SairAsync_DeveLancarExcecao_QuandoClienteNaoEncontrado()
    {
        _clienteRepoMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Cliente?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.SairAsync(999));
    }

    [Fact]
    public async Task SairAsync_DeveLancarExcecao_QuandoClienteJaInativo()
    {
        var cliente = new Cliente { Id = 1, Ativo = false };
        _clienteRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(cliente);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.SairAsync(1));

        Assert.Equal("CLIENTE_JA_INATIVO", ex.Message);
    }

    // ==================== ALTERAR VALOR ====================

    [Fact]
    public async Task AlterarValorMensalAsync_DeveAtualizar_QuandoValorValido()
    {
        var cliente = new Cliente { Id = 1, ValorMensal = 3000m };
        _clienteRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(cliente);

        var request = new AlterarValorRequest { NovoValorMensal = 6000m };
        var result = await _service.AlterarValorMensalAsync(1, request);

        Assert.Equal(3000m, result.ValorMensalAnterior);
        Assert.Equal(6000m, result.ValorMensalNovo);
        Assert.Contains("Valor mensal atualizado", result.Mensagem);
    }

    [Fact]
    public async Task AlterarValorMensalAsync_DeveLancarExcecao_QuandoValorInvalido()
    {
        var cliente = new Cliente { Id = 1, ValorMensal = 3000m };
        _clienteRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(cliente);

        var request = new AlterarValorRequest { NovoValorMensal = 50m };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.AlterarValorMensalAsync(1, request));

        Assert.Equal("VALOR_MENSAL_INVALIDO", ex.Message);
    }

    [Fact]
    public async Task AlterarValorMensalAsync_DeveLancarExcecao_QuandoClienteNaoEncontrado()
    {
        _clienteRepoMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Cliente?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.AlterarValorMensalAsync(999, new AlterarValorRequest { NovoValorMensal = 1000m }));
    }

    // ==================== CONSULTAR CARTEIRA ====================

    [Fact]
    public async Task ConsultarCarteiraAsync_DeveRetornarCarteira_ComAtivos()
    {
        var cliente = new Cliente
        {
            Id = 1, Nome = "Joao",
            ContaGrafica = new ContaGrafica
            {
                Id = 1, NumeroConta = "FLH-000001", Tipo = "FILHOTE",
                Custodias = new List<CustodiaFilhote>
                {
                    new() { Ticker = "PETR4", Quantidade = 10, PrecoMedio = 35m },
                    new() { Ticker = "VALE3", Quantidade = 5, PrecoMedio = 60m }
                }
            }
        };
        _clienteRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(cliente);

        Func<string, decimal> getCotacao = ticker => ticker switch
        {
            "PETR4" => 37m,
            "VALE3" => 65m,
            _ => 0m
        };

        var result = await _service.ConsultarCarteiraAsync(1, getCotacao);

        Assert.Equal(1, result.ClienteId);
        Assert.Equal("FLH-000001", result.ContaGrafica);
        Assert.Equal(2, result.Ativos.Count);
        Assert.Equal(695m, result.Resumo.ValorAtualCarteira); // 10*37 + 5*65
        Assert.Equal(650m, result.Resumo.ValorTotalInvestido); // 10*35 + 5*60
        Assert.Equal(45m, result.Resumo.PlTotal);
        Assert.True(result.Resumo.RentabilidadePercentual > 0);

        var petr4 = result.Ativos.First(a => a.Ticker == "PETR4");
        Assert.Equal(10, petr4.Quantidade);
        Assert.Equal(37m, petr4.CotacaoAtual);
        Assert.Equal(370m, petr4.ValorAtual);
        Assert.True(petr4.ComposicaoCarteira > 0);
    }

    [Fact]
    public async Task ConsultarCarteiraAsync_DeveRetornarVazio_QuandoSemCustodia()
    {
        var cliente = new Cliente
        {
            Id = 1, Nome = "Novo",
            ContaGrafica = new ContaGrafica
            {
                Id = 1, NumeroConta = "FLH-000002", Tipo = "FILHOTE",
                Custodias = new List<CustodiaFilhote>()
            }
        };
        _clienteRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(cliente);

        var result = await _service.ConsultarCarteiraAsync(1, _ => 0);

        Assert.Empty(result.Ativos);
        Assert.Equal(0, result.Resumo.ValorTotalInvestido);
    }

    // ==================== RENTABILIDADE ====================

    [Fact]
    public async Task GetRentabilidadeAsync_DeveRetornarHistorico()
    {
        var cliente = new Cliente
        {
            Id = 1, Nome = "Joao",
            ContaGrafica = new ContaGrafica
            {
                Id = 1, NumeroConta = "FLH-000001", Tipo = "FILHOTE",
                Custodias = new List<CustodiaFilhote>
                {
                    new() { Ticker = "PETR4", Quantidade = 10, PrecoMedio = 35m }
                }
            }
        };
        _clienteRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(cliente);

        var historico = new List<OperacaoHistorico>
        {
            new() { ClienteId = 1, Ticker = "PETR4", TipoOperacao = "COMPRA",
                     Quantidade = 10, PrecoUnitario = 35m, ValorTotal = 350m,
                     DataOperacao = DateTime.UtcNow, Motivo = "COMPRA_PROGRAMADA" }
        };
        _custodiaRepoMock.Setup(r => r.GetHistoricoByClienteIdAsync(1)).ReturnsAsync(historico);

        var result = await _service.GetRentabilidadeAsync(1, _ => 37m);

        Assert.Equal(1, result.ClienteId);
        Assert.NotNull(result.Rentabilidade);
        Assert.NotEmpty(result.HistoricoAportes);
        Assert.NotEmpty(result.EvolucaoCarteira);
    }
}
