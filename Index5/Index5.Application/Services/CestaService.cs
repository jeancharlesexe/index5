using Index5.Application.DTOs;
using Index5.Domain.Entities;
using Index5.Domain.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Index5.Application.Services;

public class CestaService
{
    private readonly ICestaRepository _cestaRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly RebalanceamentoService _rebalanceamentoService;
    private readonly ICotahistParser _cotahistParser;
    private readonly string _cotacoesFolder;

    public CestaService(
        ICestaRepository cestaRepo, 
        IUnitOfWork unitOfWork, 
        RebalanceamentoService rebalanceamentoService,
        ICotahistParser cotahistParser,
        Microsoft.Extensions.Configuration.IConfiguration config)
    {
        _cestaRepo = cestaRepo;
        _unitOfWork = unitOfWork;
        _rebalanceamentoService = rebalanceamentoService;
        _cotahistParser = cotahistParser;
        _cotacoesFolder = config["Cotacoes:Folder"] ?? "cotacoes";
    }

    public async Task<CestaResponse> CadastrarAsync(CestaRequest request)
    {
        if (request.Itens.Count != 5)
            throw new InvalidOperationException("QUANTIDADE_ATIVOS_INVALIDA");

        var somaPercentuais = request.Itens.Sum(i => i.Percentual);
        if (somaPercentuais != 100)
            throw new InvalidOperationException("PERCENTUAIS_INVALIDOS");

        var cestaAtual = await _cestaRepo.GetActiveAsync();
        RebalanceamentoSummary? summary = null;
        CestaAnteriorDto? anteriorDto = null;

        if (cestaAtual != null)
        {
            anteriorDto = new CestaAnteriorDto
            {
                CestaId = cestaAtual.Id,
                Nome = cestaAtual.Nome,
                DataDesativacao = DateTime.UtcNow
            };

            cestaAtual.Ativa = false;
            cestaAtual.DataDesativacao = anteriorDto.DataDesativacao;
            _cestaRepo.Update(cestaAtual);
        }

        var novaCestaEntities = request.Itens.Select(i => new ItemCesta
        {
            Ticker = i.Ticker,
            Percentual = i.Percentual
        }).ToList();

        var novaCesta = new CestaRecomendacao
        {
            Nome = request.Nome,
            Ativa = true,
            DataCriacao = DateTime.UtcNow,
            Itens = novaCestaEntities
        };

        await _cestaRepo.AddAsync(novaCesta);
        
        // Trigger Rebalancing if there was a previous basket
        if (cestaAtual != null)
        {
            summary = await _rebalanceamentoService.RebalancearTodosClientesAsync(
                novaCesta, 
                cestaAtual, 
                ticker => _cotahistParser.GetClosingQuote(_cotacoesFolder, ticker)?.PrecoFechamento ?? 0);
        }

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
            CestaAnteriorDesativada = anteriorDto,
            RebalanceamentoDisparado = summary != null,
            AtivosRemovidos = summary?.AtivosRemovidos,
            AtivosAdicionados = summary?.AtivosAdicionados,
            Mensagem = summary != null
                ? $"Cesta atualizada. Rebalanceamento disparado para {summary.ClientesAfetados} clientes ativos."
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
