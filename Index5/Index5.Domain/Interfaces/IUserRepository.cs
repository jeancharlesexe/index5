using Index5.Domain.Entities;

namespace Index5.Domain.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByCpfAsync(string cpf);
    Task<User?> GetByUsernameAsync(string username);
    Task<User?> GetByEmailAsync(string email);
    Task AddAsync(User user);
}
