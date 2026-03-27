using BIF.ToyStore.ViewModels.Pages;
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

            if (!requiresRestartWarning || !string.IsNullOrWhiteSpace(ViewModel.ErrorMessage))
            {
                return;
            }

            await CommonDialog.ShowAsync(
                XamlRoot,
                CommonDialogType.Warning,
                "Restart Required",
                "The app needs to restart to apply the new server configuration.",
                primaryButtonText: "OK",
                closeButtonText: null,
                defaultButton: ContentDialogButton.Primary);
        }
    }
}
