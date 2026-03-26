using BIF.ToyStore.ViewModels.Pages;
using BIF.ToyStore.WinUI.Services;
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
            var dialogResult = await CommonDialog.ShowAsync(
                XamlRoot,
                CommonDialogType.Confirmation,
                title: "Confirm Initial Configuration",
                message: "Saving will finalize initial setup. You will be redirected to Login. Continue?",
                primaryButtonText: "Confirm",
                closeButtonText: "Cancel",
                defaultButton: ContentDialogButton.Primary);

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
                var restartResult = await CommonDialog.ShowAsync(
                    XamlRoot,
                    CommonDialogType.Warning,
                    title: "Restart Required",
                    message: "Server port was changed. The app will now close so it can bind to the new port on next launch.",
                    primaryButtonText: "Exit Now",
                    closeButtonText: "Cancel",
                    defaultButton: ContentDialogButton.Primary);

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
