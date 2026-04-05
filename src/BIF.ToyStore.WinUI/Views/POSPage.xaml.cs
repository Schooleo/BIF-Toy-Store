using BIF.ToyStore.ViewModels.Pages;
using BIF.ToyStore.WinUI.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BIF.ToyStore.WinUI.Views
{
    public sealed partial class POSPage : Page
    {
        public POSViewModel ViewModel { get; }

        public POSPage()
        {
            InitializeComponent();

            ViewModel = App.Current.Services.GetRequiredService<POSViewModel>();
            DataContext = ViewModel;

            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            await ViewModel.LoadAsync();
        }

        private void CategoryFlyout_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not ListView listView || listView.SelectedItem is null)
            {
                return;
            }

            CommonFlyout.HideAttachedFlyout(CategoryFilterButton);
        }

        private void SortFlyout_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not ListView listView || listView.SelectedItem is null)
            {
                return;
            }

            CommonFlyout.HideAttachedFlyout(SortFilterButton);
        }
    }
}
