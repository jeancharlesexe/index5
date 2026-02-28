using Index5.Domain.Entities;
using Index5.Domain.Interfaces;
using Index5.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Index5.Infrastructure.Repositories;

public class ClientRepository : IClientRepository
{
    private readonly AppDbContext _context;

    public ClientRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Client?> GetByIdAsync(int id)
    {
        return await _context.Clients
            .Include(c => c.GraphicAccount!)
            .ThenInclude(ga => ga.Custodies)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<Client?> GetByCpfAsync(string cpf)
    {
        return await _context.Clients.FirstOrDefaultAsync(c => c.Cpf == cpf);
    }

    public async Task<List<Client>> GetAllActiveAsync()
    {
        return await _context.Clients
            .Include(c => c.GraphicAccount)
            .Where(c => c.Active)
            .ToListAsync();
    }

    public async Task<List<Client>> GetPendingAsync()
    {
        return await _context.Clients
            .Where(c => !c.Active && c.ExitDate == null && c.GraphicAccount == null)
            .ToListAsync();
    }

    public async Task AddAsync(Client client)
    {
        await _context.Clients.AddAsync(client);
    }

    public void Update(Client client)
    {
        _context.Clients.Update(client);
    }
}
