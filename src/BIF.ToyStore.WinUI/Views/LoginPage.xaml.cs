using BIF.ToyStore.ViewModels.Pages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace BIF.ToyStore.WinUI.Views
{
    public sealed partial class LoginPage : Page
    {
        public LoginViewModel ViewModel { get; }

        public LoginPage()
        {
            InitializeComponent();

            ViewModel = App.Current.Services.GetRequiredService<LoginViewModel>();

            this.DataContext = ViewModel;
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            await ViewModel.TryAutoLoginAsync();

            // Keep PasswordBox and ViewModel in sync without binding Password directly,
            // which can suppress the reveal button in WinUI.
            if (UserPasswordBox.Password != ViewModel.Password)
            {
                UserPasswordBox.Password = ViewModel.Password;
            }
        }

        private void UserPasswordBox_PasswordChanged(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (ViewModel.Password != UserPasswordBox.Password)
            {
                ViewModel.Password = UserPasswordBox.Password;
            }
        }
    }
}
