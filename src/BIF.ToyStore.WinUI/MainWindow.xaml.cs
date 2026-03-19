using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.ViewModels.Messages;
using BIF.ToyStore.ViewModels.Utils;
using BIF.ToyStore.WinUI.Views;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
namespace BIF.ToyStore.WinUI
{
    public sealed partial class MainWindow : Window, IRecipient<LoginSucceededMessage>
    {
        private readonly ILocalSettingsService _localSettingsService;

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

        public void Receive(LoginSucceededMessage message)
        {
            var route = _localSettingsService.GetString(AppPreferenceKeys.LastActiveRoute, "Dashboard");
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
