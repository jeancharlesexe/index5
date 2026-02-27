using Index5.Domain.Entities;

namespace Index5.Domain.Interfaces;

public interface IClienteRepository
{
    Task<Cliente?> GetByIdAsync(int id);
    Task<Cliente?> GetByCpfAsync(string cpf);
    Task<Cliente?> GetByEmailAsync(string email);
    Task<List<Cliente>> GetAllActivesAsync();
    Task AddAsync(Cliente cliente);
    void Update(Cliente cliente);
}
