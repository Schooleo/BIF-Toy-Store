using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.ViewModels.Messages;
using BIF.ToyStore.ViewModels.Utils;
using BIF.ToyStore.WinUI.Views;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
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

        public string CurrentUsername
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_currentUser?.Username))
                {
                    return _currentUser.Username;
                }

                return _localSettingsService.GetString("LastUsername", "Unknown User");
            }
        }

        public string CurrentUserRoleLabel
        {
            get
            {
                if (_currentUser is not null)
                {
                    return _currentUser.Role == Core.Enums.UserRole.Admin ? "ADMIN" : "SALE";
                }

                var role = _localSettingsService.GetString(AppPreferenceKeys.CurrentUserRole, string.Empty);
                return string.Equals(role, Core.Enums.UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase)
                    ? "ADMIN"
                    : "SALE";
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            _localSettingsService = App.Current.Services.GetRequiredService<ILocalSettingsService>();
            _credentialVaultService = App.Current.Services.GetRequiredService<ICredentialVaultService>();
            _scopeFactory = App.Current.Services.GetRequiredService<IServiceScopeFactory>();
            WeakReferenceMessenger.Default.Register(this);

            WindowRoot.AddHandler(
                UIElement.PointerPressedEvent,
                new PointerEventHandler(WindowRoot_PointerPressed),
                true);
        }

        private void WindowRoot_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (e.OriginalSource is not DependencyObject source || IsTextInputElement(source))
            {
                return;
            }

            if (FindAncestor<SettingsPage>(source) is not null)
            {
                return;
            }

            if (FocusManager.GetFocusedElement(rootFrame.XamlRoot) is not DependencyObject focused
                || !IsTextInputElement(focused))
            {
                return;
            }

            FocusSink.Focus(FocusState.Programmatic);
        }

        private static bool IsTextInputElement(DependencyObject? element)
        {
            while (element is not null)
            {
                if (element is TextBox
                    || element is PasswordBox
                    || element is AutoSuggestBox
                    || element is NumberBox
                    || element is RichEditBox)
                {
                    return true;
                }

                element = VisualTreeHelper.GetParent(element);
            }

            return false;
        }

        private static T? FindAncestor<T>(DependencyObject? element) where T : DependencyObject
        {
            while (element is not null)
            {
                if (element is T typed)
                {
                    return typed;
                }

                element = VisualTreeHelper.GetParent(element);
            }

            return null;
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

        public void NavigateToPos()
        {
            var shell = EnsureShell();
            shell.NavigateToPos();
            _localSettingsService.SetString(AppPreferenceKeys.LastActiveRoute, "POS");
        }

        public void NavigateToOrders()
        {
            var shell = EnsureShell();
            shell.SetAdminMode(IsCurrentUserAdmin);
            shell.NavigateToOrders();
            _localSettingsService.SetString(AppPreferenceKeys.LastActiveRoute, "Orders");
        }

        public void NavigateToReports()
        {
            if (!IsCurrentUserAdmin)
            {
                NavigateToDashboard();
                return;
            }

            var shell = EnsureShell();
            shell.SetAdminMode(IsCurrentUserAdmin);
            shell.NavigateToReports();
            _localSettingsService.SetString(AppPreferenceKeys.LastActiveRoute, "Reports");
        }

        public async Task LogoutAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

            await authService.LogoutAsync();

            _credentialVaultService.ClearCredentials(CredentialResourceName);
            _currentUser = null;
            _localSettingsService.SetString(AppPreferenceKeys.CurrentUserRole, string.Empty);
            _localSettingsService.SetInt(AppPreferenceKeys.CurrentUserId, 0);
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

        public void NavigateToProducts()
        {
            var shell = EnsureShell();
            shell.SetAdminMode(IsCurrentUserAdmin);
            shell.NavigateToProducts();
            _localSettingsService.SetString(AppPreferenceKeys.LastActiveRoute, "Products");
        }

        public void NavigateToCategories()
        {
            var shell = EnsureShell();
            shell.SetAdminMode(IsCurrentUserAdmin);
            shell.NavigateToCategories();
            _localSettingsService.SetString(AppPreferenceKeys.LastActiveRoute, "Categories");
        }

        public void NavigateToSettings()
        {
            if (!IsCurrentUserAdmin)
            {
                NavigateToDashboard();
                return;
            }

            var shell = EnsureShell();
            shell.SetAdminMode(IsCurrentUserAdmin);
            shell.NavigateToSettings();
            _localSettingsService.SetString(AppPreferenceKeys.LastActiveRoute, "Settings");
        }

        public void RefreshShellStoreName()
        {
            if (rootFrame.Content is AppShellPage shell)
            {
                shell.RefreshStoreName();
            }
        }

        public void Receive(LoginSucceededMessage message)
        {
            _currentUser = message.Value;

            var startOnLastOpened = _localSettingsService.GetBool(AppPreferenceKeys.StartOnLastOpened, false);
            if (!startOnLastOpened)
            {
                NavigateToDashboard();
                _localSettingsService.SetString(AppPreferenceKeys.LastActiveRoute, "Dashboard");
                return;
            }

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

            if (route == "Products")
            {
                NavigateToProducts();
                return;
            }

            if (route == "Categories")
            {
                NavigateToCategories();
                return;
            }

            if (route == "Orders")
            {
                NavigateToOrders();
                return;
            }

            if (route == "Reports")
            {
                if (IsCurrentUserAdmin)
                {
                    NavigateToReports();
                }
                else
                {
                    NavigateToDashboard();
                    _localSettingsService.SetString(AppPreferenceKeys.LastActiveRoute, "Dashboard");
                }
                return;
            }

            if (route == "Settings")
            {
                if (IsCurrentUserAdmin)
                {
                    NavigateToSettings();
                }
                else
                {
                    NavigateToDashboard();
                    _localSettingsService.SetString(AppPreferenceKeys.LastActiveRoute, "Dashboard");
                }
                return;
            }

            NavigateToDashboard();
            _localSettingsService.SetString(AppPreferenceKeys.LastActiveRoute, "Dashboard");
        }
    }
}
