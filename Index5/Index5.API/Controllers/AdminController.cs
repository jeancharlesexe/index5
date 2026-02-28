using Index5.Application.DTOs;
using Index5.Application.Services;
using Index5.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Index5.API.Controllers;

[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = "ADMIN")]
public class AdminController : ControllerBase
{
    private readonly BasketService _basketService;
    private readonly ClientService _clientService;
    private readonly ICustodyRepository _custodyRepo;
    private readonly ICotahistParser _cotahistParser;
    private readonly IConfiguration _configuration;

    public AdminController(
        BasketService basketService, 
        ClientService clientService,
        ICustodyRepository custodyRepo,
        ICotahistParser cotahistParser,
        IConfiguration configuration)
    {
        _basketService = basketService;
        _clientService = clientService;
        _custodyRepo = custodyRepo;
        _cotahistParser = cotahistParser;
        _configuration = configuration;
    }

    [HttpGet("clients/pending")]
    public async Task<IActionResult> GetPendingClients()
    {
        var result = await _clientService.GetPendingClientsAsync();
        return Ok(ApiResponse<List<PendingClientDto>>.Success(result, "Pending clients retrieved successfully."));
    }

    [HttpPost("clients/{clientId}/approve")]
    public async Task<IActionResult> ApproveClient(int clientId)
    {
        try
        {
            var result = await _clientService.ApproveClientAsync(clientId);
            return Ok(ApiResponse<JoinResponse>.Success(result, "Client approved successfully."));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<object>.Error("Client not found.", "CLIENT_NOT_FOUND", 404));
        }
        catch (InvalidOperationException ex) when (ex.Message == "CLIENT_ALREADY_ACTIVE")
        {
            return BadRequest(ApiResponse<object>.Error("Client is already active/approved.", ex.Message));
        }
        catch (InvalidOperationException ex) when (ex.Message == "CLIENT_ALREADY_EXITED")
        {
            return BadRequest(ApiResponse<object>.Error("Client has already exited.", ex.Message));
        }
    }

    [HttpPost("basket")]
    public async Task<IActionResult> CreateBasket([FromBody] BasketRequest request)
    {
        try
        {
            var result = await _basketService.CreateAsync(request);
            return StatusCode(201, ApiResponse<BasketResponse>.Created(result, result.Message));
        }
        catch (InvalidOperationException ex) when (ex.Message == "INVALID_ASSET_COUNT")
        {
            return BadRequest(ApiResponse<object>.Error(
                $"Basket must contain exactly 5 assets. Count provided: {request.Items.Count}.",
                ex.Message));
        }
        catch (InvalidOperationException ex) when (ex.Message == "INVALID_PERCENTAGES")
        {
            return BadRequest(ApiResponse<object>.Error(
                $"Percentages must sum to exactly 100%. Current sum: {request.Items.Sum(i => i.Percentage)}%.",
                ex.Message));
        }
    }

    [HttpGet("basket/current")]
    public async Task<IActionResult> GetCurrentBasket()
    {
        var basket = await _basketService.GetActiveAsync();
        if (basket == null)
            return NotFound(ApiResponse<object>.Error("No active basket found.", "BASKET_NOT_FOUND", 404));

        var quotesFolder = _configuration.GetValue<string>("Cotacoes:Folder") ?? "cotacoes";
        foreach (var item in basket.Items)
        {
            var quote = _cotahistParser.GetClosingQuote(quotesFolder, item.Ticker);
            item.CurrentQuote = quote?.PrecoFechamento;
        }

        return Ok(ApiResponse<BasketResponse>.Success(basket, "Current basket retrieved successfully."));
    }

    [HttpGet("basket/history")]
    public async Task<IActionResult> GetBasketHistory()
    {
        var result = await _basketService.GetHistoryAsync();
        return Ok(ApiResponse<BasketHistoryResponse>.Success(result, "Basket history retrieved successfully."));
    }

    [HttpGet("master-account/custody")]
    public async Task<IActionResult> GetMasterCustody()
    {
        var custodias = await _custodyRepo.GetAllMasterAsync();
        var quotesFolder = _configuration.GetValue<string>("Cotacoes:Folder") ?? "cotacoes";

        var custodyItems = custodias.Select(c =>
        {
            var quote = _cotahistParser.GetClosingQuote(quotesFolder, c.Ticker);
            var currentValue = quote?.PrecoFechamento ?? 0;

            return new MasterCustodyItemDto
            {
                Ticker = c.Ticker,
                Quantity = c.Quantity,
                AveragePrice = c.AveragePrice,
                CurrentValue = currentValue,
                Origin = c.Origin ?? ""
            };
        }).ToList();

        var response = new MasterCustodyResponse
        {
            MasterAccount = new MasterAccountDto
            {
                Id = 1,
                AccountNumber = "MST-000001",
                Type = "MASTER"
            },
            Custody = custodyItems,
            TotalResidueValue = custodyItems.Sum(c => c.Quantity * c.CurrentValue)
        };

        return Ok(ApiResponse<MasterCustodyResponse>.Success(response, "Master custody retrieved successfully."));
    }
}
