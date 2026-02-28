namespace Index5.Application.DTOs;

public class ExecutePurchaseRequest
{
    public string ReferenceDate { get; set; } = string.Empty;
}

public class ExecutePurchaseResponse
{
    public DateTime ExecutionDate { get; set; }
    public int TotalClients { get; set; }
    public decimal TotalConsolidated { get; set; }
    public List<PurchaseOrderDto> PurchaseOrders { get; set; } = new();
    public List<ClientDistributionDto> Distributions { get; set; } = new();
    public List<MasterResidueDto> MasterCustodyResidues { get; set; } = new();
    public int IREventsPublished { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class PurchaseOrderDto
{
    public string Ticker { get; set; } = string.Empty;
    public int TotalQuantity { get; set; }
    public List<OrderDetailDto> Details { get; set; } = new();
    public decimal UnitPrice { get; set; }
    public decimal TotalValue { get; set; }
}

public class OrderDetailDto
{
    public string Type { get; set; } = string.Empty;
    public string Ticker { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

public class ClientDistributionDto
{
    public int ClientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal ContributionValue { get; set; }
    public List<DistributedAssetDto> Assets { get; set; } = new();
}

public class DistributedAssetDto
{
    public string Ticker { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

public class MasterResidueDto
{
    public string Ticker { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

public class MasterCustodyResponse
{
    public MasterAccountDto MasterAccount { get; set; } = new();
    public List<MasterCustodyItemDto> Custody { get; set; } = new();
    public decimal TotalResidueValue { get; set; }
}

public class MasterAccountDto
{
    public int Id { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string Type { get; set; } = "MASTER";
}

public class MasterCustodyItemDto
{
    public string Ticker { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal AveragePrice { get; set; }
    public decimal CurrentValue { get; set; }
    public string Origin { get; set; } = string.Empty;
}
