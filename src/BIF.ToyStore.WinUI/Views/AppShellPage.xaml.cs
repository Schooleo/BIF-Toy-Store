using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace BIF.ToyStore.WinUI.Views
{
    public sealed partial class AppShellPage : Page
    {
        private bool _isAdmin;

        public AppShellPage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
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

        public void NavigateToProducts()
        {
            ShellSidebar.ActiveTab = "Products";
            NavigateContent(typeof(ProductsPage));
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

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;

            if (ContentFrame.Content is null)
            {
                NavigateToDashboard();
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
    }
}
