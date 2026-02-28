using FluentAssertions;
using Index5.Application.DTOs;
using Index5.Application.Services;
using Index5.Domain.Entities;
using Index5.Domain.Interfaces;
using Moq;
using Microsoft.Extensions.Configuration;

namespace Index5.UnitTests;

public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IConfiguration> _configMock;
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        _userRepoMock = new Mock<IUserRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _configMock = new Mock<IConfiguration>();

        // Setup mock config for JWT
        var jwtSection = new Mock<IConfigurationSection>();
        jwtSection.SetupGet(x => x["Key"]).Returns("this_is_a_very_long_secret_key_used_for_testing_12345");
        jwtSection.SetupGet(x => x["ExpirationHours"]).Returns("8");
        jwtSection.SetupGet(x => x["Issuer"]).Returns("test");
        jwtSection.SetupGet(x => x["Audience"]).Returns("test");
        
        _configMock.Setup(x => x.GetSection("Jwt")).Returns(jwtSection.Object);

        _service = new AuthService(_userRepoMock.Object, _unitOfWorkMock.Object, _configMock.Object);
    }

    [Fact]
    public async Task LoginClientAsync_ValidUser_GeneratesToken()
    {
        var password = "password123";
        var hash = BCrypt.Net.BCrypt.HashPassword(password);
        var user = new User { Id = 1, Role = "CLIENT", Cpf = "123", Name = "Test", Email = "a@a.com", PasswordHash = hash, Active = true };
        _userRepoMock.Setup(repo => repo.GetByCpfAsync("123")).ReturnsAsync(user);

        var result = await _service.LoginClientAsync(new LoginClientRequest { Cpf = "123", Password = password });

        result.Should().NotBeNull();
        result.Token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RegisterAsync_ValidAdminRequest_CreatesUser()
    {
        var request = new RegisterRequest { 
            Name = "Admin", Cpf = "456", Email = "adm@it.com", 
            Password = "123", Role = "ADMIN", JKey = "J123", BirthDate = DateTime.Today.AddYears(-20) 
        };
        _userRepoMock.Setup(r => r.GetByCpfAsync("456")).ReturnsAsync((User?)null);
        _userRepoMock.Setup(r => r.GetByEmailAsync("adm@it.com")).ReturnsAsync((User?)null);
        _userRepoMock.Setup(r => r.GetByJKeyAsync("J123")).ReturnsAsync((User?)null);

        var result = await _service.RegisterAsync(request);

        result.Cpf.Should().Be("456");
        result.JKey.Should().Be("J123");
        _userRepoMock.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_ThrowsException()
    {
        var request = new RegisterRequest { Name = "A", Cpf = "789", Email = "dup@a.com", Password = "1", Role = "CLIENT", BirthDate = DateTime.Today };
        _userRepoMock.Setup(r => r.GetByCpfAsync("789")).ReturnsAsync((User?)null);
        _userRepoMock.Setup(r => r.GetByEmailAsync("dup@a.com")).ReturnsAsync(new User());

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RegisterAsync(request));
    }

    [Fact]
    public async Task RegisterAsync_AdminWithoutJKey_ThrowsException()
    {
        var request = new RegisterRequest { Name = "A", Cpf = "1", Email = "e", Password = "1", Role = "ADMIN", JKey = "", BirthDate = DateTime.Today };
        var act = () => _service.RegisterAsync(request);
        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage("JKEY_REQUIRED");
    }

    [Fact]
    public async Task LoginAdminAsync_InvalidJKey_ThrowsException()
    {
         _userRepoMock.Setup(r => r.GetByJKeyAsync(It.IsAny<string>())).ReturnsAsync((User?)null);
         await Assert.ThrowsAsync<InvalidOperationException>(() => _service.LoginAdminAsync(new LoginAdminRequest { JKey = "X", Password = "1" }));
    }

    [Fact]
    public async Task LoginClientAsync_InactiveUser_ThrowsException()
    {
        var user = new User { Role = "CLIENT", Active = false, PasswordHash = BCrypt.Net.BCrypt.HashPassword("1") };
        _userRepoMock.Setup(r => r.GetByCpfAsync("123")).ReturnsAsync(user);
        var act = () => _service.LoginClientAsync(new LoginClientRequest { Cpf = "123", Password = "1" });
        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage("INACTIVE_USER");
    }

    [Fact]
    public async Task RegisterAsync_MissingFields_ThrowsExceptions()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RegisterAsync(new RegisterRequest { Name = "" }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RegisterAsync(new RegisterRequest { Name = "A", Cpf = "" }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RegisterAsync(new RegisterRequest { Name = "A", Cpf = "1", Email = "" }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RegisterAsync(new RegisterRequest { Name = "A", Cpf = "1", Email = "e", Password = "" }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RegisterAsync(new RegisterRequest { Name = "A", Cpf = "1", Email = "e", Password = "p", BirthDate = default }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RegisterAsync(new RegisterRequest { Name = "A", Cpf = "1", Email = "e", Password = "p", BirthDate = DateTime.Today, Role = "X" }));
    }

    [Fact]
    public async Task RegisterAsync_DuplicateJKey_ThrowsException()
    {
        var request = new RegisterRequest { Role = "ADMIN", JKey = "DUP", Name = "A", Cpf = "1", Email = "e", Password = "p", BirthDate = DateTime.Today };
        _userRepoMock.Setup(r => r.GetByJKeyAsync("DUP")).ReturnsAsync(new User());
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RegisterAsync(request));
    }

    [Fact]
    public async Task LoginAdminAsync_ValidUser_GeneratesToken()
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("123");
        var user = new User { Role = "ADMIN", JKey = "J1", PasswordHash = hash, Active = true, Name = "A", Email = "e" };
        _userRepoMock.Setup(r => r.GetByJKeyAsync("J1")).ReturnsAsync(user);
        
        var result = await _service.LoginAdminAsync(new LoginAdminRequest { JKey = "J1", Password = "123" });
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task RegisterAsync_ClientWithJKey_ThrowsException()
    {
        var request = new RegisterRequest { Role = "CLIENT", JKey = "X", Name = "A", Cpf = "1", Email = "e", Password = "p", BirthDate = DateTime.Today };
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RegisterAsync(request));
    }
}
