using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.Core.Settings;
using BIF.ToyStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BIF.ToyStore.Infrastructure.Repositories
{
    public class CategoryRepository(AppDbContext dbContext) : BaseRepository<Category>(dbContext), ICategoryRepository
    {
        public IQueryable<Category> QueryForGraphQL()
        {
            var deletedCategoryIds = _dbContext.Categories
                .IgnoreQueryFilters()
                .Where(c => c.IsDeleted && c.Id != AppConstants.OtherCategoryId)
                .Select(c => c.Id);

            return _dbContext.Categories
                .AsNoTracking()
                .Select(c => new Category
                {
                    Id = c.Id,
                    Name = c.Name,
                    IsDeleted = c.IsDeleted,
                    ProductCount = c.Id == AppConstants.OtherCategoryId
                        ? _dbContext.Products
                            .IgnoreQueryFilters()
                            .Count(p => !p.IsDeleted && (p.CategoryId == AppConstants.OtherCategoryId || deletedCategoryIds.Contains(p.CategoryId)))
                        : _dbContext.Products
                            .IgnoreQueryFilters()
                            .Count(p => !p.IsDeleted && p.CategoryId == c.Id)
                });
        }

        public async Task<Category> UpdateNameAsync(int id, string name)
        {
            var category = await _dbContext.Categories
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted)
                ?? throw new InvalidOperationException("Category not found.");

            category.Name = name;
            await _dbContext.SaveChangesAsync();
            return category;
        }

        public async Task<bool> SoftDeleteAsync(int id)
        {
            if (id == AppConstants.OtherCategoryId)
            {
                throw new InvalidOperationException("Cannot delete the default 'Other' category.");
            }

            var category = await _dbContext.Categories
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted)
                ?? throw new InvalidOperationException("Category not found.");

            category.IsDeleted = true;
            await _dbContext.SaveChangesAsync();
            return true;
        }

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
