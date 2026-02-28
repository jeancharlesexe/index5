using Index5.Domain.Entities;

namespace Index5.Domain.Interfaces;

public interface IClientRepository
{
    Task<Client?> GetByIdAsync(int id);
    Task<Client?> GetByCpfAsync(string cpf);
    Task<List<Client>> GetAllActiveAsync();
    Task<List<Client>> GetPendingAsync();
    Task AddAsync(Client client);
    void Update(Client client);
}
