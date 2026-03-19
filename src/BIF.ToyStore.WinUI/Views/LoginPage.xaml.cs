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
        }
    }
}
