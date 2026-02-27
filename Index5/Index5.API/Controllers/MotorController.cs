using Index5.Application.DTOs;
using Index5.Application.Services;
using Index5.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Index5.API.Controllers;

[ApiController]
[Route("api/motor")]
public class MotorController : ControllerBase
{
    private readonly MotorCompraService _motorService;
    private readonly ICotahistParser _cotahistParser;
    private readonly IConfiguration _configuration;

    public MotorController(
        MotorCompraService motorService,
        ICotahistParser cotahistParser,
        IConfiguration configuration)
    {
        _motorService = motorService;
        _cotahistParser = cotahistParser;
        _configuration = configuration;
    }

    [HttpPost("executar-compra")]
    public async Task<IActionResult> ExecutarCompra([FromBody] ExecutarCompraRequest request)
    {
        try
        {
            var quotesFolder = _configuration.GetValue<string>("Cotacoes:Folder") ?? "cotacoes";
            
            Func<string, decimal> getCotacao = ticker => 
            {
                var quote = _cotahistParser.GetClosingQuote(quotesFolder, ticker);
                return quote?.PrecoFechamento ?? 0;
            };

            var result = await _motorService.ExecutarCompraAsync(request.DataReferencia, getCotacao);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message == "CESTA_NAO_ENCONTRADA")
        {
            return NotFound(new ErrorResponse { Erro = "Nenhuma cesta ativa encontrada.", Codigo = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message == "NENHUM_CLIENTE_ATIVO")
        {
            return BadRequest(new ErrorResponse { Erro = "Nenhum cliente ativo encontrado.", Codigo = ex.Message });
        }
    }
}
