using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.ViewModels.Base;
using BIF.ToyStore.ViewModels.Utils;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BIF.ToyStore.ViewModels.Pages
{
    public partial class InitialSetupViewModel : BaseViewModel
    {
        private readonly IGraphQLClient _graphQLClient;
        private readonly ILocalSettingsService _localSettingsService;

        [ObservableProperty]
        private string _storeName = "BIF Toy Store";

        [ObservableProperty]
        private string _receiptHeader = "Welcome to BIF Toy Store";

        [ObservableProperty]
        private string _receiptFooter = "Thank you for your purchase!";

        [ObservableProperty]
        private string _selectedCurrency = "USD";

        [ObservableProperty]
        private string _adminUsername = string.Empty;

        [ObservableProperty]
        private string _adminPassword = string.Empty;

        [ObservableProperty]
        private string _confirmAdminPassword = string.Empty;

        [ObservableProperty]
        private double _taxRate = 10.0;

        [ObservableProperty]
        private double _localServerPort = 5000;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private string _appVersion = string.Empty;

        public IReadOnlyList<string> CurrencyOptions { get; } = ["VND", "USD"];

        public InitialSetupViewModel(
            IGraphQLClient graphQLClient,
            ILocalSettingsService localSettingsService,
            IAppInfoService appInfoService)
        {
            _graphQLClient = graphQLClient;
            _localSettingsService = localSettingsService;

            Title = "Initial Setup";
            AppVersion = appInfoService.GetAppVersion();
            LocalServerPort = _localSettingsService.GetInt(AppPreferenceKeys.LocalServerPort, 5000);
        }

        public async Task InitializeAsync()
        {
            const string query = @"query GetAppConfig {
                appConfig {
                    displayName
                    receiptHeader
                    receiptFooter
                    currencySymbol
                    taxRate
                }
            }";

            try
            {
                var config = await _graphQLClient.ExecuteAsync<SetupConfigView>(query, dataKey: "appConfig");
                if (config is null)
                {
                    return;
                }

                StoreName = config.DisplayName;
                ReceiptHeader = config.ReceiptHeader;
                ReceiptFooter = config.ReceiptFooter;
                SelectedCurrency = CurrencyOptions.Contains(config.CurrencySymbol)
                    ? config.CurrencySymbol
                    : "USD";
                TaxRate = (double)(config.TaxRate * 100m);
            }
            catch (Exception ex)
            {
                ErrorMessage = "Unable to load defaults: " + ex.Message;
            }
        }

        [RelayCommand]
        public async Task<SetupSaveResult> SaveConfigurationAsync()
        {
            ErrorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(StoreName))
            {
                return SetupSaveResult.Fail("Store Name is required.");
            }

            if (TaxRate < 0 || TaxRate > 100)
            {
                return SetupSaveResult.Fail("Tax rate must be between 0 and 100.");
            }

            if (string.IsNullOrWhiteSpace(AdminUsername))
            {
                return SetupSaveResult.Fail("Admin username is required.");
            }

            if (string.IsNullOrWhiteSpace(AdminPassword))
            {
                return SetupSaveResult.Fail("Admin password is required.");
            }

            if (AdminPassword != ConfirmAdminPassword)
            {
                return SetupSaveResult.Fail("Admin passwords do not match.");
            }

            if (!CurrencyOptions.Contains(SelectedCurrency))
            {
                return SetupSaveResult.Fail("Please select a supported currency.");
            }

            var nextPort = (int)Math.Round(LocalServerPort);
            if (nextPort is < 1024 or > 65535)
            {
                return SetupSaveResult.Fail("Server port must be between 1024 and 65535.");
            }

            const string mutation = @"mutation CompleteInitialSetup($input: InitialSetupInput!) {
                completeInitialSetup(input: $input) {
                    isInitialSetupCompleted
                }
            }";

            const string createAdminMutation = @"mutation CreateSetupAdmin($user: String!, $pass: String!, $role: UserRole!) {
                createUser(username: $user, password: $pass, role: $role) {
                    id
                }
            }";

            var variables = new
            {
                input = new
                {
                    displayName = StoreName,
                    receiptHeader = ReceiptHeader,
                    receiptFooter = ReceiptFooter,
                    currencySymbol = SelectedCurrency,
                    themePreference = "System",
                    enableLoyaltyPoints = false,
                    taxRate = decimal.Round((decimal)TaxRate / 100m, 4)
                }
            };

            int? createdAdminUserId = null;

            try
            {
                var createdAdmin = await _graphQLClient.ExecuteAsync<LoginUser>(
                    createAdminMutation,
                    new
                    {
                        user = AdminUsername.Trim(),
                        pass = AdminPassword,
                        role = "ADMIN"
                    },
                    dataKey: "createUser");

                if (createdAdmin is null)
                {
                    return SetupSaveResult.Fail("Unable to create admin account.");
                }

                createdAdminUserId = createdAdmin.Id;

                var result = await _graphQLClient.ExecuteAsync<SetupStateView>(mutation, variables, dataKey: "completeInitialSetup");
                if (result is null || !result.IsInitialSetupCompleted)
                {
                    if (createdAdminUserId.HasValue)
                    {
                        await TryRollbackCreatedAdminAsync(createdAdminUserId.Value);
                    }

                    return SetupSaveResult.Fail("Server did not confirm setup completion.");
                }

                var previousPort = _localSettingsService.GetInt(AppPreferenceKeys.LocalServerPort, 5000);
                _localSettingsService.SetInt(AppPreferenceKeys.LocalServerPort, nextPort);
                _localSettingsService.SetString(AppPreferenceKeys.StoreName, StoreName);

                return SetupSaveResult.Success(previousPort != nextPort);
            }
            catch (Exception ex)
            {
                if (createdAdminUserId.HasValue)
                {
                    await TryRollbackCreatedAdminAsync(createdAdminUserId.Value);
                }

                return SetupSaveResult.Fail("Save failed: " + ex.Message);
            }
        }

        private async Task TryRollbackCreatedAdminAsync(int userId)
        {
            const string deleteMutation = @"mutation DeleteSetupAdmin($id: Int!) {
                deleteUser(id: $id)
            }";

            try
            {
                await _graphQLClient.ExecuteAsync<bool>(deleteMutation, new { id = userId }, dataKey: "deleteUser");
            }
            catch
            {
                // Best-effort rollback only.
            }
        }

        public sealed class SetupSaveResult
        {
            public bool IsSuccessful { get; init; }
            public bool RequiresRestart { get; init; }
            public string ErrorMessage { get; init; } = string.Empty;

            public static SetupSaveResult Success(bool requiresRestart) =>
                new() { IsSuccessful = true, RequiresRestart = requiresRestart };

            public static SetupSaveResult Fail(string error) =>
                new() { IsSuccessful = false, ErrorMessage = error };
        }

        public sealed class SetupConfigView
        {
            public string DisplayName { get; set; } = string.Empty;
            public string ReceiptHeader { get; set; } = string.Empty;
            public string ReceiptFooter { get; set; } = string.Empty;
            public string CurrencySymbol { get; set; } = "USD";
            public decimal TaxRate { get; set; }
        }

        public sealed class SetupStateView
        {
            public bool IsInitialSetupCompleted { get; set; }
        }
    }
}
