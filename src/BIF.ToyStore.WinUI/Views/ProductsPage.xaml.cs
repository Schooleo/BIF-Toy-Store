using BIF.ToyStore.Core.Models;
using BIF.ToyStore.ViewModels.Pages;
using BIF.ToyStore.WinUI.Controls;
using BIF.ToyStore.WinUI.Services;
using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using WinRT.Interop;
using Microsoft.UI.Dispatching;

namespace BIF.ToyStore.WinUI.Views
{
    public sealed partial class ProductsPage : Page
    {
        public ProductsViewModel ViewModel { get; }
        private DispatcherQueueTimer _searchDebounceTimer;

        public ProductsPage()
        {
            ViewModel = App.Current.Services.GetRequiredService<ProductsViewModel>();
            InitializeComponent();
            
            _searchDebounceTimer = DispatcherQueue.CreateTimer();
            _searchDebounceTimer.Interval = TimeSpan.FromMilliseconds(400);
            _searchDebounceTimer.Tick += SearchDebounceTimer_Tick;
            
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

        private void CategoryFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not ListView listView || listView.SelectedItem is null)
            {
                return;
            }

            ApplyFilter();
            CommonFlyout.HideAttachedFlyout(CategoryFilterButton);
        }

        private void SortFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not ListView listView || listView.SelectedItem is null)
            {
                return;
            }

            ApplyFilter();
            CommonFlyout.HideAttachedFlyout(SortFilterButton);
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

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchDebounceTimer.Stop();
            _searchDebounceTimer.Start();
        }

        private void SearchDebounceTimer_Tick(DispatcherQueueTimer sender, object args)
        {
            _searchDebounceTimer.Stop();
            ApplyFilter();
        }

        private void ProductsDataGrid_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (IsFromButton(e.OriginalSource as DependencyObject))
            {
                return;
            }

            e.Handled = true;

            if (sender is DataGrid dataGrid)
            {
                dataGrid.SelectedItem = null;
            }
        }

        private void ProductsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is DataGrid dataGrid && dataGrid.SelectedItem != null)
            {
                dataGrid.SelectedItem = null;
            }
        }

        private static bool IsFromButton(DependencyObject? source)
        {
            while (source != null)
            {
                if (source is Button)
                {
                    return true;
                }

                source = VisualTreeHelper.GetParent(source);
            }

            return false;
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
                try
                {
                    var input = new Product
                    {
                        Name = dialog.ResultProduct.Name,
                        CategoryId = dialog.ResultProduct.CategoryId,
                        ImportPrice = dialog.ResultProduct.ImportPrice,
                        RetailPrice = dialog.ResultProduct.RetailPrice,
                        StockQuantity = dialog.ResultProduct.StockQuantity,
                        ImageUrl = dialog.ResultProduct.ImageUrl
                    };

                    await ViewModel.CreateProductAsync(input);
                }
                catch (Exception ex)
                {
                    await CommonDialog.ShowAsync(
                        XamlRoot,
                        CommonDialogType.Error,
                        title: "Unable to add product",
                        message: ex.Message,
                        primaryButtonText: "OK",
                        closeButtonText: null);
                }
            }
        }

        private void EditProduct_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: Product product } && ViewModel.OpenEditPanelCommand.CanExecute(product))
            {
                ViewModel.OpenEditPanelCommand.Execute(product);
            }
        }

        private async void DeleteProduct_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag is Product product)
            {
                var result = await CommonDialog.ShowAsync(
                    XamlRoot,
                    CommonDialogType.Confirmation,
                    title: "Confirm Delete",
                    message: $"Do you want to delete '{product.Name}'?",
                    primaryButtonText: "Delete",
                    closeButtonText: "Cancel",
                    defaultButton: ContentDialogButton.Primary
                );

                if (result == ContentDialogResult.Primary)
                {
                    await ViewModel.DeleteProductAsync(product.Id);
                }
            }
        }
    }
}
