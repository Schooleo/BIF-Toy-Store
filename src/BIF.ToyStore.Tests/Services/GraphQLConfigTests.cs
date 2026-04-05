using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.Core.Settings;
using BIF.ToyStore.Infrastructure.GraphQL;
using Moq;

namespace BIF.ToyStore.Tests.Services
{
    public class GraphQLConfigTests
    {
        [Fact]
        public async Task Queries_SetupState_ReturnsCompletionFlag()
        {
            var configServiceMock = new Mock<IConfigService>();
            configServiceMock
                .Setup(x => x.IsInitialSetupCompletedAsync())
                .ReturnsAsync(true);

            var queries = new Queries();

            var result = await queries.SetupState(configServiceMock.Object);

            Assert.True(result.IsInitialSetupCompleted);
        }

        [Fact]
        public async Task Mutations_CompleteInitialSetup_MapsInputAndReturnsPayload()
        {
            var configServiceMock = new Mock<IConfigService>();
            configServiceMock
                .Setup(x => x.CompleteInitialSetupAsync(It.IsAny<InitialSetupConfiguration>()))
                .ReturnsAsync(new AppConfig
                {
                    Id = 1,
                    DisplayName = "Configured Store",
                    ReceiptHeader = "Header",
                    ReceiptFooter = "Footer",
                    CurrencySymbol = "USD",
                    ThemePreference = "Dark",
                    EnableLoyaltyPoints = true,
                    TaxRate = 0.05m,
                    LocalServerPort = 5000,
                    DatabasePath = "ToyStore.db",
                    IsInitialSetupCompleted = true
                });

            var mutation = new Mutations();

            var result = await mutation.CompleteInitialSetup(new InitialSetupInput
            {
                DisplayName = "Configured Store",
                ReceiptHeader = "Header",
                ReceiptFooter = "Footer",
                CurrencySymbol = "USD",
                ThemePreference = "Dark",
                EnableLoyaltyPoints = true,
                TaxRate = 0.05m
            }, configServiceMock.Object);

            Assert.Equal("Configured Store", result.DisplayName);
            Assert.Equal("USD", result.CurrencySymbol);
            Assert.True(result.IsInitialSetupCompleted);

            configServiceMock.Verify(x => x.CompleteInitialSetupAsync(It.Is<InitialSetupConfiguration>(cfg =>
                cfg.DisplayName == "Configured Store"
                && cfg.CurrencySymbol == "USD"
                && cfg.ThemePreference == "Dark"
                && cfg.TaxRate == 0.05m)), Times.Once);
        }

        [Fact]
        public async Task Mutations_UpdateStoreSettings_MapsInputAndReturnsPayload()
        {
            var configServiceMock = new Mock<IConfigService>();
            configServiceMock
                .Setup(x => x.UpdateStoreSettingsAsync("Configured Store", 0.08m, "USD", "Hdr", "Ftr", "Dark"))
                .ReturnsAsync(new AppConfig
                {
                    Id = 1,
                    DisplayName = "Configured Store",
                    ReceiptHeader = "Hdr",
                    ReceiptFooter = "Ftr",
                    CurrencySymbol = "USD",
                    ThemePreference = "Dark",
                    EnableLoyaltyPoints = true,
                    TaxRate = 0.08m,
                    LocalServerPort = 5000,
                    DatabasePath = "ToyStore.db",
                    IsInitialSetupCompleted = true
                });

            var mutation = new Mutations();

            var result = await mutation.UpdateStoreSettings(new UpdateStoreSettingsInput
            {
                DisplayName = "Configured Store",
                TaxRate = 0.08m,
                CurrencySymbol = "USD",
                ReceiptHeader = "Hdr",
                ReceiptFooter = "Ftr",
                ThemePreference = "Dark"
            }, configServiceMock.Object);

            Assert.Equal("Configured Store", result.DisplayName);
            Assert.Equal(0.08m, result.TaxRate);
            Assert.Equal("USD", result.CurrencySymbol);
            Assert.Equal("Hdr", result.ReceiptHeader);
            Assert.Equal("Ftr", result.ReceiptFooter);
            Assert.Equal("Dark", result.ThemePreference);
        }
    }
}
