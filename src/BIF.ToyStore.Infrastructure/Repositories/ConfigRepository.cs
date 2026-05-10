using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Settings;
using BIF.ToyStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BIF.ToyStore.Infrastructure.Repositories
{
    public class ConfigRepository(AppDbContext dbContext) : IConfigRepository
    {
        private readonly AppDbContext _dbContext = dbContext;

        public async Task<AppConfig?> GetSingletonNoTrackingAsync()
        {
            return await _dbContext.AppConfigs
                .AsNoTracking()
                .SingleOrDefaultAsync(c => c.Id == 1);
        }

        public async Task<AppConfig> GetOrCreateSingletonTrackedAsync()
        {
            var config = await _dbContext.AppConfigs.SingleOrDefaultAsync(c => c.Id == 1);

            if (config is not null)
            {
                return config;
            }

            config = new AppConfig { Id = 1 };
            _dbContext.AppConfigs.Add(config);
            await _dbContext.SaveChangesAsync();

            return await _dbContext.AppConfigs.SingleAsync(c => c.Id == 1);
        }

        public async Task SaveChangesAsync()
        {
            await _dbContext.SaveChangesAsync();
        }
    }
}
