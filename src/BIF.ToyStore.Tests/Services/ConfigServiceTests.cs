using BIF.ToyStore.Core.Settings;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Infrastructure.Data;
using BIF.ToyStore.Infrastructure.Repositories;
using BIF.ToyStore.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BIF.ToyStore.Tests.Services
{
    public class ConfigServiceTests : IDisposable
    {
        private readonly AppDbContext _dbContext;
        private readonly IMemoryCache _memoryCache;
        private readonly ConfigService _configService;
        private readonly InMemoryLocalSettingsService _localSettingsService;

        public ConfigServiceTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _dbContext = new AppDbContext(options);
            _memoryCache = new MemoryCache(new MemoryCacheOptions());
            var configRepository = new ConfigRepository(_dbContext);
            _localSettingsService = new InMemoryLocalSettingsService();
            var runtimeSettingsService = new RuntimeSettingsService(_localSettingsService, new DatabasePathService());
            _configService = new ConfigService(configRepository, _memoryCache, runtimeSettingsService);
        }

        public void Dispose()
        {
            _memoryCache.Dispose();
            _dbContext.Dispose();
        }

        [Fact]
        public async Task GetConfigAsync_ConfigMissing_CreatesDefaultSingleton()
        {
            var config = await _configService.GetConfigAsync();

            Assert.NotNull(config);
            Assert.Equal(1, config.Id);

            var inDb = await _dbContext.AppConfigs.SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal(1, inDb.Id);
        }

        [Fact]
        public async Task GetConfigAsync_ReadsFromCache_OnSecondCall()
        {
            _dbContext.AppConfigs.Add(new AppConfig
            {
                Id = 1,
                DisplayName = "Initial",
                TaxRate = 0.1m,
                LocalServerPort = 5000,
                DatabasePath = "ToyStore.db"
            });
            await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

            var first = await _configService.GetConfigAsync();

            var dbConfig = await _dbContext.AppConfigs.SingleAsync(TestContext.Current.CancellationToken);
            dbConfig.DisplayName = "ChangedInDb";
            await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

            var second = await _configService.GetConfigAsync();

            Assert.Equal("Initial", first.DisplayName);
            Assert.Equal("Initial", second.DisplayName);
        }

        [Fact]
        public async Task GetConfigAsync_AppliesRuntimeOverrides_ForPortAndDatabasePath()
        {
            _dbContext.AppConfigs.Add(new AppConfig
            {
                Id = 1,
                DisplayName = "Initial",
                TaxRate = 0.1m,
                LocalServerPort = 5000,
                DatabasePath = "ToyStore.db"
            });
            await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

            _localSettingsService.SetInt("LocalServerPort", 5055);
            _localSettingsService.SetString("DatabasePath", "custom.db");

            var config = await _configService.GetConfigAsync();

            Assert.Equal(5055, config.LocalServerPort);
            Assert.Equal("custom.db", config.DatabasePath);
        }

        [Fact]
        public async Task UpdateConfigAsync_UpdatesDatabaseCache_AndRuntimeSettings()
        {
            await _configService.GetConfigAsync();

            var updated = await _configService.UpdateConfigAsync("Store A", 0.08m, 6001, "custom.db");
            var readBack = await _configService.GetConfigAsync();

            Assert.Equal(1, updated.Id);
            Assert.Equal("Store A", updated.DisplayName);
            Assert.Equal(0.08m, updated.TaxRate);
            Assert.Equal(6001, updated.LocalServerPort);
            Assert.Equal("custom.db", updated.DatabasePath);

            Assert.Equal("Store A", readBack.DisplayName);
            Assert.Equal(0.08m, readBack.TaxRate);
            Assert.Equal(6001, _localSettingsService.GetInt("LocalServerPort", -1));
            Assert.Equal("custom.db", _localSettingsService.GetString("DatabasePath"));
        }

        [Fact]
        public async Task UpdateStoreSettingsAsync_UpdatesTaxCurrencyAndReceipts()
        {
            await _configService.GetConfigAsync();

            var updated = await _configService.UpdateStoreSettingsAsync(
                "Store Name",
                0.07m,
                "USD",
                "Header",
                "Footer",
                "Dark");

            Assert.Equal(0.07m, updated.TaxRate);
            Assert.Equal("USD", updated.CurrencySymbol);
            Assert.Equal("Header", updated.ReceiptHeader);
            Assert.Equal("Footer", updated.ReceiptFooter);
            Assert.Equal("Store Name", updated.DisplayName);
            Assert.Equal("Dark", updated.ThemePreference);
        }

        [Fact]
        public async Task IsInitialSetupCompletedAsync_DefaultConfig_ReturnsFalse()
        {
            var result = await _configService.IsInitialSetupCompletedAsync();

            Assert.False(result);
        }

        [Fact]
        public async Task CompleteInitialSetupAsync_UpdatesSetupFields_AndMarksCompleted()
        {
            await _configService.GetConfigAsync();

            var completed = await _configService.CompleteInitialSetupAsync(new InitialSetupConfiguration
            {
                DisplayName = "My Store",
                ReceiptHeader = "Header",
                ReceiptFooter = "Footer",
                CurrencySymbol = "USD",
                ThemePreference = "Dark",
                EnableLoyaltyPoints = true,
                TaxRate = 0.075m
            });

            Assert.True(completed.IsInitialSetupCompleted);
            Assert.Equal("My Store", completed.DisplayName);
            Assert.Equal("Header", completed.ReceiptHeader);
            Assert.Equal("Footer", completed.ReceiptFooter);
            Assert.Equal("USD", completed.CurrencySymbol);
            Assert.Equal("Dark", completed.ThemePreference);
            Assert.True(completed.EnableLoyaltyPoints);
            Assert.Equal(0.075m, completed.TaxRate);

            var isCompleted = await _configService.IsInitialSetupCompletedAsync();
            Assert.True(isCompleted);
        }

        private sealed class InMemoryLocalSettingsService : ILocalSettingsService
        {
            private readonly Dictionary<string, string> _stringValues = new(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<string, int> _intValues = new(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<string, bool> _boolValues = new(StringComparer.OrdinalIgnoreCase);

            public void SetString(string key, string value)
            {
                _stringValues[key] = value;
            }

            public string GetString(string key, string defaultValue = "")
            {
                return _stringValues.TryGetValue(key, out var value) ? value : defaultValue;
            }

            public void SetInt(string key, int value)
            {
                _intValues[key] = value;
            }

            public int GetInt(string key, int defaultValue)
            {
                return _intValues.TryGetValue(key, out var value) ? value : defaultValue;
            }

            public void SetBool(string key, bool value)
            {
                _boolValues[key] = value;
            }

            public bool GetBool(string key, bool defaultValue)
            {
                return _boolValues.TryGetValue(key, out var value) ? value : defaultValue;
            }
        }
    }
}
