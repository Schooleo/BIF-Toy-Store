using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.Core.Settings;
using BIF.ToyStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BIF.ToyStore.Infrastructure.Repositories
{
    public class ProductRepository(AppDbContext dbContext) : BaseRepository<Product>(dbContext), IProductRepository
    {
        public async Task<int> BulkInsertAsync(IEnumerable<Product> products)
        {
            await _dbSet.AddRangeAsync(products);
            return await _dbContext.SaveChangesAsync();
        }

        public async Task<Product> UpdateDetailsAsync(Product product)
        {
            var existing = await _dbContext.Products
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Id == product.Id && !p.IsDeleted);

            if (existing is null)
            {
                throw new InvalidOperationException("Product not found.");
            }

            existing.Name = product.Name;
            existing.CategoryId = product.CategoryId;
            existing.RetailPrice = product.RetailPrice;
            existing.ImportPrice = product.ImportPrice;
            existing.StockQuantity = product.StockQuantity;

            await _dbContext.SaveChangesAsync();

            return existing;
        }

        public async Task<bool> SoftDeleteAsync(int id)
        {
            var product = await _dbContext.Products
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

            if (product is null)
            {
                return false;
            }

            product.IsDeleted = true;
            await _dbContext.SaveChangesAsync();
            return true;
        }

        public IQueryable<Product> QueryForGraphQL()
        {
            var deletedCategoryIds = _dbContext.Categories
                .IgnoreQueryFilters()
                .Where(c => c.IsDeleted && c.Id != AppConstants.OtherCategoryId)
                .Select(c => c.Id);

            return _dbContext.Products
                .IgnoreQueryFilters()
                .Where(p => !p.IsDeleted)
                .Select(p => new Product
                {
                    Id = p.Id,
                    Name = p.Name,
                    CategoryId = deletedCategoryIds.Contains(p.CategoryId)
                        ? AppConstants.OtherCategoryId
                        : p.CategoryId,
                    RetailPrice = p.RetailPrice,
                    ImportPrice = p.ImportPrice,
                    StockQuantity = p.StockQuantity,
                    IsDeleted = p.IsDeleted
                })
                .AsNoTracking();
        }

        public IQueryable<Product> QueryByCategoryForGraphQL(int categoryId)
        {
            if (categoryId != AppConstants.OtherCategoryId)
            {
                return _dbContext.Products
                    .IgnoreQueryFilters()
                    .Where(p => !p.IsDeleted && p.CategoryId == categoryId)
                    .AsNoTracking();
            }

            var deletedCategoryIds = _dbContext.Categories
                .IgnoreQueryFilters()
                .Where(c => c.IsDeleted && c.Id != AppConstants.OtherCategoryId)
                .Select(c => c.Id);

            var effectiveOtherProductIds = _dbContext.Products
                .IgnoreQueryFilters()
                .Where(p => !p.IsDeleted && (p.CategoryId == AppConstants.OtherCategoryId || deletedCategoryIds.Contains(p.CategoryId)))
                .Select(p => p.Id)
                .Distinct();

            return _dbContext.Products
                .IgnoreQueryFilters()
                .Where(p => !p.IsDeleted && effectiveOtherProductIds.Contains(p.Id))
                .AsNoTracking();
        }

        public async Task<Category?> ResolveEffectiveCategoryAsync(Product product)
        {
            if (product.Category != null)
            {
                if (!product.Category.IsDeleted)
                {
                    return product.Category;
                }

                return await _dbContext.Categories
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == AppConstants.OtherCategoryId);
            }

            var originalCategory = await _dbContext.Categories
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == product.CategoryId);

            if (originalCategory is null)
            {
                return null;
            }

            if (!originalCategory.IsDeleted)
            {
                return originalCategory;
            }

            return await _dbContext.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == AppConstants.OtherCategoryId);
        }
    }
}
