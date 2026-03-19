using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.Core.Settings;
using BIF.ToyStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BIF.ToyStore.Infrastructure.Services
{
    public class ConfigService(AppDbContext dbContext, IMemoryCache memoryCache) : IConfigService
    {
        private const string ConfigCacheKey = "app-config-singleton";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

        private readonly AppDbContext _dbContext = dbContext;
        private readonly IMemoryCache _memoryCache = memoryCache;

        public async Task<AppConfig> GetConfigAsync()
        {
            if (_memoryCache.TryGetValue<AppConfig>(ConfigCacheKey, out var cachedConfig) && cachedConfig is not null)
            {
                return CloneConfig(cachedConfig);
            }

            var dbConfig = await _dbContext.AppConfigs
                .AsNoTracking()
                .SingleOrDefaultAsync(c => c.Id == 1);

            if (dbConfig is null)
            {
                dbConfig = new AppConfig { Id = 1 };
                _dbContext.AppConfigs.Add(dbConfig);
                await _dbContext.SaveChangesAsync();

                dbConfig = await _dbContext.AppConfigs
                    .AsNoTracking()
                    .SingleAsync(c => c.Id == 1);
            }

            _memoryCache.Set(ConfigCacheKey, CloneConfig(dbConfig), CacheTtl);
            return CloneConfig(dbConfig);
        }

        public async Task<bool> IsInitialSetupCompletedAsync()
        {
            var config = await GetConfigAsync();
            return config.IsInitialSetupCompleted;
        }

        public async Task<AppConfig> CompleteInitialSetupAsync(InitialSetupConfiguration setupConfiguration)
        {
            var config = await _dbContext.AppConfigs.SingleOrDefaultAsync(c => c.Id == 1)
                ?? new AppConfig { Id = 1 };

            config.Id = 1;
            config.DisplayName = setupConfiguration.DisplayName;
            config.ReceiptHeader = setupConfiguration.ReceiptHeader;
            config.ReceiptFooter = setupConfiguration.ReceiptFooter;
            config.ThemePreference = setupConfiguration.ThemePreference;
            config.EnableLoyaltyPoints = setupConfiguration.EnableLoyaltyPoints;
            config.TaxRate = setupConfiguration.TaxRate;
            config.IsInitialSetupCompleted = true;

            if (_dbContext.Entry(config).State == EntityState.Detached)
            {
                _dbContext.AppConfigs.Add(config);
            }

            await _dbContext.SaveChangesAsync();

            var cachedCopy = CloneConfig(config);
            _memoryCache.Set(ConfigCacheKey, cachedCopy, CacheTtl);
            return CloneConfig(cachedCopy);
        }

        public async Task<AppConfig> UpdateConfigAsync(string displayName, decimal taxRate, int localServerPort, string databasePath)
        {
            var config = await _dbContext.AppConfigs.SingleOrDefaultAsync(c => c.Id == 1)
                ?? new AppConfig { Id = 1 };

            config.Id = 1;
            config.DisplayName = displayName;
            config.TaxRate = taxRate;
            config.LocalServerPort = localServerPort;
            config.DatabasePath = databasePath;

            if (_dbContext.Entry(config).State == EntityState.Detached)
            {
                _dbContext.AppConfigs.Add(config);
            }

            await _dbContext.SaveChangesAsync();

            var cachedCopy = CloneConfig(config);
            _memoryCache.Set(ConfigCacheKey, cachedCopy, CacheTtl);
            return CloneConfig(cachedCopy);
        }

        private static AppConfig CloneConfig(AppConfig source)
        {
            return new AppConfig
            {
                Id = source.Id,
                DisplayName = source.DisplayName,
                ReceiptHeader = source.ReceiptHeader,
                ReceiptFooter = source.ReceiptFooter,
                ThemePreference = source.ThemePreference,
                EnableLoyaltyPoints = source.EnableLoyaltyPoints,
                TaxRate = source.TaxRate,
                LocalServerPort = source.LocalServerPort,
                DatabasePath = source.DatabasePath,
                IsInitialSetupCompleted = source.IsInitialSetupCompleted
            };
        }
    }
}
