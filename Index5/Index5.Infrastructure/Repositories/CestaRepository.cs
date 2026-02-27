using Index5.Domain.Entities;
using Index5.Domain.Interfaces;
using Index5.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Index5.Infrastructure.Repositories;

public class CestaRepository : ICestaRepository
{
    private readonly AppDbContext _context;

    public CestaRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<CestaRecomendacao?> GetActiveAsync()
    {
        return await _context.CestasRecomendacao
            .Include(c => c.Itens)
            .FirstOrDefaultAsync(c => c.Ativa);
    }

    public async Task<List<CestaRecomendacao>> GetAllAsync()
    {
        return await _context.CestasRecomendacao
            .Include(c => c.Itens)
            .OrderByDescending(c => c.DataCriacao)
            .ToListAsync();
    }

    public async Task AddAsync(CestaRecomendacao cesta)
    {
        await _context.CestasRecomendacao.AddAsync(cesta);
    }

    public void Update(CestaRecomendacao cesta)
    {
        _context.CestasRecomendacao.Update(cesta);
    }
}
