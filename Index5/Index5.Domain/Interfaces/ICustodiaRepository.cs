using Index5.Domain.Entities;

namespace Index5.Domain.Interfaces;

public interface ICustodiaRepository
{
    Task<List<CustodiaFilhote>> GetByContaGraficaIdAsync(int contaGraficaId);
    Task<CustodiaFilhote?> GetByContaAndTickerAsync(int contaGraficaId, string ticker);
    Task AddAsync(CustodiaFilhote custodia);
    void Update(CustodiaFilhote custodia);

    Task<List<CustodiaMaster>> GetAllMasterAsync();
    Task<CustodiaMaster?> GetMasterByTickerAsync(string ticker);
    Task AddMasterAsync(CustodiaMaster custodia);
    void UpdateMaster(CustodiaMaster custodia);
}
