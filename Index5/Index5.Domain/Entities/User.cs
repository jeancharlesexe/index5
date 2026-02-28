namespace Index5.Domain.Entities;

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Cpf { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? JKey { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime BirthDate { get; set; }
    public string Role { get; set; } = "CLIENT"; // ADMIN or CLIENT
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool Active { get; set; } = true;
}
