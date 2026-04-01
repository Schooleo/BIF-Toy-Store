using BIF.ToyStore.ViewModels.Pages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BIF.ToyStore.WinUI.Views
{
    public sealed partial class ReportsPage : Page
    {
        public ReportsViewModel ViewModel { get; }

        public ReportsPage()
        {
            InitializeComponent();
            ViewModel = App.Current.Services.GetRequiredService<ReportsViewModel>();
            DataContext = ViewModel;
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;

            var mainWindow = App.Current.MainWindowInstance;
            if (mainWindow is null || !mainWindow.IsCurrentUserAdmin)
            {
                if (mainWindow is not null)
                {
                    mainWindow.NavigateToDashboard();
                }
                return;
            }

            await ViewModel.LoadAsync();
        }
    }
}
