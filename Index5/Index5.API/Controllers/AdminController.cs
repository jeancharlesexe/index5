using Index5.Application.DTOs;
using Index5.Application.Services;
using Index5.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Index5.API.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly CestaService _cestaService;
    private readonly ICustodiaRepository _custodiaRepo;
    private readonly ICotahistParser _cotahistParser;
    private readonly IConfiguration _configuration;

    public AdminController(
        CestaService cestaService, 
        ICustodiaRepository custodiaRepo,
        ICotahistParser cotahistParser,
        IConfiguration configuration)
    {
        _cestaService = cestaService;
        _custodiaRepo = custodiaRepo;
        _cotahistParser = cotahistParser;
        _configuration = configuration;
    }

    [HttpPost("cesta")]
    public async Task<IActionResult> CadastrarCesta([FromBody] CestaRequest request)
    {
        try
        {
            var result = await _cestaService.CadastrarAsync(request);
            return Created("/api/admin/cesta/atual", result);
        }
        catch (InvalidOperationException ex) when (ex.Message == "QUANTIDADE_ATIVOS_INVALIDA")
        {
            return BadRequest(new ErrorResponse
            {
                Erro = $"A cesta deve conter exatamente 5 ativos. Quantidade informada: {request.Itens.Count}.",
                Codigo = ex.Message
            });
        }
        catch (InvalidOperationException ex) when (ex.Message == "PERCENTUAIS_INVALIDOS")
        {
            return BadRequest(new ErrorResponse
            {
                Erro = $"A soma dos percentuais deve ser exatamente 100%. Soma atual: {request.Itens.Sum(i => i.Percentual)}%.",
                Codigo = ex.Message
            });
        }
    }

    [HttpGet("cesta/atual")]
    public async Task<IActionResult> GetCestaAtual()
    {
        var cesta = await _cestaService.GetActiveAsync();
        if (cesta == null)
            return NotFound(new ErrorResponse { Erro = "Nenhuma cesta ativa encontrada.", Codigo = "CESTA_NAO_ENCONTRADA" });

        // Add real quotes to items
        var quotesFolder = _configuration.GetValue<string>("Cotacoes:Folder") ?? "cotacoes";
        foreach (var item in cesta.Itens)
        {
            var quote = _cotahistParser.GetClosingQuote(quotesFolder, item.Ticker);
            item.CotacaoAtual = quote?.PrecoFechamento;
        }

        return Ok(cesta);
    }

    [HttpGet("cesta/historico")]
    public async Task<IActionResult> GetHistorico()
    {
        var result = await _cestaService.GetHistoricoAsync();
        return Ok(result);
    }

    [HttpGet("conta-master/custodia")]
    public async Task<IActionResult> GetCustodiaMaster()
    {
        var custodias = await _custodiaRepo.GetAllMasterAsync();
        var quotesFolder = _configuration.GetValue<string>("Cotacoes:Folder") ?? "cotacoes";

        var custodiaDtos = custodias.Select(c =>
        {
            var quote = _cotahistParser.GetClosingQuote(quotesFolder, c.Ticker);
            var valorAtual = quote?.PrecoFechamento ?? 0;

            return new CustodiaMasterItemDto
            {
                Ticker = c.Ticker,
                Quantidade = c.Quantidade,
                PrecoMedio = c.PrecoMedio,
                ValorAtual = valorAtual,
                Origem = c.Origem ?? ""
            };
        }).ToList();

        var response = new CustodiaMasterResponse
        {
            ContaMaster = new ContaMasterDto
            {
                Id = 1,
                NumeroConta = "MST-000001",
                Tipo = "MASTER"
            },
            Custodia = custodiaDtos,
            ValorTotalResiduo = custodiaDtos.Sum(c => c.Quantidade * c.ValorAtual)
        };

        return Ok(response);
    }
}
