using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.ViewModels.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

namespace BIF.ToyStore.WinUI.Views
{
    public sealed partial class AppShellPage : Page
    {
        private bool _isAdmin;
        private readonly ILocalSettingsService _localSettingsService;
        private readonly IGraphQLClient _graphQLClient;

        public AppShellPage()
        {
            InitializeComponent();
            _localSettingsService = App.Current.Services.GetRequiredService<ILocalSettingsService>();
            _graphQLClient = App.Current.Services.GetRequiredService<IGraphQLClient>();
            RefreshStoreName();
            Loaded += OnLoaded;
        }

        public void RefreshStoreName()
        {
            ShellSidebar.StoreName = _localSettingsService.GetString(AppPreferenceKeys.StoreName, "BIF Toy Store");
        }

        public void SetAdminMode(bool isAdmin)
        {
            _isAdmin = isAdmin;
            ShellSidebar.IsAdmin = _isAdmin;
            ShellHeader.UserRole = _isAdmin ? "ADMIN" : "SALE";

            if (!_isAdmin && (ShellSidebar.ActiveTab == "Users" || ShellSidebar.ActiveTab == "Reports" || ShellSidebar.ActiveTab == "Settings"))
            {
                NavigateToDashboard();
            }
        }

        public void NavigateToDashboard()
        {
            ShellSidebar.ActiveTab = "Dashboard";
            NavigateContent(typeof(DashboardPage));
        }

        public void NavigateToUsers()
        {
            if (!_isAdmin)
            {
                NavigateToDashboard();
                return;
            }

            ShellSidebar.ActiveTab = "Users";
            NavigateContent(typeof(UsersPage));
        }

        public void NavigateToPos()
        {
            ShellSidebar.ActiveTab = "POS";
            NavigateContent(typeof(POSPage));
        }

        public void NavigateToOrders()
        {
            ShellSidebar.ActiveTab = "Orders";
            NavigateContent(typeof(OrderPage));
        }

        public void NavigateToReports()
        {
            if (!_isAdmin)
            {
                NavigateToDashboard();
                return;
            }

            ShellSidebar.ActiveTab = "Reports";
            NavigateContent(typeof(ReportsPage));
        }

        public void NavigateToProducts()
        {
            ShellSidebar.ActiveTab = "Products";
            NavigateContent(typeof(ProductsPage));
        }

        public void NavigateToCategories()
        {
            ShellSidebar.ActiveTab = "Categories";
            NavigateContent(typeof(CategoriesPage));
        }

        public void NavigateToSettings()
        {
            if (!_isAdmin)
            {
                NavigateToDashboard();
                return;
            }

            ShellSidebar.ActiveTab = "Settings";
            NavigateContent(typeof(SettingsPage));
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            await EnsureStoreNameAsync();

            if (ContentFrame.Content is null)
            {
                NavigateToDashboard();
            }
        }

        private async Task EnsureStoreNameAsync()
        {
            var cachedStoreName = _localSettingsService.GetString(AppPreferenceKeys.StoreName, string.Empty);
            if (!string.IsNullOrWhiteSpace(cachedStoreName))
            {
                ShellSidebar.StoreName = cachedStoreName;
                return;
            }

            const string query = @"query GetShellStoreName {
                appConfig {
                    displayName
                }
            }";

            try
            {
                var config = await _graphQLClient.ExecuteAsync<ShellStoreConfig>(query, dataKey: "appConfig");
                if (config is null || string.IsNullOrWhiteSpace(config.DisplayName))
                {
                    return;
                }

                var resolvedStoreName = config.DisplayName.Trim();
                _localSettingsService.SetString(AppPreferenceKeys.StoreName, resolvedStoreName);
                ShellSidebar.StoreName = resolvedStoreName;
            }
            catch
            {
                // Keep startup resilient; sidebar already has a fallback display name.
            }
        }

        private void NavigateContent(Type targetPage)
        {
            if (ContentFrame.Content?.GetType() == targetPage)
            {
                return;
            }

            ContentFrame.Navigate(targetPage);
        }

        private sealed class ShellStoreConfig
        {
            public string DisplayName { get; set; } = string.Empty;
        }
    }
}
