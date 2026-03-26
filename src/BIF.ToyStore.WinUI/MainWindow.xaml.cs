using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.ViewModels.Messages;
using BIF.ToyStore.ViewModels.Utils;
using BIF.ToyStore.WinUI.Views;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using System;
using System.Threading.Tasks;

namespace BIF.ToyStore.WinUI
{
    public sealed partial class MainWindow : Window, IRecipient<LoginSucceededMessage>
    {
        private const string CredentialResourceName = "BIF.ToyStore.POS";

        private readonly ILocalSettingsService _localSettingsService;
        private readonly ICredentialVaultService _credentialVaultService;
        private readonly IServiceScopeFactory _scopeFactory;
        private LoginUser? _currentUser;

        public bool IsCurrentUserAdmin
        {
            get
            {
                if (_currentUser is not null)
                {
                    return _currentUser.Role == Core.Enums.UserRole.Admin;
                }

                var role = _localSettingsService.GetString(AppPreferenceKeys.CurrentUserRole, string.Empty);
                return string.Equals(role, Core.Enums.UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            _localSettingsService = App.Current.Services.GetRequiredService<ILocalSettingsService>();
            _credentialVaultService = App.Current.Services.GetRequiredService<ICredentialVaultService>();
            _scopeFactory = App.Current.Services.GetRequiredService<IServiceScopeFactory>();
            WeakReferenceMessenger.Default.Register(this);
        }

        public void NavigateToInitialSetup()
        {
            rootFrame.Navigate(typeof(InitialSetupPage));
        }

        public void NavigateToLogin()
        {
            rootFrame.Navigate(typeof(LoginPage));
        }

        public void NavigateToDashboard()
        {
            var shell = EnsureShell();
            shell.SetAdminMode(IsCurrentUserAdmin);
            shell.NavigateToDashboard();
            _localSettingsService.SetString(AppPreferenceKeys.LastActiveRoute, "Dashboard");
        }

        public void NavigateToUsers()
        {
            if (!IsCurrentUserAdmin)
            {
                NavigateToDashboard();
                return;
            }

            var shell = EnsureShell();
            shell.SetAdminMode(true);
            shell.NavigateToUsers();
            _localSettingsService.SetString(AppPreferenceKeys.LastActiveRoute, "Users");
        }

        public async Task LogoutAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

            await authService.LogoutAsync();

            _credentialVaultService.ClearCredentials(CredentialResourceName);
            _currentUser = null;
            _localSettingsService.SetString(AppPreferenceKeys.CurrentUserRole, string.Empty);
            _localSettingsService.SetString(AppPreferenceKeys.LastActiveRoute, "Dashboard");

            NavigateToLogin();
        }

        private AppShellPage EnsureShell()
        {
            if (rootFrame.Content is AppShellPage shell)
            {
                return shell;
            }

            rootFrame.Navigate(typeof(AppShellPage));
            return (AppShellPage)rootFrame.Content;
        }

        public void Receive(LoginSucceededMessage message)
        {
            _currentUser = message.Value;

            var route = _localSettingsService.GetString(AppPreferenceKeys.LastActiveRoute, "Dashboard");
            if (route == "Users" && IsCurrentUserAdmin)
            {
                NavigateToUsers();
                return;
            }

            if (route == "Dashboard")
            {
                NavigateToDashboard();
                _localSettingsService.SetString(AppPreferenceKeys.LastActiveRoute, "Dashboard");
                return;
            }

            NavigateToDashboard();
            _localSettingsService.SetString(AppPreferenceKeys.LastActiveRoute, "Dashboard");
        }
    }
}
