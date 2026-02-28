using Index5.Domain.Entities;
using Index5.Domain.Interfaces;
using Index5.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Index5.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByCpfAsync(string cpf)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Cpf == cpf);
    }

    public async Task<User?> GetByJKeyAsync(string jKey)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.JKey == jKey);
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task AddAsync(User user)
    {
        await _context.Users.AddAsync(user);
    }
}
