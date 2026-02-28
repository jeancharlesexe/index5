using Index5.Application.DTOs;
using Index5.Application.Services;
using Index5.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Index5.API.Controllers;

[ApiController]
[Route("api/v1/clients")]
[Authorize(Roles = "CLIENT")]
public class ClientController : ControllerBase
{
    private readonly ClientService _clientService;
    private readonly ICotahistParser _cotahistParser;
    private readonly IConfiguration _configuration;

    public ClientController(
        ClientService clientService,
        ICotahistParser cotahistParser,
        IConfiguration configuration)
    {
        _clientService = clientService;
        _cotahistParser = cotahistParser;
        _configuration = configuration;
    }

    [HttpPost("join")]
    public async Task<IActionResult> Join([FromBody] JoinRequest request)
    {
        try
        {
            var cpf = User.FindFirst("cpf")?.Value;
            if (string.IsNullOrEmpty(cpf))
                return Unauthorized(ApiResponse<object>.Error("User CPF not found in token.", "UNAUTHORIZED", 401));

            var result = await _clientService.JoinAsync(request, cpf);
            return StatusCode(201, ApiResponse<JoinResponse>.Created(result, "Client registered successfully."));
        }
        catch (InvalidOperationException ex) when (ex.Message == "USER_NOT_FOUND")
        {
            return NotFound(ApiResponse<object>.Error("User not found.", ex.Message, 404));
        }
        catch (InvalidOperationException ex) when (ex.Message == "DUPLICATE_CPF")
        {
            return BadRequest(ApiResponse<object>.Error("CPF already registered in the system.", ex.Message));
        }
        catch (InvalidOperationException ex) when (ex.Message == "INVALID_MONTHLY_VALUE")
        {
            return BadRequest(ApiResponse<object>.Error("Minimum monthly value is R$ 100.00.", ex.Message));
        }
    }

    [HttpPost("{clientId}/exit")]
    public async Task<IActionResult> Exit(int clientId)
    {
        try
        {
            var result = await _clientService.ExitAsync(clientId);
            return Ok(ApiResponse<ExitResponse>.Success(result, "Exit completed successfully."));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<object>.Error("Client not found.", "CLIENT_NOT_FOUND", 404));
        }
        catch (InvalidOperationException ex) when (ex.Message == "CLIENT_ALREADY_INACTIVE")
        {
            return BadRequest(ApiResponse<object>.Error("Client has already exited.", ex.Message));
        }
    }

    [HttpPut("{clientId}/monthly-value")]
    public async Task<IActionResult> UpdateMonthlyValue(int clientId, [FromBody] UpdateMonthlyValueRequest request)
    {
        try
        {
            var result = await _clientService.UpdateMonthlyValueAsync(clientId, request);
            return Ok(ApiResponse<UpdateMonthlyValueResponse>.Success(result, "Monthly value updated successfully."));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<object>.Error("Client not found.", "CLIENT_NOT_FOUND", 404));
        }
        catch (InvalidOperationException ex) when (ex.Message == "INVALID_MONTHLY_VALUE")
        {
            return BadRequest(ApiResponse<object>.Error("Minimum monthly value is R$ 100.00.", ex.Message));
        }
    }

    [HttpGet("{clientId}/portfolio")]
    public async Task<IActionResult> GetPortfolio(int clientId)
    {
        try
        {
            var quotesFolder = _configuration.GetValue<string>("Cotacoes:Folder") ?? "cotacoes";
            var result = await _clientService.GetPortfolioAsync(clientId, ticker => 
            {
                var quote = _cotahistParser.GetClosingQuote(quotesFolder, ticker);
                return quote?.PrecoFechamento ?? 0;
            });
            return Ok(ApiResponse<PortfolioResponse>.Success(result, "Portfolio retrieved successfully."));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<object>.Error("Client not found.", "CLIENT_NOT_FOUND", 404));
        }
    }

    [HttpGet("{clientId}/profitability")]
    public async Task<IActionResult> GetProfitability(int clientId)
    {
        try
        {
            var quotesFolder = _configuration.GetValue<string>("Cotacoes:Folder") ?? "cotacoes";
            var result = await _clientService.GetProfitabilityAsync(clientId, ticker =>
                _cotahistParser.GetClosingQuote(quotesFolder, ticker)?.PrecoFechamento ?? 0);

            return Ok(ApiResponse<ProfitabilityResponse>.Success(result, "Profitability retrieved successfully."));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<object>.Error("Client not found.", "CLIENT_NOT_FOUND", 404));
        }
    }
}
