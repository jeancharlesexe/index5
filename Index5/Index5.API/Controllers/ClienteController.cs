using Index5.Application.DTOs;
using Index5.Application.Services;
using Index5.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Index5.API.Controllers;

[Authorize]
[ApiController]
[Route("api/clientes")]
public class ClienteController : ControllerBase
{
    private readonly ClienteService _clienteService;
    private readonly ICotahistParser _cotahistParser;
    private readonly IConfiguration _configuration;

    public ClienteController(
        ClienteService clienteService,
        ICotahistParser cotahistParser,
        IConfiguration configuration)
    {
        _clienteService = clienteService;
        _cotahistParser = cotahistParser;
        _configuration = configuration;
    }

    [AllowAnonymous]
    [HttpPost("adesao")]
    public async Task<IActionResult> Aderir([FromBody] AdesaoRequest request)
    {
        try
        {
            int? usuarioId = null;
            var usuarioIdClaim = User.FindFirst("UsuarioId")?.Value;
            if (int.TryParse(usuarioIdClaim, out var id))
                usuarioId = id;

            var result = await _clienteService.AderirAsync(request, usuarioId);
            return Created($"/api/clientes/{result.ClienteId}/carteira", result);
        }
        catch (InvalidOperationException ex) when (ex.Message == "CLIENTE_CPF_DUPLICADO")
        {
            return BadRequest(new ErrorResponse { Erro = "CPF ja cadastrado no sistema.", Codigo = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message == "CLIENTE_EMAIL_DUPLICADO")
        {
            return BadRequest(new ErrorResponse { Erro = "E-mail ja cadastrado no sistema.", Codigo = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message == "VALOR_MENSAL_INVALIDO")
        {
            return BadRequest(new ErrorResponse { Erro = "O valor mensal minimo e de R$ 100,00.", Codigo = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message == "USUARIO_JA_TEM_ADESAO")
        {
            return BadRequest(new ErrorResponse { Erro = "Este usuario ja possui uma adesao ativa ao produto.", Codigo = ex.Message });
        }
    }

    [HttpPost("{clienteId}/saida")]
    public async Task<IActionResult> Sair(int clienteId)
    {
        try
        {
            var result = await _clienteService.SairAsync(clienteId);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ErrorResponse { Erro = "Cliente nao encontrado.", Codigo = "CLIENTE_NAO_ENCONTRADO" });
        }
        catch (InvalidOperationException ex) when (ex.Message == "CLIENTE_JA_INATIVO")
        {
            return BadRequest(new ErrorResponse { Erro = "Cliente ja havia saido do produto.", Codigo = ex.Message });
        }
    }

    [HttpPut("{clienteId}/valor-mensal")]
    public async Task<IActionResult> AlterarValorMensal(int clienteId, [FromBody] AlterarValorRequest request)
    {
        try
        {
            var result = await _clienteService.AlterarValorMensalAsync(clienteId, request);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ErrorResponse { Erro = "Cliente nao encontrado.", Codigo = "CLIENTE_NAO_ENCONTRADO" });
        }
        catch (InvalidOperationException ex) when (ex.Message == "VALOR_MENSAL_INVALIDO")
        {
            return BadRequest(new ErrorResponse { Erro = "O valor mensal minimo e de R$ 100,00.", Codigo = ex.Message });
        }
    }

    [HttpGet("{clienteId}/carteira")]
    public async Task<IActionResult> ConsultarCarteira(int clienteId)
    {
        if (!VerificarAcessoCliente(clienteId))
            return Forbid();

        try
        {
            var quotesFolder = _configuration.GetValue<string>("Cotacoes:Folder") ?? "cotacoes";
            var result = await _clienteService.ConsultarCarteiraAsync(clienteId, ticker => 
            {
                var quote = _cotahistParser.GetClosingQuote(quotesFolder, ticker);
                return quote?.PrecoFechamento ?? 0;
            });
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ErrorResponse { Erro = "Cliente nao encontrado.", Codigo = "CLIENTE_NAO_ENCONTRADO" });
        }
    }

    [HttpGet("{clienteId}/rentabilidade")]
    public async Task<IActionResult> ConsultarRentabilidade(int clienteId)
    {
        if (!VerificarAcessoCliente(clienteId))
            return Forbid();

        try
        {
            var quotesFolder = _configuration.GetValue<string>("Cotacoes:Folder") ?? "cotacoes";
            var result = await _clienteService.GetRentabilidadeAsync(clienteId, ticker =>
                _cotahistParser.GetClosingQuote(quotesFolder, ticker)?.PrecoFechamento ?? 0);

            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ErrorResponse { Erro = "Cliente nao encontrado.", Codigo = "CLIENTE_NAO_ENCONTRADO" });
        }
    }

    private bool VerificarAcessoCliente(int clienteId)
    {
        if (User.IsInRole("Admin")) return true;

        var claimId = User.FindFirst("ClienteId")?.Value;
        return claimId == clienteId.ToString();
    }
}
