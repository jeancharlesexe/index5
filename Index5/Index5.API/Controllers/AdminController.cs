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
            return Created("/api/v1/admin/cesta/atual", result);
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

        return Ok(cesta);
    }

    [HttpGet("cesta/historico")]
    public async Task<IActionResult> GetHistorico()
    {
        var result = await _cestaService.GetHistoricoAsync();
        return Ok(result);
    }
}
