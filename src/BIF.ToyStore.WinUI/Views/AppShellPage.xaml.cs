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
            ShellHeader.UserRole = _isAdmin ? "ADMIN" : "SALE";

            if (!_isAdmin && ShellSidebar.ActiveTab == "Users")
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
