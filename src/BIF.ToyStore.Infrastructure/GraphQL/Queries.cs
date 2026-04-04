using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.Infrastructure.Data;
using BIF.ToyStore.Infrastructure.Services;
using HotChocolate.Data;
using HotChocolate.Types;
using Microsoft.EntityFrameworkCore;
using System.Linq;
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
            int? employeeId,
            [Service] IOrderService orderService)
        {
            var (items, totalCount) = await orderService.GetOrdersAsync(
                page, pageSize, fromDate, toDate, employeeId);

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
        public IQueryable<Product> Products([Service] IProductRepository productRepository)
        {
            return productRepository.QueryForGraphQL();
        }

        [UsePaging(IncludeTotalCount = true)]
        [UseFiltering]
        [UseSorting]
        public IQueryable<Category> Categories([Service] ICategoryRepository categoryRepository)
        {
            return categoryRepository.QueryForGraphQL();
        }

        public async Task<List<UserPayload>> Users([Service] AppDbContext dbContext)
        {
            var users = await dbContext.Users
                .AsNoTracking()
                .OrderBy(u => u.Username)
                .ToListAsync();

            return users.Select(u => new UserPayload
            {
                Id = u.Id,
                Username = u.Username,
                PasswordHash = PasswordCipher.TryDecrypt(u.PasswordHash, out var plainText)
                    ? plainText
                    : string.Empty,
                Role = u.Role
            }).ToList();
        }

        public async Task<List<UserListItemPayload>> GetUserList([Service] AppDbContext dbContext)
        {
            var users = await dbContext.Users
                .AsNoTracking()
                .OrderBy(u => u.Username)
                .ToListAsync();

            return users.Select(UserListItemPayload.FromUser).ToList();
        }

        public async Task<List<SaleKpiRankingPayload>> GetSaleKpiRanking(
            DateTime? fromDate,
            DateTime? toDate,
            [Service] IOrderService orderService)
        {
            var ranking = await orderService.GetSaleKpiRankingAsync(fromDate, toDate);
            return ranking.Select(SaleKpiRankingPayload.FromModel).ToList();
        }

        public async Task<List<RevenueTrendPointPayload>> GetRevenueTrend(
            int days,
            [Service] IOrderService orderService)
        {
            var points = await orderService.GetRevenueTrendAsync(days);
            return points.Select(RevenueTrendPointPayload.FromModel).ToList();
        }

        public async Task<List<BestSellingProductPayload>> GetTopBestSellingProducts(
            int take,
            [Service] IOrderService orderService)
        {
            var products = await orderService.GetTopBestSellingProductsAsync(take);
            return products.Select(BestSellingProductPayload.FromModel).ToList();
        }
    }

    [ExtendObjectType(typeof(Category))]
    public class CategoryExtension
    {
        [BindMember(nameof(Category.Products))]
        public IQueryable<Product> GetProducts([Parent] Category category, [Service] IProductRepository productRepository)
        {
            return productRepository.QueryByCategoryForGraphQL(category.Id);
        }
    }

    [ExtendObjectType(typeof(Product))]
    public class ProductExtension
    {
        [BindMember(nameof(Product.Category))]
        public async Task<Category?> GetCategory([Parent] Product product, [Service] IProductRepository productRepository)
        {
            return await productRepository.ResolveEffectiveCategoryAsync(product);
        }
    }
}

