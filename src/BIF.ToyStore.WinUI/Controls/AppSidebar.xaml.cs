using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;
using BIF.ToyStore.WinUI.Services;
using System;

namespace BIF.ToyStore.WinUI.Controls
{
    public sealed partial class AppSidebar : UserControl
    {
        private const int MaxStoreNameLength = 15;

        public static readonly DependencyProperty IsAdminProperty =
            DependencyProperty.Register(
                nameof(IsAdmin),
                typeof(bool),
                typeof(AppSidebar),
                new PropertyMetadata(true, OnIsAdminChanged));

        public static readonly DependencyProperty ActiveTabProperty =
            DependencyProperty.Register(
                nameof(ActiveTab),
                typeof(string),
                typeof(AppSidebar),
                new PropertyMetadata("Dashboard", OnActiveTabChanged));

        public static readonly DependencyProperty StoreNameProperty =
            DependencyProperty.Register(
                nameof(StoreName),
                typeof(string),
                typeof(AppSidebar),
                new PropertyMetadata("BIF Toy Store", OnStoreNameChanged));

        public bool IsAdmin
        {
            get => (bool)GetValue(IsAdminProperty);
            set => SetValue(IsAdminProperty, value);
        }

        public string ActiveTab
        {
            get => (string)GetValue(ActiveTabProperty);
            set => SetValue(ActiveTabProperty, value);
        }

        public string StoreName
        {
            get => (string)GetValue(StoreNameProperty);
            set => SetValue(StoreNameProperty, value);
        }

        public AppSidebar()
        {
            InitializeComponent();
            UpdateStoreName();
            UpdateRoleVisibility();
            UpdateActiveState();
            UpdateCurrentUsername();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateCurrentUsername();
        }

        private static void OnIsAdminChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AppSidebar sidebar)
            {
                sidebar.UpdateRoleVisibility();
                sidebar.UpdateActiveState();
                sidebar.UpdateCurrentUsername();
            }
        }

        private static void OnActiveTabChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AppSidebar sidebar)
            {
                sidebar.UpdateActiveState();
            }
        }

        private static void OnStoreNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AppSidebar sidebar)
            {
                sidebar.UpdateStoreName();
            }
        }

        private void UpdateStoreName()
        {
            var resolvedStoreName = string.IsNullOrWhiteSpace(StoreName)
                ? "BIF Toy Store"
                : StoreName.Trim();

            StoreNameText.Text = resolvedStoreName.Length > MaxStoreNameLength
                ? resolvedStoreName[..MaxStoreNameLength]
                : resolvedStoreName;
        }

        private void UpdateCurrentUsername()
        {
            var roleLabel = App.Current.MainWindowInstance?.CurrentUserRoleLabel;
            var isAdmin = string.Equals(roleLabel, "ADMIN", StringComparison.OrdinalIgnoreCase);
            SidebarUserRoleText.Text = isAdmin ? "Admin" : "Sale";

            var username = App.Current.MainWindowInstance?.CurrentUsername;
            SidebarUsernameText.Text = string.IsNullOrWhiteSpace(username)
                ? "Unknown User"
                : username.Trim();
        }

        private void UpdateActiveState()
        {
            var activeBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["FluentPlayPrimaryBrush"];
            var inactiveBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["FluentPlayOnSurfaceVariantBrush"];

            bool isDashboard = string.Equals(ActiveTab, "Dashboard", System.StringComparison.OrdinalIgnoreCase);
            bool isPos = string.Equals(ActiveTab, "POS", System.StringComparison.OrdinalIgnoreCase);
            bool isProducts = string.Equals(ActiveTab, "Products", System.StringComparison.OrdinalIgnoreCase);
            bool isCategories = string.Equals(ActiveTab, "Categories", System.StringComparison.OrdinalIgnoreCase);
            bool isOrders = string.Equals(ActiveTab, "Orders", System.StringComparison.OrdinalIgnoreCase);
            bool isUsers = string.Equals(ActiveTab, "Users", System.StringComparison.OrdinalIgnoreCase);
            bool isReports = string.Equals(ActiveTab, "Reports", System.StringComparison.OrdinalIgnoreCase);
            bool isSettings = string.Equals(ActiveTab, "Settings", System.StringComparison.OrdinalIgnoreCase);

            SetTabState(DashboardActiveWrap, DashboardActiveIndicator, DashboardText, DashboardIcon, isDashboard, activeBrush, inactiveBrush);
            SetTabState(PosActiveWrap, PosActiveIndicator, PosText, PosIcon, isPos, activeBrush, inactiveBrush);
            SetTabState(ProductsActiveWrap, ProductsActiveIndicator, ProductsText, ProductsIcon, isProducts, activeBrush, inactiveBrush);
            SetTabState(CategoriesActiveWrap, CategoriesActiveIndicator, CategoriesText, CategoriesIcon, isCategories, activeBrush, inactiveBrush);
            SetTabState(OrdersActiveWrap, OrdersActiveIndicator, OrdersText, OrdersIcon, isOrders, activeBrush, inactiveBrush);
            SetTabState(UsersActiveWrap, UsersActiveIndicator, UsersText, UsersIcon, isUsers, activeBrush, inactiveBrush);
            SetTabState(ReportsActiveWrap, ReportsActiveIndicator, ReportsText, ReportsIcon, isReports, activeBrush, inactiveBrush);
            SetTabState(SettingsActiveWrap, SettingsActiveIndicator, SettingsText, SettingsIcon, isSettings, activeBrush, inactiveBrush);
        }

        private void UpdateRoleVisibility()
        {
            var privilegedVisibility = IsAdmin ? Visibility.Visible : Visibility.Collapsed;

            UsersActiveWrap.Visibility = privilegedVisibility;
            ReportsActiveWrap.Visibility = privilegedVisibility;
            SettingsActiveWrap.Visibility = privilegedVisibility;

            if (!IsAdmin
                && (string.Equals(ActiveTab, "Users", System.StringComparison.OrdinalIgnoreCase)
                    || string.Equals(ActiveTab, "Reports", System.StringComparison.OrdinalIgnoreCase)
                    || string.Equals(ActiveTab, "Settings", System.StringComparison.OrdinalIgnoreCase)))
            {
                ActiveTab = "Dashboard";
            }
        }

        private static void SetTabState(
            Border wrap,
            Rectangle indicator,
            TextBlock text,
            PathIcon icon,
            bool isActive,
            Microsoft.UI.Xaml.Media.Brush activeBrush,
            Microsoft.UI.Xaml.Media.Brush inactiveBrush)
        {
            wrap.Background = isActive
                ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White)
                : new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);

            indicator.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;
            text.Foreground = isActive ? activeBrush : inactiveBrush;
            icon.Foreground = isActive ? activeBrush : inactiveBrush;
            text.FontWeight = isActive ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Medium;
        }

        private void DashboardButton_Click(object sender, RoutedEventArgs e)
        {
            App.Current.MainWindowInstance?.NavigateToDashboard();
        }

        private void PosButton_Click(object sender, RoutedEventArgs e)
        {
            App.Current.MainWindowInstance?.NavigateToPos();
        }

        private void UsersButton_Click(object sender, RoutedEventArgs e)
        {
            App.Current.MainWindowInstance?.NavigateToUsers();
        }

        private void ProductsButton_Click(object sender, RoutedEventArgs e)
        {
            App.Current.MainWindowInstance?.NavigateToProducts();
        }

        private void CategoriesButton_Click(object sender, RoutedEventArgs e)
        {
            App.Current.MainWindowInstance?.NavigateToCategories();
        }

        private void OrdersButton_Click(object sender, RoutedEventArgs e)
        {
            App.Current.MainWindowInstance?.NavigateToOrders();
        }

        private void ReportsButton_Click(object sender, RoutedEventArgs e)
        {
            App.Current.MainWindowInstance?.NavigateToReports();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            App.Current.MainWindowInstance?.NavigateToSettings();
        }

        private async void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            var result = await CommonDialog.ShowAsync(
                XamlRoot,
                CommonDialogType.Warning,
                title: "Confirm Logout",
                message: "You are about to sign out of Toy Workshop. Any unsaved work may be lost. Continue?",
                primaryButtonText: "Logout",
                closeButtonText: "Cancel",
                defaultButton: ContentDialogButton.Primary);

            if (result != ContentDialogResult.Primary)
            {
                return;
            }

            if (App.Current.MainWindowInstance is not null)
            {
                await App.Current.MainWindowInstance.LogoutAsync();
            }
        }
    }
}
