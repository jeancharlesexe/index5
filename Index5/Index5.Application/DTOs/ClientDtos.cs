using System.ComponentModel.DataAnnotations;

namespace Index5.Application.DTOs;

public class JoinRequest
{
    [Required]
    [Range(100, double.MaxValue, ErrorMessage = "Minimum monthly value is R$ 100.00.")]
    public decimal MonthlyValue { get; set; }
}

public class JoinResponse
{
    public int ClientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Cpf { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public decimal MonthlyValue { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime JoinDate { get; set; }
    public string Message { get; set; } = string.Empty;
    public GraphicAccountDto? GraphicAccount { get; set; }
}

public class PendingClientDto
{
    public int ClientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Cpf { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public decimal MonthlyValue { get; set; }
    public DateTime JoinDate { get; set; }
}

public class ActiveClientDto
{
    public int ClientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Cpf { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public decimal MonthlyValue { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public DateTime JoinDate { get; set; }
}

public class GraphicAccountDto
{
    public int Id { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class ExitResponse
{
    public int ClientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Active { get; set; }
    public DateTime? ExitDate { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class UpdateMonthlyValueRequest
{
    public decimal NewMonthlyValue { get; set; }
}

public class UpdateMonthlyValueResponse
{
    public int ClientId { get; set; }
    public decimal PreviousMonthlyValue { get; set; }
    public decimal NewMonthlyValue { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class ErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}
