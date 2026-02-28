using Index5.Domain.Entities;
using Index5.Domain.Interfaces;
using Index5.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Index5.Infrastructure.Repositories;

public class BasketRepository : IBasketRepository
{
    private readonly AppDbContext _context;

    public BasketRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<RecommendationBasket?> GetActiveAsync()
    {
        return await _context.RecommendationBaskets
            .Include(b => b.Items)
            .FirstOrDefaultAsync(b => b.Active);
    }

    public async Task<List<RecommendationBasket>> GetAllAsync()
    {
        return await _context.RecommendationBaskets
            .Include(b => b.Items)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();
    }

    public async Task AddAsync(RecommendationBasket basket)
    {
        await _context.RecommendationBaskets.AddAsync(basket);
    }

    public void Update(RecommendationBasket basket)
    {
        _context.RecommendationBaskets.Update(basket);
    }
}
