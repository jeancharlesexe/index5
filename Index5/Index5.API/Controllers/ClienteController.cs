using Index5.Application.DTOs;
using Index5.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Index5.API.Controllers;

[ApiController]
[Route("api/v1/clientes")]
public class ClienteController : ControllerBase
{
    private readonly ClienteService _clienteService;

    public ClienteController(ClienteService clienteService)
    {
        _clienteService = clienteService;
    }

    [HttpPost("adesao")]
    public async Task<IActionResult> Aderir([FromBody] AdesaoRequest request)
    {
        try
        {
            var result = await _clienteService.AderirAsync(request);
            return StatusCode(201, ApiResponse<AdesaoResponse>.Created(result, "Cliente cadastrado com sucesso."));
        }
        catch (InvalidOperationException ex) when (ex.Message == "CLIENTE_CPF_DUPLICADO")
        {
            return BadRequest(ApiResponse<object>.Error("CPF ja cadastrado no sistema.", ex.Message));
        }
        catch (InvalidOperationException ex) when (ex.Message == "VALOR_MENSAL_INVALIDO")
        {
            return BadRequest(ApiResponse<object>.Error("O valor mensal minimo e de R$ 100,00.", ex.Message));
        }
    }

    [HttpPost("{clienteId}/saida")]
    public async Task<IActionResult> Sair(int clienteId)
    {
        try
        {
            var result = await _clienteService.SairAsync(clienteId);
            return Ok(ApiResponse<SaidaResponse>.Success(result, "Adesao encerrada com sucesso."));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<object>.Error("Cliente nao encontrado.", "CLIENTE_NAO_ENCONTRADO", 404));
        }
        catch (InvalidOperationException ex) when (ex.Message == "CLIENTE_JA_INATIVO")
        {
            return BadRequest(ApiResponse<object>.Error("Cliente ja havia saido do produto.", ex.Message));
        }
    }

    [HttpPut("{clienteId}/valor-mensal")]
    public async Task<IActionResult> AlterarValorMensal(int clienteId, [FromBody] AlterarValorRequest request)
    {
        try
        {
            var result = await _clienteService.AlterarValorMensalAsync(clienteId, request);
            return Ok(ApiResponse<AlterarValorResponse>.Success(result, "Valor mensal atualizado com sucesso."));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<object>.Error("Cliente nao encontrado.", "CLIENTE_NAO_ENCONTRADO", 404));
        }
        catch (InvalidOperationException ex) when (ex.Message == "VALOR_MENSAL_INVALIDO")
        {
            return BadRequest(ApiResponse<object>.Error("O valor mensal minimo e de R$ 100,00.", ex.Message));
        }
    }

    [HttpGet("{clienteId}/carteira")]
    public async Task<IActionResult> ConsultarCarteira(int clienteId)
    {
        try
        {
            var result = await _clienteService.ConsultarCarteiraAsync(clienteId, ticker => 0m);
            return Ok(ApiResponse<CarteiraResponse>.Success(result, "Carteira consultada com sucesso."));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<object>.Error("Cliente nao encontrado.", "CLIENTE_NAO_ENCONTRADO", 404));
        }
    }
}
