using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.Infrastructure.Data;

namespace BIF.ToyStore.Infrastructure.Repositories
{
    public class ProductRepository(AppDbContext dbContext) : BaseRepository<Product>(dbContext), IProductRepository
    {
        public async Task<int> BulkInsertAsync(IEnumerable<Product> products)
        {
            await _dbSet.AddRangeAsync(products);
            return await _dbContext.SaveChangesAsync();
        }
    }
}
