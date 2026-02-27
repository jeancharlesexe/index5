using Index5.Application.DTOs;
using Index5.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Index5.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var response = await _authService.LoginAsync(request);
        if (response == null)
            return Unauthorized(new { mensagem = "Usuário ou senha inválidos" });

        return Ok(response);
    }

    [HttpPost("registrar")]
    public async Task<IActionResult> Registrar([FromBody] RegistroRequest request)
    {
        var sucesso = await _authService.RegistrarAsync(request);
        if (!sucesso)
            return BadRequest(new { mensagem = "Usuário já existe" });

        return Ok(new { mensagem = "Usuário criado com sucesso" });
    }
}
