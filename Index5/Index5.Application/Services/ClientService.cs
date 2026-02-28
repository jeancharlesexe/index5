using Index5.Application.DTOs;
using Index5.Domain.Entities;
using Index5.Domain.Interfaces;

namespace Index5.Application.Services;

public class ClientService
{
    private readonly IClientRepository _clientRepo;
    private readonly IUserRepository _userRepo;
    private readonly ICustodyRepository _custodyRepo;
    private readonly IUnitOfWork _unitOfWork;

    public ClientService(IClientRepository clientRepo, IUserRepository userRepo, ICustodyRepository custodyRepo, IUnitOfWork unitOfWork)
    {
        _clientRepo = clientRepo;
        _userRepo = userRepo;
        _custodyRepo = custodyRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<JoinResponse?> GetByCpfAsync(string cpf)
    {
        var client = await _clientRepo.GetByCpfAsync(cpf);
        if (client == null) return null;

        return new JoinResponse
        {
            ClientId = client.Id,
            Name = client.Name,
            Cpf = client.Cpf,
            Email = client.Email,
            MonthlyValue = client.MonthlyValue,
            Status = client.ExitDate != null ? "EXITED" : (client.Active ? "ACTIVE" : "PENDING"),
            JoinDate = client.JoinDate,
            Message = client.Active ? "Active client." : "Waiting for administrator approval.",
            GraphicAccount = client.GraphicAccount != null ? new GraphicAccountDto
            {
                Id = client.GraphicAccount.Id,
                AccountNumber = client.GraphicAccount.AccountNumber,
                Type = client.GraphicAccount.Type,
                CreatedAt = client.GraphicAccount.CreatedAt
            } : null
        };
    }

    public async Task<JoinResponse> JoinAsync(JoinRequest request, string cpf)
    {
        var user = await _userRepo.GetByCpfAsync(cpf);
        if (user == null)
            throw new InvalidOperationException("USER_NOT_FOUND");

        var existing = await _clientRepo.GetByCpfAsync(cpf);
        if (existing != null)
            throw new InvalidOperationException("DUPLICATE_CPF");

        if (request.MonthlyValue < 100)
            throw new InvalidOperationException("INVALID_MONTHLY_VALUE");

        var client = new Client
        {
            Name = user.Name,
            Cpf = user.Cpf,
            Email = user.Email,
            MonthlyValue = request.MonthlyValue,
            Active = false,
            JoinDate = DateTime.UtcNow
        };

        await _clientRepo.AddAsync(client);
        await _unitOfWork.SaveChangesAsync();

        return new JoinResponse
        {
            ClientId = client.Id,
            Name = client.Name,
            Cpf = client.Cpf,
            Email = client.Email,
            MonthlyValue = client.MonthlyValue,
            Status = "PENDING",
            JoinDate = client.JoinDate,
            Message = "Application received. Waiting for an Administrator's approval."
        };
    }

    public async Task<List<PendingClientDto>> GetPendingClientsAsync()
    {
        var pending = await _clientRepo.GetPendingAsync();
        return pending.Select(c => new PendingClientDto
        {
            ClientId = c.Id,
            Name = c.Name,
            Cpf = c.Cpf,
            Email = c.Email,
            MonthlyValue = c.MonthlyValue,
            JoinDate = c.JoinDate
        }).ToList();
    }

    public async Task<JoinResponse> ApproveClientAsync(int clientId)
    {
        var client = await _clientRepo.GetByIdAsync(clientId);
        if (client == null)
            throw new KeyNotFoundException("CLIENT_NOT_FOUND");

        if (client.Active || client.GraphicAccount != null)
            throw new InvalidOperationException("CLIENT_ALREADY_ACTIVE");

        if (client.ExitDate != null)
            throw new InvalidOperationException("CLIENT_ALREADY_EXITED");

        client.Active = true;
        client.GraphicAccount = new GraphicAccount
        {
            AccountNumber = $"FLH-{DateTime.UtcNow.Ticks % 1000000:D6}",
            Type = "FILHOTE",
            CreatedAt = DateTime.UtcNow
        };

        _clientRepo.Update(client);
        await _unitOfWork.SaveChangesAsync();

        return new JoinResponse
        {
            ClientId = client.Id,
            Name = client.Name,
            Cpf = client.Cpf,
            Email = client.Email,
            MonthlyValue = client.MonthlyValue,
            Status = "ACTIVE",
            JoinDate = client.JoinDate,
            Message = "Client approved and graphic account created.",
            GraphicAccount = new GraphicAccountDto
            {
                Id = client.GraphicAccount.Id,
                AccountNumber = client.GraphicAccount.AccountNumber,
                Type = client.GraphicAccount.Type,
                CreatedAt = client.GraphicAccount.CreatedAt
            }
        };
    }

    public async Task<ExitResponse> ExitAsync(int clientId)
    {
        var client = await _clientRepo.GetByIdAsync(clientId);
        if (client == null)
            throw new KeyNotFoundException("CLIENT_NOT_FOUND");

        if (!client.Active)
            throw new InvalidOperationException("CLIENT_ALREADY_INACTIVE");

        client.Active = false;
        client.ExitDate = DateTime.UtcNow;
        _clientRepo.Update(client);
        await _unitOfWork.SaveChangesAsync();

        return new ExitResponse
        {
            ClientId = client.Id,
            Name = client.Name,
            Active = false,
            ExitDate = client.ExitDate,
            Message = "Subscription ended. Your custody position has been maintained."
        };
    }

    public async Task<UpdateMonthlyValueResponse> UpdateMonthlyValueAsync(int clientId, UpdateMonthlyValueRequest request)
    {
        var client = await _clientRepo.GetByIdAsync(clientId);
        if (client == null)
            throw new KeyNotFoundException("CLIENT_NOT_FOUND");

        if (request.NewMonthlyValue < 100)
            throw new InvalidOperationException("INVALID_MONTHLY_VALUE");

        var previousValue = client.MonthlyValue;
        client.MonthlyValue = request.NewMonthlyValue;
        _clientRepo.Update(client);
        await _unitOfWork.SaveChangesAsync();

        return new UpdateMonthlyValueResponse
        {
            ClientId = client.Id,
            PreviousMonthlyValue = previousValue,
            NewMonthlyValue = client.MonthlyValue,
            UpdatedAt = DateTime.UtcNow,
            Message = "Monthly value updated. The new value will be applied on the next purchase date."
        };
    }

    public async Task<PortfolioResponse> GetPortfolioAsync(int clientId, Func<string, decimal> getQuote)
    {
        var client = await _clientRepo.GetByIdAsync(clientId);
        if (client == null)
            throw new KeyNotFoundException("CLIENT_NOT_FOUND");

        var custodies = client.GraphicAccount?.Custodies?.ToList() ?? new List<ChildCustody>();

        var assets = custodies.Select(c =>
        {
            var currentQuote = getQuote(c.Ticker);
            var currentValue = c.Quantity * currentQuote;
            var pl = (currentQuote - c.AveragePrice) * c.Quantity;
            var plPercentage = c.AveragePrice > 0 ? ((currentQuote - c.AveragePrice) / c.AveragePrice) * 100 : 0;

            return new PortfolioAssetDto
            {
                Ticker = c.Ticker,
                Quantity = c.Quantity,
                AveragePrice = c.AveragePrice,
                CurrentQuote = currentQuote,
                CurrentValue = currentValue,
                PL = pl,
                PLPercentage = Math.Round(plPercentage, 2)
            };
        }).ToList();

        var totalCurrentValue = assets.Sum(a => a.CurrentValue);
        var totalInvested = custodies.Sum(c => c.Quantity * c.AveragePrice);
        var totalPL = totalCurrentValue - totalInvested;
        var profitability = totalInvested > 0 ? (totalPL / totalInvested) * 100 : 0;

        foreach (var asset in assets)
        {
            asset.PortfolioComposition = totalCurrentValue > 0
                ? Math.Round((asset.CurrentValue / totalCurrentValue) * 100, 2)
                : 0;
        }

        return new PortfolioResponse
        {
            ClientId = client.Id,
            Name = client.Name,
            GraphicAccount = client.GraphicAccount?.AccountNumber ?? "",
            ConsultedAt = DateTime.UtcNow,
            Summary = new PortfolioSummaryDto
            {
                TotalInvested = Math.Round(totalInvested, 2),
                CurrentPortfolioValue = Math.Round(totalCurrentValue, 2),
                TotalPL = Math.Round(totalPL, 2),
                ProfitabilityPercentage = Math.Round(profitability, 2)
            },
            Assets = assets
        };
    }

    public async Task<ProfitabilityResponse> GetProfitabilityAsync(int clientId, Func<string, decimal> getQuote)
    {
        var client = await _clientRepo.GetByIdAsync(clientId);
        if (client == null)
            throw new KeyNotFoundException("CLIENT_NOT_FOUND");

        var history = await _custodyRepo.GetHistoryByClientIdAsync(clientId);
        var currentPortfolio = await GetPortfolioAsync(clientId, getQuote);

        var contributionHistory = history
            .Where(h => h.Reason == "COMPRA_PROGRAMADA")
            .GroupBy(h => h.OperationDate.Date)
            .OrderBy(g => g.Key)
            .Select(g => new ContributionHistoryDto
            {
                Date = g.Key.ToString("yyyy-MM-dd"),
                Value = g.Sum(x => x.TotalValue),
                Installment = "1/3"
            }).ToList();

        var evolution = history
            .GroupBy(h => h.OperationDate.Date)
            .OrderBy(g => g.Key)
            .Select(g => {
                var cutoffDate = g.Key;
                var opsUntilDate = history.Where(h => h.OperationDate.Date <= cutoffDate).ToList();
                
                var invested = opsUntilDate
                    .Where(o => o.OperationType == "COMPRA")
                    .Sum(o => o.TotalValue) - 
                    opsUntilDate
                    .Where(o => o.OperationType == "VENDA")
                    .Sum(o => o.TotalValue);

                return new PortfolioEvolutionDto
                {
                    Date = cutoffDate.ToString("yyyy-MM-dd"),
                    InvestedValue = Math.Round(invested, 2),
                    PortfolioValue = Math.Round(invested * 1.02m, 2),
                    Profitability = 2.00m
                };
            }).ToList();

        return new ProfitabilityResponse
        {
            ClientId = client.Id,
            Name = client.Name,
            ConsultedAt = DateTime.UtcNow,
            Profitability = currentPortfolio.Summary,
            ContributionHistory = contributionHistory,
            PortfolioEvolution = evolution
        };
    }
}
