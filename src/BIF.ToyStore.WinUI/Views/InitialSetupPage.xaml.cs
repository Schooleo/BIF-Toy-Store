using BIF.ToyStore.ViewModels.Pages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace BIF.ToyStore.WinUI.Views
{
    public sealed partial class InitialSetupPage : Page
    {
        public InitialSetupViewModel ViewModel { get; }

        public InitialSetupPage()
        {
            InitializeComponent();
            ViewModel = App.Current.Services.GetRequiredService<InitialSetupViewModel>();
            DataContext = ViewModel;
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await ViewModel.InitializeAsync();
        }

        private async void OnSaveConfigurationClick(object sender, RoutedEventArgs e)
        {
            var confirmDialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Confirm Initial Configuration",
                Content = "Saving will finalize initial setup. You will be redirected to Login. Continue?",
                PrimaryButtonText = "Confirm",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary
            };

            var dialogResult = await confirmDialog.ShowAsync();
            if (dialogResult != ContentDialogResult.Primary)
            {
                return;
            }

            var result = await ViewModel.SaveConfigurationAsync();
            if (!result.IsSuccessful)
            {
                ViewModel.ErrorMessage = result.ErrorMessage;
                return;
            }

            if (result.RequiresRestart)
            {
                var restartDialog = new ContentDialog
                {
                    XamlRoot = XamlRoot,
                    Title = "Restart Required",
                    Content = "Server port was changed. The app will now close so it can bind to the new port on next launch.",
                    PrimaryButtonText = "Exit Now",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Primary
                };

                var restartResult = await restartDialog.ShowAsync();
                if (restartResult == ContentDialogResult.Primary)
                {
                    App.Current.Exit();
                }

                return;
            }

            App.Current.MainWindowInstance?.NavigateToLogin();
        }
    }
}
