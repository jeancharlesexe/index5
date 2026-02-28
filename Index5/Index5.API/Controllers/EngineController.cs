using Index5.Application.DTOs;
using Index5.Application.Services;
using Index5.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Index5.API.Controllers;

[ApiController]
[Route("api/v1/engine")]
[Authorize]
public class EngineController : ControllerBase
{
    private readonly PurchaseEngineService _engineService;
    private readonly ICotahistParser _cotahistParser;
    private readonly IConfiguration _configuration;
    private readonly RebalancingService _rebalancingService;
    private readonly IBasketRepository _basketRepo;

    public EngineController(
        PurchaseEngineService engineService,
        ICotahistParser cotahistParser,
        IConfiguration configuration,
        RebalancingService rebalancingService,
        IBasketRepository basketRepo)
    {
        _engineService = engineService;
        _cotahistParser = cotahistParser;
        _configuration = configuration;
        _rebalancingService = rebalancingService;
        _basketRepo = basketRepo;
    }

    [HttpGet("status")]
    [Authorize(Roles = "ADMIN,CLIENT")]
    public IActionResult GetStatus()
    {
        var status = _engineService.GetStatus();
        return Ok(ApiResponse<EngineStatusDto>.Success(status, "Engine status retrieved successfully."));
    }

    [HttpPost("execute-purchase")]
    [Authorize(Roles = "ADMIN")]
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

    [HttpPost("execute-rebalance")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> ExecuteRebalance()
    {
        try
        {
            var activeBasket = await _basketRepo.GetActiveAsync();
            if (activeBasket == null)
            {
                return NotFound(ApiResponse<object>.Error("No active basket found to run rebalancing.", "BASKET_NOT_FOUND", 404));
            }

            var quotesFolder = _configuration.GetValue<string>("Cotacoes:Folder") ?? "cotacoes";
            
            Func<string, decimal> getQuote = ticker => 
            {
                var quote = _cotahistParser.GetClosingQuote(quotesFolder, ticker);
                return quote?.PrecoFechamento ?? 0;
            };

            var summary = await _rebalancingService.RebalanceAllClientsAsync(activeBasket, activeBasket, getQuote);

            return Ok(ApiResponse<object>.Success(new {
                summary.ClientsAffected,
                Message = "Proportional deviation rebalancing executed successfully."
            }, "Rebalancing executed successfully."));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Error(ex.Message, "REBALANCING_ERROR"));
        }
    }
}
