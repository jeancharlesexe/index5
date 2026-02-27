using Index5.Application.DTOs;
using Index5.Domain.Entities;
using Index5.Domain.Interfaces;

namespace Index5.Application.Services;

public class CestaService
{
    private readonly ICestaRepository _cestaRepo;
    private readonly IUnitOfWork _unitOfWork;

    public CestaService(ICestaRepository cestaRepo, IUnitOfWork unitOfWork)
    {
        _cestaRepo = cestaRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<CestaResponse> CadastrarAsync(CestaRequest request)
    {
        if (request.Itens.Count != 5)
            throw new InvalidOperationException("QUANTIDADE_ATIVOS_INVALIDA");

        var somaPercentuais = request.Itens.Sum(i => i.Percentual);
        if (somaPercentuais != 100)
            throw new InvalidOperationException("PERCENTUAIS_INVALIDOS");

        var cestaAtual = await _cestaRepo.GetActiveAsync();
        bool hasRebalancing = false;

        if (cestaAtual != null)
        {
            cestaAtual.Ativa = false;
            cestaAtual.DataDesativacao = DateTime.UtcNow;
            _cestaRepo.Update(cestaAtual);
            hasRebalancing = true;
        }

        var novaCesta = new CestaRecomendacao
        {
            Nome = request.Nome,
            Ativa = true,
            DataCriacao = DateTime.UtcNow,
            Itens = request.Itens.Select(i => new ItemCesta
            {
                Ticker = i.Ticker,
                Percentual = i.Percentual
            }).ToList()
        };

        await _cestaRepo.AddAsync(novaCesta);
        await _unitOfWork.SaveChangesAsync();

        return new CestaResponse
        {
            CestaId = novaCesta.Id,
            Nome = novaCesta.Nome,
            Ativa = true,
            DataCriacao = novaCesta.DataCriacao,
            Itens = novaCesta.Itens.Select(i => new ItemCestaDto
            {
                Ticker = i.Ticker,
                Percentual = i.Percentual
            }).ToList(),
            RebalanceamentoDisparado = hasRebalancing,
            Mensagem = hasRebalancing
                ? "Cesta atualizada. Rebalanceamento disparado."
                : "Primeira cesta cadastrada com sucesso."
        };
    }

    public async Task<CestaResponse?> GetActiveAsync()
    {
        var cesta = await _cestaRepo.GetActiveAsync();
        if (cesta == null) return null;

        return new CestaResponse
        {
            CestaId = cesta.Id,
            Nome = cesta.Nome,
            Ativa = cesta.Ativa,
            DataCriacao = cesta.DataCriacao,
            Itens = cesta.Itens.Select(i => new ItemCestaDto
            {
                Ticker = i.Ticker,
                Percentual = i.Percentual
            }).ToList()
        };
    }

    public async Task<HistoricoCestasResponse> GetHistoricoAsync()
    {
        var cestas = await _cestaRepo.GetAllAsync();

        return new HistoricoCestasResponse
        {
            Cestas = cestas.Select(c => new CestaHistoricoDto
            {
                CestaId = c.Id,
                Nome = c.Nome,
                Ativa = c.Ativa,
                DataCriacao = c.DataCriacao,
                DataDesativacao = c.DataDesativacao,
                Itens = c.Itens.Select(i => new ItemCestaDto
                {
                    Ticker = i.Ticker,
                    Percentual = i.Percentual
                }).ToList()
            }).ToList()
        };
    }
}
