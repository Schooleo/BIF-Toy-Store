using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;
using BIF.ToyStore.WinUI.Services;

namespace BIF.ToyStore.WinUI.Controls
{
    public sealed partial class AppSidebar : UserControl
    {
        public static readonly DependencyProperty ActiveTabProperty =
            DependencyProperty.Register(
                nameof(ActiveTab),
                typeof(string),
                typeof(AppSidebar),
                new PropertyMetadata("Dashboard", OnActiveTabChanged));

        public string ActiveTab
        {
            get => (string)GetValue(ActiveTabProperty);
            set => SetValue(ActiveTabProperty, value);
        }

        public AppSidebar()
        {
            InitializeComponent();
            UpdateActiveState();
        }

        private static void OnActiveTabChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AppSidebar sidebar)
            {
                sidebar.UpdateActiveState();
            }
        }

        private void UpdateActiveState()
        {
            var activeBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["FluentPlayPrimaryBrush"];
            var inactiveBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["FluentPlayOnSurfaceVariantBrush"];

            bool isDashboard = string.Equals(ActiveTab, "Dashboard", System.StringComparison.OrdinalIgnoreCase);
            bool isPos = string.Equals(ActiveTab, "POS", System.StringComparison.OrdinalIgnoreCase);
            bool isProducts = string.Equals(ActiveTab, "Products", System.StringComparison.OrdinalIgnoreCase);
            bool isOrders = string.Equals(ActiveTab, "Orders", System.StringComparison.OrdinalIgnoreCase);
            bool isUsers = string.Equals(ActiveTab, "Users", System.StringComparison.OrdinalIgnoreCase);
            bool isReports = string.Equals(ActiveTab, "Reports", System.StringComparison.OrdinalIgnoreCase);
            bool isSettings = string.Equals(ActiveTab, "Settings", System.StringComparison.OrdinalIgnoreCase);
            bool isProfile = string.Equals(ActiveTab, "Profile", System.StringComparison.OrdinalIgnoreCase);

            SetTabState(DashboardActiveWrap, DashboardActiveIndicator, DashboardText, DashboardIcon, isDashboard, activeBrush, inactiveBrush);
            SetTabState(PosActiveWrap, PosActiveIndicator, PosText, PosIcon, isPos, activeBrush, inactiveBrush);
            SetTabState(ProductsActiveWrap, ProductsActiveIndicator, ProductsText, ProductsIcon, isProducts, activeBrush, inactiveBrush);
            SetTabState(OrdersActiveWrap, OrdersActiveIndicator, OrdersText, OrdersIcon, isOrders, activeBrush, inactiveBrush);
            SetTabState(UsersActiveWrap, UsersActiveIndicator, UsersText, UsersIcon, isUsers, activeBrush, inactiveBrush);
            SetTabState(ReportsActiveWrap, ReportsActiveIndicator, ReportsText, ReportsIcon, isReports, activeBrush, inactiveBrush);
            SetTabState(SettingsActiveWrap, SettingsActiveIndicator, SettingsText, SettingsIcon, isSettings, activeBrush, inactiveBrush);
            SetTabState(ProfileActiveWrap, ProfileActiveIndicator, ProfileText, ProfileIcon, isProfile, activeBrush, inactiveBrush);
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

        private void UsersButton_Click(object sender, RoutedEventArgs e)
        {
            App.Current.MainWindowInstance?.NavigateToUsers();
        }

        private void ProductsButton_Click(object sender, RoutedEventArgs e)
        {
            App.Current.MainWindowInstance?.NavigateToProducts();
        }

        private void PlaceholderButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string tab && !string.IsNullOrWhiteSpace(tab))
            {
                ActiveTab = tab;
            }
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
