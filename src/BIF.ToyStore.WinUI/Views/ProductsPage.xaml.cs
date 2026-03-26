using BIF.ToyStore.ViewModels.Pages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace BIF.ToyStore.WinUI.Views
{
    public sealed partial class ProductsPage : Page
    {
        public ProductsViewModel ViewModel { get; }

        public ProductsPage()
        {
            ViewModel = App.Current.Services.GetRequiredService<ProductsViewModel>();
            InitializeComponent();
            
            Loaded += async (s, e) =>
            {
                await ViewModel.LoadCategoriesCommand.ExecuteAsync(null);
                await ViewModel.LoadProductsCommand.ExecuteAsync("next");
            };
        }
        
        private void Filter_Changed(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void Filter_Changed(object sender, Microsoft.UI.Xaml.Controls.NumberBoxValueChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void Filter_Changed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            if (ViewModel != null && ViewModel.ApplyFilterCommand.CanExecute(null))
            {
                ViewModel.ApplyFilterCommand.Execute(null);
            }
        }

        private void SearchBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                if (ViewModel.ApplyFilterCommand.CanExecute(null))
                {
                    ViewModel.ApplyFilterCommand.Execute(null);
                }
            }
        }

        public string FormatTotalCount(int count) => $"Total items in catalog: {count} Units";
        public string FormatPaging(int count) => $"Showing products of {count}";
        public string FormatSku(int id) => $"SKU: TOY-{id}";
        public string FormatStock(int stock) => $"{stock} Units";
    }
}
