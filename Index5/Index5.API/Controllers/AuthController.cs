using Index5.Application.DTOs;
using Index5.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Index5.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var result = await _authService.RegisterAsync(request);
            return StatusCode(201, ApiResponse<RegisterResponse>.Created(result, "User registered successfully."));
        }
        catch (InvalidOperationException ex) when (ex.Message == "NAME_REQUIRED")
        {
            return BadRequest(ApiResponse<object>.Error("Name is required.", ex.Message));
        }
        catch (InvalidOperationException ex) when (ex.Message == "CPF_REQUIRED")
        {
            return BadRequest(ApiResponse<object>.Error("CPF is required.", ex.Message));
        }
        catch (InvalidOperationException ex) when (ex.Message == "EMAIL_REQUIRED")
        {
            return BadRequest(ApiResponse<object>.Error("Email is required.", ex.Message));
        }
        catch (InvalidOperationException ex) when (ex.Message == "PASSWORD_REQUIRED")
        {
            return BadRequest(ApiResponse<object>.Error("Password is required.", ex.Message));
        }
        catch (InvalidOperationException ex) when (ex.Message == "BIRTHDATE_REQUIRED")
        {
            return BadRequest(ApiResponse<object>.Error("Birth date is required.", ex.Message));
        }
        catch (InvalidOperationException ex) when (ex.Message == "CLIENT_SHOULD_NOT_HAVE_JKEY")
        {
            return BadRequest(ApiResponse<object>.Error("Clients should not have a JKey.", ex.Message));
        }
        catch (InvalidOperationException ex) when (ex.Message == "CPF_ALREADY_REGISTERED")
        {
            return BadRequest(ApiResponse<object>.Error("CPF already registered.", ex.Message));
        }
        catch (InvalidOperationException ex) when (ex.Message == "EMAIL_ALREADY_REGISTERED")
        {
            return BadRequest(ApiResponse<object>.Error("Email already registered.", ex.Message));
        }
        catch (InvalidOperationException ex) when (ex.Message == "JKEY_ALREADY_REGISTERED")
        {
            return BadRequest(ApiResponse<object>.Error("JKey already registered.", ex.Message));
        }
        catch (InvalidOperationException ex) when (ex.Message == "JKEY_REQUIRED")
        {
            return BadRequest(ApiResponse<object>.Error("JKey is required for administrators.", ex.Message));
        }
        catch (InvalidOperationException ex) when (ex.Message == "INVALID_ROLE")
        {
            return BadRequest(ApiResponse<object>.Error("Role must be ADMIN or CLIENT.", ex.Message));
        }
    }

    [HttpPost("login/client")]
    public async Task<IActionResult> LoginClient([FromBody] LoginClientRequest request)
    {
        try
        {
            var result = await _authService.LoginClientAsync(request);
            return Ok(ApiResponse<LoginResponse>.Success(result, "Login successful."));
        }
        catch (InvalidOperationException ex) when (ex.Message == "INVALID_CREDENTIALS")
        {
            return Unauthorized(ApiResponse<object>.Error("Invalid CPF or password.", ex.Message, 401));
        }
        catch (InvalidOperationException ex) when (ex.Message == "INACTIVE_USER")
        {
            return Unauthorized(ApiResponse<object>.Error("Inactive user.", ex.Message, 401));
        }
    }

    [HttpPost("login/admin")]
    public async Task<IActionResult> LoginAdmin([FromBody] LoginAdminRequest request)
    {
        try
        {
            var result = await _authService.LoginAdminAsync(request);
            return Ok(ApiResponse<LoginResponse>.Success(result, "Login successful."));
        }
        catch (InvalidOperationException ex) when (ex.Message == "INVALID_CREDENTIALS")
        {
            return Unauthorized(ApiResponse<object>.Error("Invalid JKey or password.", ex.Message, 401));
        }
        catch (InvalidOperationException ex) when (ex.Message == "INACTIVE_USER")
        {
            return Unauthorized(ApiResponse<object>.Error("Inactive user.", ex.Message, 401));
        }
    }
}
