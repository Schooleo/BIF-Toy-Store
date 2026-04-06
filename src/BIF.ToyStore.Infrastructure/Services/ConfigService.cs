using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.Core.Settings;
using Microsoft.Extensions.Caching.Memory;

namespace BIF.ToyStore.Infrastructure.Services
{
    public class ConfigService(IConfigRepository configRepository, IMemoryCache memoryCache) : IConfigService
    {
        private const string ConfigCacheKey = "app-config-singleton";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

        private readonly IConfigRepository _configRepository = configRepository;
        private readonly IMemoryCache _memoryCache = memoryCache;

        public async Task<AppConfig> GetConfigAsync()
        {
            if (_memoryCache.TryGetValue<AppConfig>(ConfigCacheKey, out var cachedConfig) && cachedConfig is not null)
            {
                return CloneConfig(cachedConfig);
            }

            var dbConfig = await _configRepository.GetSingletonNoTrackingAsync();

            if (dbConfig is null)
            {
                await _configRepository.GetOrCreateSingletonTrackedAsync();
                dbConfig = await _configRepository.GetSingletonNoTrackingAsync();
            }

            if (dbConfig is null)
            {
                throw new InvalidOperationException("Unable to load application configuration.");
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
            var config = await _configRepository.GetOrCreateSingletonTrackedAsync();

            config.Id = 1;
            config.DisplayName = setupConfiguration.DisplayName;
            config.ReceiptHeader = setupConfiguration.ReceiptHeader;
            config.ReceiptFooter = setupConfiguration.ReceiptFooter;
            config.CurrencySymbol = string.IsNullOrWhiteSpace(setupConfiguration.CurrencySymbol)
                ? "VND"
                : setupConfiguration.CurrencySymbol.Trim();
            config.ThemePreference = setupConfiguration.ThemePreference;
            config.EnableLoyaltyPoints = setupConfiguration.EnableLoyaltyPoints;
            config.TaxRate = setupConfiguration.TaxRate;
            config.IsInitialSetupCompleted = true;

            await _configRepository.SaveChangesAsync();

            var cachedCopy = CloneConfig(config);
            _memoryCache.Set(ConfigCacheKey, cachedCopy, CacheTtl);
            return CloneConfig(cachedCopy);
        }

        public async Task<AppConfig> UpdateConfigAsync(string displayName, decimal taxRate, int localServerPort, string databasePath)
        {
            var config = await _configRepository.GetOrCreateSingletonTrackedAsync();

            config.Id = 1;
            config.DisplayName = displayName;
            config.TaxRate = taxRate;
            config.LocalServerPort = localServerPort;
            config.DatabasePath = databasePath;

            await _configRepository.SaveChangesAsync();

            var cachedCopy = CloneConfig(config);
            _memoryCache.Set(ConfigCacheKey, cachedCopy, CacheTtl);
            return CloneConfig(cachedCopy);
        }

        public async Task<AppConfig> UpdateStoreSettingsAsync(
            string displayName,
            decimal taxRate,
            string currencySymbol,
            string receiptHeader,
            string receiptFooter,
            string themePreference)
        {
            var config = await _configRepository.GetOrCreateSingletonTrackedAsync();

            config.Id = 1;
            config.DisplayName = string.IsNullOrWhiteSpace(displayName) ? config.DisplayName : displayName.Trim();
            config.TaxRate = taxRate;
            config.CurrencySymbol = string.IsNullOrWhiteSpace(currencySymbol) ? "VND" : currencySymbol.Trim();
            config.ReceiptHeader = receiptHeader ?? string.Empty;
            config.ReceiptFooter = receiptFooter ?? string.Empty;
            config.ThemePreference = string.IsNullOrWhiteSpace(themePreference) ? "System" : themePreference.Trim();

            await _configRepository.SaveChangesAsync();

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
                CurrencySymbol = source.CurrencySymbol,
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
