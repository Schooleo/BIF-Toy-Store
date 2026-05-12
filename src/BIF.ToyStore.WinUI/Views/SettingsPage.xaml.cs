using BIF.ToyStore.ViewModels.Pages;
using BIF.ToyStore.WinUI.Controls;
using BIF.ToyStore.WinUI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BIF.ToyStore.WinUI.Views
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsViewModel ViewModel { get; }

        public SettingsPage()
        {
            InitializeComponent();
            ViewModel = App.Current.Services.GetRequiredService<SettingsViewModel>();
            DataContext = ViewModel;
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await ViewModel.LoadAsync();
        }

        private async void SaveChangesButton_Click(object sender, RoutedEventArgs e)
        {
            var requiresRestartWarning = ViewModel.HasServerConfigurationChanges();

            await ViewModel.SaveChangesCommand.ExecuteAsync(null);

            if (string.IsNullOrWhiteSpace(ViewModel.ErrorMessage))
            {
                App.Current.MainWindowInstance?.RefreshShellStoreName();
            }

            if (!requiresRestartWarning || !string.IsNullOrWhiteSpace(ViewModel.ErrorMessage))
            {
                return;
            }

            var restartResult = await CommonDialog.ShowAsync(
                XamlRoot,
                CommonDialogType.Warning,
                "Restart Required",
                "The app needs to restart to apply the new server configuration. Exit now?",
                primaryButtonText: "Exit Now",
                closeButtonText: "Later",
                defaultButton: ContentDialogButton.Primary);

            if (restartResult == ContentDialogResult.Primary)
            {
                App.Current.Exit();
            }
        }

        private async void RestoreSystemButton_Click(object sender, RoutedEventArgs e)
        {
            var confirmResult = await CommonDialog.ShowAsync(
                XamlRoot,
                CommonDialogType.Warning,
                title: "Confirm Restore",
                message: "This will restore data from your latest backup. The app must restart to apply restored data. Continue?",
                primaryButtonText: "Continue",
                closeButtonText: "Cancel",
                defaultButton: ContentDialogButton.Primary);

            if (confirmResult != ContentDialogResult.Primary)
            {
                return;
            }

            await ViewModel.RestoreSystemCommand.ExecuteAsync(null);

            if (!string.IsNullOrWhiteSpace(ViewModel.ErrorMessage))
            {
                await CommonDialog.ShowAsync(
                    XamlRoot,
                    CommonDialogType.Error,
                    title: "Restore Failed",
                    message: ViewModel.ErrorMessage,
                    primaryButtonText: "OK",
                    closeButtonText: null,
                    defaultButton: ContentDialogButton.Primary);
                return;
            }

            var restartResult = await CommonDialog.ShowAsync(
                XamlRoot,
                CommonDialogType.Warning,
                title: "Restart Required",
                message: "Restore is scheduled successfully. Restart now to apply restored data.",
                primaryButtonText: "Exit Now",
                closeButtonText: "Later",
                defaultButton: ContentDialogButton.Primary);

            if (restartResult == ContentDialogResult.Primary)
            {
                App.Current.Exit();
            }
        }

        private void CurrencyOption_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.Tag is string currency)
            {
                ViewModel.SelectedCurrency = currency;
                CommonFlyout.CloseParentFlyout(menuItem);
            }
        }

        private void ItemsPerPageOption_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem
                && menuItem.Tag is string rawValue
                && int.TryParse(rawValue, out var parsedValue))
            {
                ViewModel.SelectedItemsPerPage = parsedValue;
                CommonFlyout.CloseParentFlyout(menuItem);
            }
        }
    }
}
