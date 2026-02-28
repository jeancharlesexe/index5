using FluentAssertions;
using Index5.Application.DTOs;
using Index5.Application.Services;
using Index5.Domain.Entities;
using Index5.Domain.Interfaces;
using Moq;

namespace Index5.UnitTests;

public class ClientServiceTests
{
    private readonly Mock<IClientRepository> _clientRepoMock;
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly Mock<ICustodyRepository> _custodyRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly ClientService _service;

    public ClientServiceTests()
    {
        _clientRepoMock = new Mock<IClientRepository>();
        _userRepoMock = new Mock<IUserRepository>();
        _custodyRepoMock = new Mock<ICustodyRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _service = new ClientService(
            _clientRepoMock.Object,
            _userRepoMock.Object,
            _custodyRepoMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task JoinAsync_ExistingActiveClient_ThrowsDuplicateCpf()
    {
        _userRepoMock.Setup(r => r.GetByCpfAsync("123")).ReturnsAsync(new User { Cpf = "123" });
        _clientRepoMock.Setup(r => r.GetByCpfAsync("123")).ReturnsAsync(new Client { Active = true, ExitDate = null });
        
        var act = () => _service.JoinAsync(new JoinRequest { MonthlyValue = 1000 }, "123");
        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage("DUPLICATE_CPF");
    }

    [Fact]
    public async Task JoinAsync_ExistingExitedClient_ReactivatesAccount()
    {
        _userRepoMock.Setup(r => r.GetByCpfAsync("123")).ReturnsAsync(new User { Cpf = "123" });
        var client = new Client { Cpf = "123", Active = false, ExitDate = DateTime.Now };
        _clientRepoMock.Setup(r => r.GetByCpfAsync("123")).ReturnsAsync(client);

        var result = await _service.JoinAsync(new JoinRequest { MonthlyValue = 2000 }, "123");

        result.Message.Should().Contain("successfully reactivated");
        client.Active.Should().BeTrue();
        client.ExitDate.Should().BeNull();
        client.MonthlyValue.Should().Be(2000);
    }

    [Fact]
    public async Task GetByCpfAsync_ExistingClient_ReturnsJoinResponse()
    {
        var cpf = "12345678900";
        var client = new Client { Id = 1, Name = "Test", Cpf = cpf, Email = "test@test.com", Active = true, JoinDate = DateTime.UtcNow };
        _clientRepoMock.Setup(repo => repo.GetByCpfAsync(cpf)).ReturnsAsync(client);
        var result = await _service.GetByCpfAsync(cpf);
        result.Should().NotBeNull();
        result!.Cpf.Should().Be(cpf);
    }

    [Fact]
    public async Task ApproveClientAsync_ValidClient_SetsActiveAndCreatesAccount()
    {
        var client = new Client { Id = 1, Active = false, GraphicAccount = null };
        _clientRepoMock.Setup(repo => repo.GetByIdAsync(1)).ReturnsAsync(client);
        var result = await _service.ApproveClientAsync(1);
        result.Status.Should().Be("ACTIVE");
        client.Active.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateMonthlyValueAsync_InvalidValue_ThrowsException()
    {
        var client = new Client { Id = 1, MonthlyValue = 500 };
        _clientRepoMock.Setup(repo => repo.GetByIdAsync(1)).ReturnsAsync(client);
        var act = () => _service.UpdateMonthlyValueAsync(1, new UpdateMonthlyValueRequest { NewMonthlyValue = 50 });
        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage("INVALID_MONTHLY_VALUE");
    }

    [Fact]
    public async Task GetByCpfAsync_NonExisting_ReturnsNull()
    {
        _clientRepoMock.Setup(r => r.GetByCpfAsync(It.IsAny<string>())).ReturnsAsync((Client?)null);
        var result = await _service.GetByCpfAsync("999");
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeactivateClientAsync_ValidClient_SetsInactive()
    {
        var client = new Client { Id = 1, Active = true };
        _clientRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(client);
        var result = await _service.DeactivateClientAsync(1);
        result.Active.Should().BeFalse();
        client.ExitDate.Should().NotBeNull();
    }

    [Fact]
    public async Task RejectClientAsync_ValidClient_RemovesFromRepo()
    {
        var client = new Client { Id = 1 };
        _clientRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(client);
        await _service.RejectClientAsync(1);
        _clientRepoMock.Verify(r => r.Remove(client), Times.Once);
    }

    [Fact]
    public async Task GetPortfolioAsync_ValidClient_CalculatesProfits()
    {
        var client = new Client { 
            Id = 1, Name = "P", 
            GraphicAccount = new GraphicAccount { 
                AccountNumber = "A1", 
                Custodies = new List<ChildCustody> { 
                    new() { Ticker = "T1", Quantity = 10, AveragePrice = 100 } 
                } 
            } 
        };
        _clientRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(client);
        
        var result = await _service.GetPortfolioAsync(1, t => 120m);
        
        result.Summary.TotalPL.Should().Be(200); // (120-100)*10
        result.Summary.ProfitabilityPercentage.Should().Be(20);
        result.Assets.Single().PortfolioComposition.Should().Be(100);
    }

    [Fact]
    public async Task GetProfitabilityAsync_ValidClient_ReturnsData()
    {
        var client = new Client { Id = 1, GraphicAccount = new GraphicAccount { Id = 1 }};
        _clientRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(client);
        _custodyRepoMock.Setup(r => r.GetHistoryByClientIdAsync(1)).ReturnsAsync(new List<OperationHistory> {
            new() { OperationType = "BUY", Ticker = "A", Quantity = 1, UnitPrice = 10, TotalValue = 10, OperationDate = DateTime.Now, Reason = "COMPRA_PROGRAMADA" }
        });

        var result = await _service.GetProfitabilityAsync(1, t => 15m);
        result.ContributionHistory.Should().NotBeEmpty();
        result.PortfolioEvolution.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ApproveClient_AlreadyActive_ThrowsException()
    {
        var client = new Client { Id = 1, Active = true };
        _clientRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(client);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.ApproveClientAsync(1));
    }

    [Fact]
    public async Task JoinAsync_UserNotFound_ThrowsException()
    {
        _userRepoMock.Setup(r => r.GetByCpfAsync("1")).ReturnsAsync((User?)null);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.JoinAsync(new JoinRequest(), "1"));
    }

    [Fact]
    public async Task JoinAsync_InvalidValue_ThrowsException()
    {
        _userRepoMock.Setup(r => r.GetByCpfAsync("1")).ReturnsAsync(new User());
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.JoinAsync(new JoinRequest { MonthlyValue = 50 }, "1"));
    }

    [Fact]
    public async Task GetPendingClients_ReturnsList()
    {
        _clientRepoMock.Setup(r => r.GetPendingAsync()).ReturnsAsync(new List<Client> { new() { Id = 1, Name = "P" } });
        var result = await _service.GetPendingClientsAsync();
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetPagedActiveClients_ReturnsPagedResult()
    {
        _clientRepoMock.Setup(r => r.GetFilteredActiveAsync(null, null, null, 1, 10))
            .ReturnsAsync((new List<Client> { new() { Id = 1, Name = "A" } }, 1));
        
        var result = await _service.GetPagedActiveClientsAsync(null, null, null, 1, 10);
        result.Items.Should().HaveCount(1);
        result.TotalCount.Should().Be(1);
    }
}
