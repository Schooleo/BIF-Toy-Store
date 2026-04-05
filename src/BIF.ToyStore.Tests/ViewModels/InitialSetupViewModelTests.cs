using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.ViewModels.Pages;
using BIF.ToyStore.ViewModels.Utils;
using Moq;

namespace BIF.ToyStore.Tests.ViewModels.Pages
{
    public class InitialSetupViewModelTests
    {
        private readonly Mock<IGraphQLClient> _graphQlClientMock;
        private readonly Mock<ILocalSettingsService> _localSettingsServiceMock;
        private readonly Mock<IAppInfoService> _appInfoServiceMock;
        private readonly InitialSetupViewModel _viewModel;

        public InitialSetupViewModelTests()
        {
            _graphQlClientMock = new Mock<IGraphQLClient>();
            _localSettingsServiceMock = new Mock<ILocalSettingsService>();
            _appInfoServiceMock = new Mock<IAppInfoService>();

            _localSettingsServiceMock
                .Setup(x => x.GetInt(AppPreferenceKeys.LocalServerPort, 5000))
                .Returns(5000);
            _appInfoServiceMock
                .Setup(x => x.GetAppVersion())
                .Returns("Version 1.0.0.0");

            _viewModel = new InitialSetupViewModel(
                _graphQlClientMock.Object,
                _localSettingsServiceMock.Object,
                _appInfoServiceMock.Object);
        }

        private void SetValidAdminCredentials()
        {
            _viewModel.AdminUsername = "owner";
            _viewModel.AdminPassword = "owner123";
            _viewModel.ConfirmAdminPassword = "owner123";
        }

        [Fact]
        public void Constructor_SetsTitleAndVersion()
        {
            Assert.Equal("Initial Setup", _viewModel.Title);
            Assert.Equal("Version 1.0.0.0", _viewModel.AppVersion);
            Assert.Equal(5000, _viewModel.LocalServerPort);
        }

        [Fact]
        public async Task SaveConfigurationAsync_EmptyStoreName_ReturnsValidationError()
        {
            _viewModel.StoreName = "";

            var result = await _viewModel.SaveConfigurationAsync();

            Assert.False(result.IsSuccessful);
            Assert.Equal("Store Name is required.", result.ErrorMessage);

            _graphQlClientMock.Verify(
                x => x.ExecuteAsync<InitialSetupViewModel.SetupStateView>(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task SaveConfigurationAsync_InvalidTaxRate_ReturnsValidationError()
        {
            _viewModel.StoreName = "Store";
            _viewModel.TaxRate = 120;

            var result = await _viewModel.SaveConfigurationAsync();

            Assert.False(result.IsSuccessful);
            Assert.Equal("Tax rate must be between 0 and 100.", result.ErrorMessage);
        }

        [Fact]
        public async Task SaveConfigurationAsync_InvalidPort_ReturnsValidationError()
        {
            _viewModel.StoreName = "Store";
            _viewModel.TaxRate = 10;
            _viewModel.LocalServerPort = 80;
            SetValidAdminCredentials();

            var result = await _viewModel.SaveConfigurationAsync();

            Assert.False(result.IsSuccessful);
            Assert.Equal("Server port must be between 1024 and 65535.", result.ErrorMessage);
        }

        [Fact]
        public async Task SaveConfigurationAsync_UnsupportedCurrency_ReturnsValidationError()
        {
            _viewModel.StoreName = "Store";
            _viewModel.TaxRate = 10;
            _viewModel.LocalServerPort = 5000;
            _viewModel.SelectedCurrency = "EUR";
            SetValidAdminCredentials();

            var result = await _viewModel.SaveConfigurationAsync();

            Assert.False(result.IsSuccessful);
            Assert.Equal("Please select a supported currency.", result.ErrorMessage);
        }

        [Fact]
        public async Task SaveConfigurationAsync_EmptyAdminUsername_ReturnsValidationError()
        {
            _viewModel.StoreName = "Store";
            _viewModel.TaxRate = 10;
            _viewModel.LocalServerPort = 5000;
            _viewModel.AdminUsername = string.Empty;
            _viewModel.AdminPassword = "owner123";
            _viewModel.ConfirmAdminPassword = "owner123";

            var result = await _viewModel.SaveConfigurationAsync();

            Assert.False(result.IsSuccessful);
            Assert.Equal("Admin username is required.", result.ErrorMessage);
        }

        [Fact]
        public async Task SaveConfigurationAsync_AdminPasswordMismatch_ReturnsValidationError()
        {
            _viewModel.StoreName = "Store";
            _viewModel.TaxRate = 10;
            _viewModel.LocalServerPort = 5000;
            _viewModel.AdminUsername = "owner";
            _viewModel.AdminPassword = "owner123";
            _viewModel.ConfirmAdminPassword = "different";

            var result = await _viewModel.SaveConfigurationAsync();

            Assert.False(result.IsSuccessful);
            Assert.Equal("Admin passwords do not match.", result.ErrorMessage);
        }

        [Fact]
        public async Task SaveConfigurationAsync_ValidInput_WithSamePort_ReturnsSuccessWithoutRestart()
        {
            _viewModel.StoreName = "Store";
            _viewModel.ReceiptHeader = "Header";
            _viewModel.ReceiptFooter = "Footer";
            _viewModel.TaxRate = 10;
            _viewModel.LocalServerPort = 5000;
            _viewModel.SelectedCurrency = "USD";
            SetValidAdminCredentials();

            _graphQlClientMock
                .Setup(x => x.ExecuteAsync<LoginUser>(
                    It.Is<string>(s => s.Contains("CreateSetupAdmin")),
                    It.IsAny<object>(),
                    "createUser"))
                .ReturnsAsync(new LoginUser
                {
                    Id = 99,
                    Username = "owner"
                });

            _graphQlClientMock
                .Setup(x => x.ExecuteAsync<InitialSetupViewModel.SetupStateView>(
                    It.Is<string>(s => s.Contains("completeInitialSetup")),
                    It.IsAny<object>(),
                    "completeInitialSetup"))
                .ReturnsAsync(new InitialSetupViewModel.SetupStateView
                {
                    IsInitialSetupCompleted = true
                });

            var result = await _viewModel.SaveConfigurationAsync();

            Assert.True(result.IsSuccessful);
            Assert.False(result.RequiresRestart);
            _localSettingsServiceMock.Verify(x => x.SetInt(AppPreferenceKeys.LocalServerPort, 5000), Times.Once);
            _graphQlClientMock.Verify(x => x.ExecuteAsync<LoginUser>(
                It.IsAny<string>(),
                It.IsAny<object>(),
                "createUser"), Times.Once);
        }

        [Fact]
        public async Task SaveConfigurationAsync_ValidInput_WithPortChange_ReturnsSuccessWithRestart()
        {
            _viewModel.StoreName = "Store";
            _viewModel.TaxRate = 10;
            _viewModel.LocalServerPort = 5051;
            SetValidAdminCredentials();

            _graphQlClientMock
                .Setup(x => x.ExecuteAsync<LoginUser>(
                    It.Is<string>(s => s.Contains("CreateSetupAdmin")),
                    It.IsAny<object>(),
                    "createUser"))
                .ReturnsAsync(new LoginUser
                {
                    Id = 100,
                    Username = "owner"
                });

            _graphQlClientMock
                .Setup(x => x.ExecuteAsync<InitialSetupViewModel.SetupStateView>(
                    It.Is<string>(s => s.Contains("completeInitialSetup")),
                    It.IsAny<object>(),
                    "completeInitialSetup"))
                .ReturnsAsync(new InitialSetupViewModel.SetupStateView
                {
                    IsInitialSetupCompleted = true
                });

            var result = await _viewModel.SaveConfigurationAsync();

            Assert.True(result.IsSuccessful);
            Assert.True(result.RequiresRestart);
            _localSettingsServiceMock.Verify(x => x.SetInt(AppPreferenceKeys.LocalServerPort, 5051), Times.Once);
        }

        [Fact]
        public async Task SaveConfigurationAsync_GraphQlFailure_ReturnsFailureResult()
        {
            _viewModel.StoreName = "Store";
            _viewModel.TaxRate = 10;
            _viewModel.LocalServerPort = 5000;
            SetValidAdminCredentials();

            _graphQlClientMock
                .Setup(x => x.ExecuteAsync<LoginUser>(
                    It.IsAny<string>(),
                    It.IsAny<object>(),
                    "createUser"))
                .ReturnsAsync(new LoginUser
                {
                    Id = 111,
                    Username = "owner"
                });

            _graphQlClientMock
                .Setup(x => x.ExecuteAsync<InitialSetupViewModel.SetupStateView>(
                    It.Is<string>(s => s.Contains("completeInitialSetup")),
                    It.IsAny<object>(),
                    "completeInitialSetup"))
                .ThrowsAsync(new Exception("server unavailable"));

            _graphQlClientMock
                .Setup(x => x.ExecuteAsync<bool>(
                    It.Is<string>(s => s.Contains("DeleteSetupAdmin")),
                    It.IsAny<object>(),
                    "deleteUser"))
                .ReturnsAsync(true);

            var result = await _viewModel.SaveConfigurationAsync();

            Assert.False(result.IsSuccessful);
            Assert.Contains("Save failed:", result.ErrorMessage);
            _graphQlClientMock.Verify(x => x.ExecuteAsync<bool>(
                It.IsAny<string>(),
                It.IsAny<object>(),
                "deleteUser"), Times.Once);
        }

        [Fact]
        public async Task SaveConfigurationAsync_ServerDoesNotConfirm_ReturnsFailureResult()
        {
            _viewModel.StoreName = "Store";
            _viewModel.TaxRate = 10;
            _viewModel.LocalServerPort = 5000;
            SetValidAdminCredentials();

            _graphQlClientMock
                .Setup(x => x.ExecuteAsync<LoginUser>(
                    It.IsAny<string>(),
                    It.IsAny<object>(),
                    "createUser"))
                .ReturnsAsync(new LoginUser
                {
                    Id = 120,
                    Username = "owner"
                });

            _graphQlClientMock
                .Setup(x => x.ExecuteAsync<InitialSetupViewModel.SetupStateView>(
                    It.Is<string>(s => s.Contains("completeInitialSetup")),
                    It.IsAny<object>(),
                    "completeInitialSetup"))
                .ReturnsAsync(new InitialSetupViewModel.SetupStateView
                {
                    IsInitialSetupCompleted = false
                });

            _graphQlClientMock
                .Setup(x => x.ExecuteAsync<bool>(
                    It.Is<string>(s => s.Contains("DeleteSetupAdmin")),
                    It.IsAny<object>(),
                    "deleteUser"))
                .ReturnsAsync(true);

            var result = await _viewModel.SaveConfigurationAsync();

            Assert.False(result.IsSuccessful);
            Assert.Equal("Server did not confirm setup completion.", result.ErrorMessage);
            _graphQlClientMock.Verify(x => x.ExecuteAsync<bool>(
                It.IsAny<string>(),
                It.IsAny<object>(),
                "deleteUser"), Times.Once);
        }

        [Fact]
        public async Task InitializeAsync_MapsConfigurationFields()
        {
            _graphQlClientMock
                .Setup(x => x.ExecuteAsync<InitialSetupViewModel.SetupConfigView>(
                    It.Is<string>(s => s.Contains("query GetAppConfig")),
                    null,
                    "appConfig"))
                .ReturnsAsync(new InitialSetupViewModel.SetupConfigView
                {
                    DisplayName = "Setup Store",
                    ReceiptHeader = "Header",
                    ReceiptFooter = "Footer",
                    CurrencySymbol = "USD",
                    TaxRate = 0.08m
                });

            await _viewModel.InitializeAsync();

            Assert.Equal("Setup Store", _viewModel.StoreName);
            Assert.Equal("Header", _viewModel.ReceiptHeader);
            Assert.Equal("Footer", _viewModel.ReceiptFooter);
            Assert.Equal("USD", _viewModel.SelectedCurrency);
            Assert.Equal(8.0, _viewModel.TaxRate);
        }

        [Fact]
        public async Task InitializeAsync_GraphQlThrows_SetsErrorMessage()
        {
            _graphQlClientMock
                .Setup(x => x.ExecuteAsync<InitialSetupViewModel.SetupConfigView>(
                    It.Is<string>(s => s.Contains("query GetAppConfig")),
                    null,
                    "appConfig"))
                .ThrowsAsync(new Exception("boom"));

            await _viewModel.InitializeAsync();

            Assert.Contains("Unable to load defaults:", _viewModel.ErrorMessage);
        }
    }
}
