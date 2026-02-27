using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Index5.Application.DTOs;
using Index5.Domain.Entities;
using Index5.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Index5.Application.Services;

public class AuthService
{
    private readonly IUsuarioRepository _usuarioRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _config;

    public AuthService(IUsuarioRepository usuarioRepo, IUnitOfWork unitOfWork, IConfiguration config)
    {
        _usuarioRepo = usuarioRepo;
        _unitOfWork = unitOfWork;
        _config = config;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        var usuario = await _usuarioRepo.GetByUsernameAsync(request.Username);

        if (usuario == null || !BCrypt.Net.BCrypt.Verify(request.Password, usuario.PasswordHash))
            return null;

        var token = GerarJwtToken(usuario);

        return new LoginResponse
        {
            Token = token,
            Username = usuario.Username,
            Role = usuario.Role,
            ClienteId = usuario.ClienteId
        };
    }

    public async Task<bool> RegistrarAsync(RegistroRequest request)
    {
        var existing = await _usuarioRepo.ExistsAsync(request.Username);
        if (existing) return false;

        var usuario = new Usuario
        {
            Username = request.Username,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = request.Role,
            ClienteId = request.ClienteId
        };

        await _usuarioRepo.AddAsync(usuario);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    private string GerarJwtToken(Usuario usuario)
    {
        var jwtSettings = _config.GetSection("Jwt");
        var key = Encoding.ASCII.GetBytes(jwtSettings["Key"] ?? "chave-secreta-super-ultra-segura-e-longa-do-itau");

        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, usuario.Username),
                new Claim(ClaimTypes.Role, usuario.Role),
                new Claim("ClienteId", usuario.ClienteId?.ToString() ?? ""),
                new Claim("UsuarioId", usuario.Id.ToString())
            }),
            Expires = DateTime.UtcNow.AddHours(2),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
            Issuer = jwtSettings["Issuer"],
            Audience = jwtSettings["Audience"]
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}
