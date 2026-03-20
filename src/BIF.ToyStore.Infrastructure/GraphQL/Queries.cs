using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.Infrastructure.Data;
using HotChocolate.Data;
using HotChocolate.Types;
using Microsoft.EntityFrameworkCore;
using System.Linq;

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
}
