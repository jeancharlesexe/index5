using FluentAssertions;
using Index5.Domain.Entities;
using Index5.Infrastructure.Data;
using Index5.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Index5.UnitTests;

public class InfrastructureTests
{
    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task ClientRepository_GetById_Works()
    {
        using var context = CreateContext();
        var repo = new ClientRepository(context);
        var client = new Client { Cpf = "456", Name = "By ID", Email = "b@b.com" };
        context.Clients.Add(client);
        await context.SaveChangesAsync();

        var result = await repo.GetByIdAsync(client.Id);
        result.Should().NotBeNull();
        result!.Cpf.Should().Be("456");
    }

    [Fact]
    public async Task ClientRepository_GetByCpf_Works()
    {
        using var context = CreateContext();
        var repo = new ClientRepository(context);
        var client = new Client { Cpf = "123", Name = "By CPF", Email = "c@c.com" };
        context.Clients.Add(client);
        await context.SaveChangesAsync();

        var result = await repo.GetByCpfAsync("123");
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task BasketRepository_GetActive_Works()
    {
        using var context = CreateContext();
        var repo = new BasketRepository(context);
        var basket = new RecommendationBasket { Name = "Active", Active = true, CreatedAt = DateTime.UtcNow };
        context.RecommendationBaskets.Add(basket);
        await context.SaveChangesAsync();

        var result = await repo.GetActiveAsync();
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task BasketRepository_UpdateAndGetAll_Works()
    {
        using var context = CreateContext();
        var repo = new BasketRepository(context);
        var basket = new RecommendationBasket { Name = "B1", Active = true, CreatedAt = DateTime.UtcNow };
        await repo.AddAsync(basket);
        await context.SaveChangesAsync();

        basket.Active = false;
        repo.Update(basket);
        await context.SaveChangesAsync();

        var all = await repo.GetAllAsync();
        all.Should().HaveCount(1);
        all[0].Active.Should().BeFalse();
    }

    [Fact]
    public async Task CustodyRepository_GetMaster_Works()
    {
        using var context = CreateContext();
        var repo = new CustodyRepository(context);
        var master = new MasterCustody { Ticker = "PETR4", Quantity = 100 };
        context.MasterCustodies.Add(master);
        await context.SaveChangesAsync();

        var result = await repo.GetMasterByTickerAsync("PETR4");
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task UserRepository_GetByEmail_Works()
    {
        using var context = CreateContext();
        var repo = new UserRepository(context);
        var user = new User { Email = "u@u.com", Cpf = "1", Name = "U", PasswordHash = "x", Role = "A" };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var result = await repo.GetByEmailAsync("u@u.com");
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task CustodyRepository_GetByAccountAndTicker_Works()
    {
        using var context = CreateContext();
        var repo = new CustodyRepository(context);
        var custody = new ChildCustody { Ticker = "ITUB4", GraphicAccountId = 1, Quantity = 10, AveragePrice = 20 };
        context.ChildCustodies.Add(custody);
        await context.SaveChangesAsync();

        var result = await repo.GetByAccountAndTickerAsync(1, "ITUB4");
        result.Should().NotBeNull();
        result!.Quantity.Should().Be(10);
    }

    [Fact]
    public async Task ClientRepository_FilteredPending_Works()
    {
        using var context = CreateContext();
        var repo = new ClientRepository(context);
        context.Clients.Add(new Client { Name = "Alice", Cpf = "1", MonthlyValue = 500, Active = false });
        context.Clients.Add(new Client { Name = "Bob", Cpf = "2", MonthlyValue = 1500, Active = false });
        await context.SaveChangesAsync();

        var (items, total) = await repo.GetFilteredPendingAsync("Alice", 100, 1000, 1, 10);
        items.Should().HaveCount(1);
        items[0].Name.Should().Be("Alice");
    }

    [Fact]
    public async Task ClientRepository_FilteredActive_Works()
    {
        using var context = CreateContext();
        var repo = new ClientRepository(context);
        context.Clients.Add(new Client { Name = "Charlie", Cpf = "3", MonthlyValue = 2000, Active = true });
        await context.SaveChangesAsync();

        var (items, total) = await repo.GetFilteredActiveAsync(null, 1000, null, 1, 10);
        items.Should().HaveCount(1);
        items[0].Cpf.Should().Be("3");
    }

    [Fact]
    public async Task UserRepository_GetByCpf_Works()
    {
        using var context = CreateContext();
        var repo = new UserRepository(context);
        context.Users.Add(new User { Cpf = "999", Name = "X", Email = "x@x.com", PasswordHash = "x", Role = "CLIENT" });
        await context.SaveChangesAsync();
        var result = await repo.GetByCpfAsync("999");
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task CustodyRepository_GetAll_Works()
    {
        using var context = CreateContext();
        var repo = new CustodyRepository(context);
        context.ChildCustodies.Add(new ChildCustody { Ticker = "A", Quantity = 1 });
        context.MasterCustodies.Add(new MasterCustody { Ticker = "B", Quantity = 1 });
        await context.SaveChangesAsync();
        
        (await repo.GetAllChildCustodiesAsync()).Should().HaveCount(1);
        (await repo.GetAllMasterAsync()).Should().HaveCount(1);
    }

    [Fact]
    public async Task CustodyRepository_GetHistoryByClientId_Works()
    {
        using var context = CreateContext();
        var repo = new CustodyRepository(context);
        
        await repo.AddHistoryAsync(new OperationHistory { ClientId = 1, Ticker = "ITUB4", Quantity = 1, OperationType = "BUY", Reason = "TEST", UnitPrice = 10, TotalValue = 10 });
        await context.SaveChangesAsync();
        
        var hist = await repo.GetHistoryByClientIdAsync(1);
        hist.Should().HaveCount(1);
    }

    [Fact]
    public async Task CustodyRepository_HasScheduledPurchaseToday_Works()
    {
        using var context = CreateContext();
        var repo = new CustodyRepository(context);
        
        await repo.AddHistoryAsync(new OperationHistory { ClientId = 1, Ticker = "A", OperationType = "BUY", Reason = "COMPRA_PROGRAMADA", Quantity = 1, UnitPrice = 5, TotalValue = 5, OperationDate = new DateTime(2023, 10, 10) });
        await context.SaveChangesAsync();
        
        var has = await repo.HasScheduledPurchaseTodayAsync(new DateTime(2023, 10, 10));
        has.Should().BeTrue();
    }

    [Fact]
    public async Task CustodyRepository_Updates_Works()
    {
        using var context = CreateContext();
        var repo = new CustodyRepository(context);
        var custody = new ChildCustody { GraphicAccountId = 3, Ticker = "X" };
        await repo.AddAsync(custody);
        
        var master = new MasterCustody { Ticker = "Y" };
        await repo.AddMasterAsync(master);
        await context.SaveChangesAsync();
        
        // Retrieve tracked entities
        var trackedMaster = await repo.GetMasterByTickerAsync("Y");
        var trackedCustody = await repo.GetByAccountAndTickerAsync(3, "X");
        
        trackedMaster!.Quantity = 5;
        repo.UpdateMaster(trackedMaster);
        trackedCustody!.Quantity = 10;
        repo.Update(trackedCustody);
        
        await context.SaveChangesAsync();
        
        var m = await repo.GetMasterByTickerAsync("Y");
        m!.Quantity.Should().Be(5);
        var c = await repo.GetByAccountAndTickerAsync(3, "X");
        c!.Quantity.Should().Be(10);
    }

    [Fact]
    public async Task ClientRepository_GetAllActiveAsync_Works()
    {
        using var context = CreateContext();
        var repo = new ClientRepository(context);
        context.Clients.Add(new Client { Active = true, Cpf = "act1", Email = "a@a", Name = "A" });
        context.Clients.Add(new Client { Active = false, Cpf = "act2", Email = "b@b", Name = "B" });
        await context.SaveChangesAsync();
        
        var active = await repo.GetAllActiveAsync();
        active.Should().HaveCount(1);
        active[0].Cpf.Should().Be("act1");
    }
}
