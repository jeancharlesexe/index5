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
    private readonly IUserRepository _userRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;

    public AuthService(
        IUserRepository userRepo,
        IUnitOfWork unitOfWork,
        IConfiguration configuration)
    {
        _userRepo = userRepo;
        _unitOfWork = unitOfWork;
        _configuration = configuration;
    }

    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("NAME_REQUIRED");

        if (string.IsNullOrWhiteSpace(request.Cpf))
            throw new InvalidOperationException("CPF_REQUIRED");

        if (string.IsNullOrWhiteSpace(request.Email))
            throw new InvalidOperationException("EMAIL_REQUIRED");

        if (string.IsNullOrWhiteSpace(request.Password))
            throw new InvalidOperationException("PASSWORD_REQUIRED");

        if (request.BirthDate == default)
            throw new InvalidOperationException("BIRTHDATE_REQUIRED");

        if (request.Role != "ADMIN" && request.Role != "CLIENT")
            throw new InvalidOperationException("INVALID_ROLE");

        if (request.Role == "ADMIN" && string.IsNullOrWhiteSpace(request.Username))
            throw new InvalidOperationException("USERNAME_REQUIRED");

        if (request.Role == "CLIENT" && !string.IsNullOrWhiteSpace(request.Username))
            throw new InvalidOperationException("CLIENT_SHOULD_NOT_HAVE_USERNAME");

        var existingCpf = await _userRepo.GetByCpfAsync(request.Cpf);
        if (existingCpf != null)
            throw new InvalidOperationException("CPF_ALREADY_REGISTERED");

        var existingEmail = await _userRepo.GetByEmailAsync(request.Email);
        if (existingEmail != null)
            throw new InvalidOperationException("EMAIL_ALREADY_REGISTERED");

        if (!string.IsNullOrWhiteSpace(request.Username))
        {
            var existingUsername = await _userRepo.GetByUsernameAsync(request.Username);
            if (existingUsername != null)
                throw new InvalidOperationException("USERNAME_ALREADY_REGISTERED");
        }

        var user = new User
        {
            Name = request.Name,
            Cpf = request.Cpf,
            Email = request.Email,
            Username = request.Role == "ADMIN" ? request.Username : null,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            BirthDate = request.BirthDate,
            Role = request.Role,
            CreatedAt = DateTime.UtcNow,
            Active = true
        };

        await _userRepo.AddAsync(user);
        await _unitOfWork.SaveChangesAsync();

        return new RegisterResponse
        {
            UserId = user.Id,
            Name = user.Name,
            Cpf = user.Cpf,
            Email = user.Email,
            Username = user.Username,
            Role = user.Role,
            CreatedAt = user.CreatedAt
        };
    }

    public async Task<LoginResponse> LoginClientAsync(LoginClientRequest request)
    {
        var user = await _userRepo.GetByCpfAsync(request.Cpf);
        if (user == null || user.Role != "CLIENT")
            throw new InvalidOperationException("INVALID_CREDENTIALS");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new InvalidOperationException("INVALID_CREDENTIALS");

        if (!user.Active)
            throw new InvalidOperationException("INACTIVE_USER");

        return GenerateTokenResponse(user);
    }

    public async Task<LoginResponse> LoginAdminAsync(LoginAdminRequest request)
    {
        var user = await _userRepo.GetByUsernameAsync(request.Username);
        if (user == null || user.Role != "ADMIN")
            throw new InvalidOperationException("INVALID_CREDENTIALS");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new InvalidOperationException("INVALID_CREDENTIALS");

        if (!user.Active)
            throw new InvalidOperationException("INACTIVE_USER");

        return GenerateTokenResponse(user);
    }

    private LoginResponse GenerateTokenResponse(User user)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]!));
        var expiration = DateTime.UtcNow.AddHours(int.Parse(jwtSettings["ExpirationHours"] ?? "8"));

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role),
            new("cpf", user.Cpf)
        };

        if (!string.IsNullOrEmpty(user.Username))
            claims.Add(new Claim("username", user.Username));

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: expiration,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new LoginResponse
        {
            UserId = user.Id,
            Name = user.Name,
            Role = user.Role,
            Token = new JwtSecurityTokenHandler().WriteToken(token),
            Expiration = expiration
        };
    }
}
