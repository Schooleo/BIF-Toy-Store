using BIF.ToyStore.Core.Settings;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.Infrastructure.Data;
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

        public ConfigServiceTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _dbContext = new AppDbContext(options);
            _memoryCache = new MemoryCache(new MemoryCacheOptions());
            _configService = new ConfigService(_dbContext, _memoryCache);
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

            var inDb = await _dbContext.AppConfigs.SingleAsync();
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
            await _dbContext.SaveChangesAsync();

            var first = await _configService.GetConfigAsync();

            var dbConfig = await _dbContext.AppConfigs.SingleAsync();
            dbConfig.DisplayName = "ChangedInDb";
            await _dbContext.SaveChangesAsync();

            var second = await _configService.GetConfigAsync();

            Assert.Equal("Initial", first.DisplayName);
            Assert.Equal("Initial", second.DisplayName);
        }

        [Fact]
        public async Task UpdateConfigAsync_UpdatesDatabase_AndCache()
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
                ThemePreference = "Dark",
                EnableLoyaltyPoints = true,
                TaxRate = 0.075m
            });

            Assert.True(completed.IsInitialSetupCompleted);
            Assert.Equal("My Store", completed.DisplayName);
            Assert.Equal("Header", completed.ReceiptHeader);
            Assert.Equal("Footer", completed.ReceiptFooter);
            Assert.Equal("Dark", completed.ThemePreference);
            Assert.True(completed.EnableLoyaltyPoints);
            Assert.Equal(0.075m, completed.TaxRate);

            var isCompleted = await _configService.IsInitialSetupCompletedAsync();
            Assert.True(isCompleted);
        }
    }
}
