using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.Infrastructure.Data;
using HotChocolate.Data;
using HotChocolate.Types;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using BIF.ToyStore.Core.Settings;
using HotChocolate;

namespace BIF.ToyStore.Infrastructure.GraphQL
{
    public class Queries
    {
        public string Ping() => "The BIF Toy Store GraphQL server is running.";

        public async Task<SetupStatePayload> SetupState([Service] IConfigService configService)
        {
            return new SetupStatePayload
            {
                IsInitialSetupCompleted = await configService.IsInitialSetupCompletedAsync()
            };
        }

        public async Task<AppConfigPayload> AppConfig([Service] IConfigService configService)
        {
            var config = await configService.GetConfigAsync();
            return AppConfigPayload.FromConfig(config);
        }


        public async Task<OrderListPayload> GetOrders(
            int page,
            int pageSize,
            DateTime? fromDate,
            DateTime? toDate,
            [Service] IOrderService orderService)
        {
            var (items, totalCount) = await orderService.GetOrdersAsync(
                page, pageSize, fromDate, toDate);

            return new OrderListPayload
            {
                Items = items.Select(OrderPayload.FromOrder).ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<OrderPayload?> GetOrderById(
            int id,
            [Service] IOrderService orderService)
        {
            var order = await orderService.GetOrderByIdAsync(id);
            return order is not null ? OrderPayload.FromOrder(order) : null;
        }

        [UsePaging(IncludeTotalCount = true)]
        [UseFiltering]
        [UseSorting]
        public IQueryable<Product> Products([Service] AppDbContext dbContext)
        {
            return dbContext.Products.Include(p => p.Category).AsNoTracking();
        }

        [UsePaging(IncludeTotalCount = true)]
        [UseFiltering]
        [UseSorting]
        public IQueryable<Category> Categories([Service] AppDbContext dbContext)
        {
            return dbContext.Categories.Include(c => c.Products).AsNoTracking();
        }
    }

    [ExtendObjectType(typeof(Category))]
    public class CategoryExtension
    {
        [BindMember(nameof(Category.Products))]
        public IQueryable<Product> GetProducts([Parent] Category category, [Service] AppDbContext dbContext)
        {
            if (category.Id != AppConstants.OtherCategoryId)
            {
                return dbContext.Products.Where(p => p.CategoryId == category.Id);
            }

            return dbContext.Products.Where(p => 
                p.CategoryId == AppConstants.OtherCategoryId || 
                dbContext.Categories.IgnoreQueryFilters().Any(c => c.Id == p.CategoryId && c.IsDeleted));
        }
    }

    [ExtendObjectType(typeof(Product))]
    public class ProductExtension
    {
        [BindMember(nameof(Product.Category))]
        public async Task<Category?> GetCategory([Parent] Product product, [Service] AppDbContext dbContext)
        {
            if (product.Category != null)
            {
                return product.Category;
            }

            var originalCategory = await dbContext.Categories
                                            .IgnoreQueryFilters()
                                            .FirstOrDefaultAsync(c => c.Id == product.CategoryId);

            if (originalCategory != null && originalCategory.IsDeleted)
            {
                return await dbContext.Categories.FirstOrDefaultAsync(c => c.Id == AppConstants.OtherCategoryId);
            }

            return null;
        }
    }
}

