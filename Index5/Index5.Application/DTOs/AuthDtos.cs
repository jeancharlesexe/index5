using System.ComponentModel.DataAnnotations;

namespace Index5.Application.DTOs;

// ==================== REGISTER ====================

public class RegisterRequest
{
    [Required(ErrorMessage = "Name is required")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "CPF is required")]
    [StringLength(11, MinimumLength = 11, ErrorMessage = "CPF must be 11 characters")]
    public string Cpf { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string Email { get; set; } = string.Empty;

    public string? JKey { get; set; } // Required only for ADMIN, checked in Service

    [Required(ErrorMessage = "Password is required")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Birth date is required")]
    public DateTime BirthDate { get; set; }

    [Required(ErrorMessage = "Role is required (ADMIN or CLIENT)")]
    public string Role { get; set; } = "CLIENT"; 
}

public class RegisterResponse
{
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Cpf { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? JKey { get; set; }
    public string Role { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

// ==================== LOGIN ====================

public class LoginClientRequest
{
    [Required(ErrorMessage = "CPF is required")]
    public string Cpf { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    public string Password { get; set; } = string.Empty;
}

public class LoginAdminRequest
{
    [Required(ErrorMessage = "JKey is required")]
    public string JKey { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public DateTime Expiration { get; set; }
}
