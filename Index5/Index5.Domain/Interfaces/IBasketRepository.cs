using Index5.Domain.Entities;

namespace Index5.Domain.Interfaces;

public interface IBasketRepository
{
    Task<RecommendationBasket?> GetActiveAsync();
    Task<List<RecommendationBasket>> GetAllAsync();
    Task AddAsync(RecommendationBasket basket);
    void Update(RecommendationBasket basket);
}
