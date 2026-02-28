using Index5.Application.DTOs;
using Index5.Application.Services;
using Index5.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Index5.API.Controllers;

[ApiController]
[Route("api/v1/engine")]
[Authorize(Roles = "ADMIN")]
public class EngineController : ControllerBase
{
    private readonly PurchaseEngineService _engineService;
    private readonly ICotahistParser _cotahistParser;
    private readonly IConfiguration _configuration;

    public EngineController(
        PurchaseEngineService engineService,
        ICotahistParser cotahistParser,
        IConfiguration configuration)
    {
        _engineService = engineService;
        _cotahistParser = cotahistParser;
        _configuration = configuration;
    }

    [HttpPost("execute-purchase")]
    public async Task<IActionResult> ExecutePurchase([FromBody] ExecutePurchaseRequest request)
    {
        try
        {
            var quotesFolder = _configuration.GetValue<string>("Cotacoes:Folder") ?? "cotacoes";
            
            Func<string, decimal> getQuote = ticker => 
            {
                var quote = _cotahistParser.GetClosingQuote(quotesFolder, ticker);
                return quote?.PrecoFechamento ?? 0;
            };

            var result = await _engineService.ExecutePurchaseAsync(request.ReferenceDate, getQuote);
            return Ok(ApiResponse<ExecutePurchaseResponse>.Success(result, result.Message));
        }
        catch (InvalidOperationException ex) when (ex.Message == "BASKET_NOT_FOUND")
        {
            return NotFound(ApiResponse<object>.Error("No active basket found.", ex.Message, 404));
        }
        catch (InvalidOperationException ex) when (ex.Message == "NO_ACTIVE_CLIENTS")
        {
            return BadRequest(ApiResponse<object>.Error("No active clients found.", ex.Message));
        }
    }
}
