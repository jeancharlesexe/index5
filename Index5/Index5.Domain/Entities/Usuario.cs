namespace Index5.Domain.Entities;

public class Usuario
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty; // "Admin" ou "Cliente"
    
    // Vinculo opcional com a entidade Cliente (se for um usuario do tipo Cliente)
    public int? ClienteId { get; set; }
    public Cliente? Cliente { get; set; }
}
