using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BIF.ToyStore.Infrastructure.Repositories
{
    public class CategoryRepository(AppDbContext dbContext) : BaseRepository<Category>(dbContext), ICategoryRepository
    {
        public async Task<Category> RestoreAsync(int id)
        {
            var category = await _dbContext.Categories
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Id == id)
                ?? throw new InvalidOperationException($"Category with ID {id} not found.");

            category.IsDeleted = false;
            await _dbContext.SaveChangesAsync();
            return category;
        }
    }
}
