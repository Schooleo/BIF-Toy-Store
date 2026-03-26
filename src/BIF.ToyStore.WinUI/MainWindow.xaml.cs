using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.ViewModels.Messages;
using BIF.ToyStore.ViewModels.Utils;
using BIF.ToyStore.WinUI.Views;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using System;

namespace BIF.ToyStore.WinUI
{
    public sealed partial class MainWindow : Window, IRecipient<LoginSucceededMessage>
    {
        private readonly ILocalSettingsService _localSettingsService;
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
            rootFrame.Navigate(typeof(DashboardPage));
        }

        public void NavigateToUsers()
        {
            if (!IsCurrentUserAdmin)
            {
                NavigateToDashboard();
                return;
            }

            rootFrame.Navigate(typeof(UsersPage));
            _localSettingsService.SetString(AppPreferenceKeys.LastActiveRoute, "Users");
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
