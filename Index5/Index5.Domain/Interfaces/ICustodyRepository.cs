using Index5.Domain.Entities;

namespace Index5.Domain.Interfaces;

public interface ICustodyRepository
{
    // Client Custody
    Task<List<ChildCustody>> GetByGraphicAccountIdAsync(int graphicAccountId);
    Task<ChildCustody?> GetByAccountAndTickerAsync(int graphicAccountId, string ticker);
    Task<List<ChildCustody>> GetAllChildCustodiesAsync();
    Task AddAsync(ChildCustody custody);
    void Update(ChildCustody custody);

    // Master Custody
    Task<MasterCustody?> GetMasterByTickerAsync(string ticker);
    Task<List<MasterCustody>> GetAllMasterAsync();
    Task AddMasterAsync(MasterCustody masterCustody);
    void UpdateMaster(MasterCustody masterCustody);

    // History
    Task AddHistoryAsync(OperationHistory history);
    Task<List<OperationHistory>> GetHistoryByClientIdAsync(int clientId);
}
