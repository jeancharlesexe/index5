namespace Index5.Application.DTOs;

public class PortfolioResponse
{
    public int ClientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string GraphicAccount { get; set; } = string.Empty;
    public DateTime ConsultedAt { get; set; }
    public PortfolioSummaryDto Summary { get; set; } = new();
    public List<PortfolioAssetDto> Assets { get; set; } = new();
}

public class PortfolioSummaryDto
{
    public decimal TotalInvested { get; set; }
    public decimal CurrentPortfolioValue { get; set; }
    public decimal TotalPL { get; set; }
    public decimal ProfitabilityPercentage { get; set; }
}

public class PortfolioAssetDto
{
    public string Ticker { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal AveragePrice { get; set; }
    public decimal CurrentQuote { get; set; }
    public decimal CurrentValue { get; set; }
    public decimal PL { get; set; }
    public decimal PLPercentage { get; set; }
    public decimal PortfolioComposition { get; set; }
}

public class ProfitabilityResponse
{
    public int ClientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime ConsultedAt { get; set; }
    public PortfolioSummaryDto Profitability { get; set; } = new();
    public List<ContributionHistoryDto> ContributionHistory { get; set; } = new();
    public List<PortfolioEvolutionDto> PortfolioEvolution { get; set; } = new();
}

public class ContributionHistoryDto
{
    public string Date { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public string Installment { get; set; } = string.Empty;
}

public class PortfolioEvolutionDto
{
    public string Date { get; set; } = string.Empty;
    public decimal PortfolioValue { get; set; }
    public decimal InvestedValue { get; set; }
    public decimal Profitability { get; set; }
}
