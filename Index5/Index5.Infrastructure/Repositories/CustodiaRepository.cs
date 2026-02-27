using Index5.Domain.Entities;
using Index5.Domain.Interfaces;
using Index5.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Index5.Infrastructure.Repositories;

public class CustodiaRepository : ICustodiaRepository
{
    private readonly AppDbContext _context;

    public CustodiaRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<CustodiaFilhote>> GetByContaGraficaIdAsync(int contaGraficaId)
    {
        return await _context.CustodiasFilhote
            .Where(c => c.ContaGraficaId == contaGraficaId)
            .ToListAsync();
    }

    public async Task<CustodiaFilhote?> GetByContaAndTickerAsync(int contaGraficaId, string ticker)
    {
        return await _context.CustodiasFilhote
            .FirstOrDefaultAsync(c => c.ContaGraficaId == contaGraficaId && c.Ticker == ticker);
    }

    public async Task AddAsync(CustodiaFilhote custodia)
    {
        await _context.CustodiasFilhote.AddAsync(custodia);
    }

    public void Update(CustodiaFilhote custodia)
    {
        _context.CustodiasFilhote.Update(custodia);
    }

    public async Task<List<CustodiaMaster>> GetAllMasterAsync()
    {
        return await _context.CustodiaMaster
            .Where(c => c.Quantidade > 0)
            .ToListAsync();
    }

    public async Task<CustodiaMaster?> GetMasterByTickerAsync(string ticker)
    {
        return await _context.CustodiaMaster
            .FirstOrDefaultAsync(c => c.Ticker == ticker);
    }

    public async Task AddMasterAsync(CustodiaMaster custodia)
    {
        await _context.CustodiaMaster.AddAsync(custodia);
    }

    public void UpdateMaster(CustodiaMaster custodia)
    {
        _context.CustodiaMaster.Update(custodia);
    }

    public async Task AddHistoricoAsync(OperacaoHistorico operacao)
    {
        await _context.HistoricoOperacoes.AddAsync(operacao);
    }

    public async Task<List<OperacaoHistorico>> GetHistoricoByClienteIdAsync(int clienteId)
    {
        return await _context.HistoricoOperacoes
            .Where(h => h.ClienteId == clienteId)
            .OrderBy(h => h.DataOperacao)
            .ToListAsync();
    }
}
