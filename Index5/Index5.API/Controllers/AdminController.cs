using Index5.Application.DTOs;
using Index5.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Index5.API.Controllers;

[ApiController]
[Route("api/v1/admin")]
public class AdminController : ControllerBase
{
    private readonly CestaService _cestaService;

    public AdminController(CestaService cestaService)
    {
        _cestaService = cestaService;
    }

    [HttpPost("cesta")]
    public async Task<IActionResult> CadastrarCesta([FromBody] CestaRequest request)
    {
        try
        {
            var result = await _cestaService.CadastrarAsync(request);
            return StatusCode(201, ApiResponse<CestaResponse>.Created(result, "Cesta cadastrada com sucesso."));
        }
        catch (InvalidOperationException ex) when (ex.Message == "QUANTIDADE_ATIVOS_INVALIDA")
        {
            return BadRequest(ApiResponse<object>.Error(
                $"A cesta deve conter exatamente 5 ativos. Quantidade informada: {request.Itens.Count}.",
                ex.Message));
        }
        catch (InvalidOperationException ex) when (ex.Message == "PERCENTUAIS_INVALIDOS")
        {
            return BadRequest(ApiResponse<object>.Error(
                $"A soma dos percentuais deve ser exatamente 100%. Soma atual: {request.Itens.Sum(i => i.Percentual)}%.",
                ex.Message));
        }
    }

    [HttpGet("cesta/atual")]
    public async Task<IActionResult> GetCestaAtual()
    {
        var cesta = await _cestaService.GetActiveAsync();
        if (cesta == null)
            return NotFound(ApiResponse<object>.Error("Nenhuma cesta ativa encontrada.", "CESTA_NAO_ENCONTRADA", 404));

        return Ok(ApiResponse<CestaResponse>.Success(cesta, "Cesta ativa encontrada."));
    }

    [HttpGet("cesta/historico")]
    public async Task<IActionResult> GetHistorico()
    {
        var result = await _cestaService.GetHistoricoAsync();
        return Ok(ApiResponse<HistoricoCestasResponse>.Success(result, "Historico de cestas."));
    }
}
