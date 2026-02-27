using Index5.Domain.Entities;

namespace Index5.Domain.Interfaces;

public interface ICestaRepository
{
    Task<CestaRecomendacao?> GetActiveAsync();
    Task<List<CestaRecomendacao>> GetAllAsync();
    Task AddAsync(CestaRecomendacao cesta);
    void Update(CestaRecomendacao cesta);
}
