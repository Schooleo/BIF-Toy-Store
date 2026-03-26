using BIF.ToyStore.ViewModels.Pages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using BIF.ToyStore.Core.Models;
using System;
using WinRT.Interop;
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
                var window = App.Current.MainWindowInstance;
                if (window != null)
                {
                    var hwnd = WindowNative.GetWindowHandle(window);
                    ViewModel.SetWindowHandle(hwnd);
                }

                await ViewModel.LoadCategoriesCommand.ExecuteAsync(null);
                await ViewModel.LoadProductsCommand.ExecuteAsync(null);
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

        private async void AddProduct_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Dialogs.AddProductForm(ViewModel.Categories)
            {
                XamlRoot = this.XamlRoot
            };
            
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && dialog.ResultProduct != null)
            {
                var input = new BIF.ToyStore.Infrastructure.GraphQL.CreateProductInput
                {
                    Name = dialog.ResultProduct.Name,
                    CategoryId = dialog.ResultProduct.CategoryId,
                    ImportPrice = dialog.ResultProduct.ImportPrice,
                    RetailPrice = dialog.ResultProduct.RetailPrice,
                    StockQuantity = dialog.ResultProduct.StockQuantity
                };
                await ViewModel.CreateProductAsync(input);
            }
        }

        private async void EditProduct_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag is Product product)
            {
                var dialog = new Dialogs.AddProductForm(ViewModel.Categories, product)
                {
                    XamlRoot = this.XamlRoot
                };
                
                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary && dialog.ResultProduct != null)
                {
                    var input = new BIF.ToyStore.Infrastructure.GraphQL.UpdateProductInput
                    {
                        Id = dialog.ResultProduct.Id,
                        Name = dialog.ResultProduct.Name,
                        CategoryId = dialog.ResultProduct.CategoryId,
                        ImportPrice = dialog.ResultProduct.ImportPrice,
                        RetailPrice = dialog.ResultProduct.RetailPrice,
                        StockQuantity = dialog.ResultProduct.StockQuantity
                    };
                    await ViewModel.UpdateProductAsync(input);
                }
            }
        }

        private async void DeleteProduct_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag is Product product)
            {
                var confirmDialog = new ContentDialog
                {
                    Title = "Delete Product",
                    Content = $"Are you sure you want to delete '{product.Name}'? This action cannot be undone.",
                    PrimaryButtonText = "DELETE",
                    CloseButtonText = "CANCEL",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.XamlRoot
                };

                var result = await confirmDialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    await ViewModel.DeleteProductAsync(product.Id);
                }
            }
        }
    }
}
