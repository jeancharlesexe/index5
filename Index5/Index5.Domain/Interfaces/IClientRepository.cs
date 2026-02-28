using Index5.Domain.Entities;

namespace Index5.Domain.Interfaces;

public interface IClientRepository
{
    Task<Client?> GetByIdAsync(int id);
    Task<Client?> GetByCpfAsync(string cpf);
    Task<List<Client>> GetAllActiveAsync();
    Task<List<Client>> GetPendingAsync();
    
    Task<(List<Client> Items, int TotalCount)> GetFilteredPendingAsync(string? name, decimal? minValue, decimal? maxValue, int page, int pageSize);
    Task<(List<Client> Items, int TotalCount)> GetFilteredActiveAsync(string? name, decimal? minValue, decimal? maxValue, int page, int pageSize);

    Task AddAsync(Client client);
    void Update(Client client);
    void Remove(Client client);
}
