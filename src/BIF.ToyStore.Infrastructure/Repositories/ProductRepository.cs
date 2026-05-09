using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.Core.Settings;
using BIF.ToyStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;

namespace BIF.ToyStore.Infrastructure.Repositories
{
    public class ProductRepository(AppDbContext dbContext) : BaseRepository<Product>(dbContext), IProductRepository
    {
        public async Task<int> BulkInsertAsync(IEnumerable<Product> products)
        {
            var incomingProducts = products
                .Where(p => p is not null && !string.IsNullOrWhiteSpace(p.Name))
                .ToList();

            if (incomingProducts.Count == 0)
            {
                return 0;
            }

            var incomingNames = incomingProducts
                .Select(p => p.Name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var existingProducts = await _dbContext.Products
                .IgnoreQueryFilters()
                .Where(p => incomingNames.Contains(p.Name))
                .ToListAsync();

            var existingByName = existingProducts.ToDictionary(
                p => p.Name,
                p => p,
                StringComparer.OrdinalIgnoreCase);

            var changedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var incoming in incomingProducts)
            {
                var normalizedName = incoming.Name.Trim();

                if (existingByName.TryGetValue(normalizedName, out var existing))
                {
                    var isChanged = false;

                    if (existing.CategoryId != incoming.CategoryId)
                    {
                        existing.CategoryId = incoming.CategoryId;
                        isChanged = true;
                    }

                    if (existing.RetailPrice != incoming.RetailPrice)
                    {
                        existing.RetailPrice = incoming.RetailPrice;
                        isChanged = true;
                    }

                    if (existing.ImportPrice != incoming.ImportPrice)
                    {
                        existing.ImportPrice = incoming.ImportPrice;
                        isChanged = true;
                    }

                    if (existing.StockQuantity != incoming.StockQuantity)
                    {
                        existing.StockQuantity += incoming.StockQuantity;
                        isChanged = true;
                    }

                    if (existing.IsDeleted)
                    {
                        existing.IsDeleted = false;
                        isChanged = true;
                    }

                    if (isChanged)
                    {
                        changedNames.Add(normalizedName);
                    }

                    continue;
                }

                var newProduct = new Product
                {
                    Name = normalizedName,
                    CategoryId = incoming.CategoryId,
                    RetailPrice = incoming.RetailPrice,
                    ImportPrice = incoming.ImportPrice,
                    StockQuantity = incoming.StockQuantity,
                    Images = incoming.Images,
                    IsDeleted = false
                };

                await _dbSet.AddAsync(newProduct);
                existingByName[normalizedName] = newProduct;
                changedNames.Add(normalizedName);
            }

            if (changedNames.Count == 0)
            {
                return 0;
            }

            await _dbContext.SaveChangesAsync();
            return changedNames.Count;
        }

        public async Task<Product> UpdateDetailsAsync(Product product)
        {
            var existing = await _dbContext.Products
                .Include(p => p.Images)
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

            // Update Images
            _dbContext.ProductImages.RemoveRange(existing.Images);
            if (product.Images != null && product.Images.Any())
            {
                foreach (var img in product.Images)
                {
                    existing.Images.Add(new ProductImage
                    {
                        ImageUrl = img.ImageUrl,
                        DisplayOrder = img.DisplayOrder,
                        IsPrimary = img.IsPrimary
                    });
                }
            }

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
                    Images = new ObservableCollection<ProductImage>(p.Images.Select(img => new ProductImage 
                    {
                        Id = img.Id,
                        ImageUrl = img.ImageUrl,
                        DisplayOrder = img.DisplayOrder,
                        IsPrimary = img.IsPrimary
                    }).ToList()), // Note: In some EF Core versions, this might still be needed for in-memory, but usually causes issues with IQueryable providers.
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
                    .Include(p => p.Images)
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
                .Include(p => p.Images)
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
