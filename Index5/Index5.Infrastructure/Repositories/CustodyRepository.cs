using Index5.Domain.Entities;
using Index5.Domain.Interfaces;
using Index5.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Index5.Infrastructure.Repositories;

public class CustodyRepository : ICustodyRepository
{
    private readonly AppDbContext _context;

    public CustodyRepository(AppDbContext context)
    {
        _context = context;
    }

    // Client Custody
    public async Task<List<ChildCustody>> GetByGraphicAccountIdAsync(int graphicAccountId)
    {
        return await _context.ChildCustodies
            .Where(c => c.GraphicAccountId == graphicAccountId)
            .ToListAsync();
    }

    public async Task<ChildCustody?> GetByAccountAndTickerAsync(int graphicAccountId, string ticker)
    {
        return await _context.ChildCustodies
            .FirstOrDefaultAsync(c => c.GraphicAccountId == graphicAccountId && c.Ticker == ticker);
    }

    public async Task AddAsync(ChildCustody custody)
    {
        await _context.ChildCustodies.AddAsync(custody);
    }

    public void Update(ChildCustody custody)
    {
        _context.ChildCustodies.Update(custody);
    }

    // Master Custody
    public async Task<MasterCustody?> GetMasterByTickerAsync(string ticker)
    {
        return await _context.MasterCustodies.FirstOrDefaultAsync(c => c.Ticker == ticker);
    }

    public async Task<List<MasterCustody>> GetAllMasterAsync()
    {
        return await _context.MasterCustodies.ToListAsync();
    }

    public async Task AddMasterAsync(MasterCustody masterCustody)
    {
        await _context.MasterCustodies.AddAsync(masterCustody);
    }

    public void UpdateMaster(MasterCustody masterCustody)
    {
        _context.MasterCustodies.Update(masterCustody);
    }

    // History
    public async Task AddHistoryAsync(OperationHistory history)
    {
        await _context.OperationHistory.AddAsync(history);
    }

    public async Task<List<OperationHistory>> GetHistoryByClientIdAsync(int clientId)
    {
        return await _context.OperationHistory
            .Where(h => h.ClientId == clientId)
            .OrderByDescending(h => h.OperationDate)
            .ToListAsync();
    }
}
