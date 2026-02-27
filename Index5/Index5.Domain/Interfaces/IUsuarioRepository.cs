using Index5.Domain.Entities;

namespace Index5.Domain.Interfaces;

public interface IUsuarioRepository
{
    Task<Usuario?> GetByUsernameAsync(string username);
    Task<Usuario?> GetByIdAsync(int id);
    Task<bool> ExistsAsync(string username);
    Task AddAsync(Usuario usuario);
    void Update(Usuario usuario);
}
