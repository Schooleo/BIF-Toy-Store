using BIF.ToyStore.Core.Models;
using BIF.ToyStore.ViewModels.Pages;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;

namespace BIF.ToyStore.WinUI.Views.Dialogs
{
    public sealed partial class AddProductForm : ContentDialog
    {
        public AddProductFormViewModel ViewModel { get; }
        public Product? ResultProduct {  get; private set; }

        public AddProductForm(IEnumerable<Category> categories, Product? existingProduct = null)
        {
            InitializeComponent();
            
            // Create and set ViewModel
            ViewModel = new AddProductFormViewModel();
            this.DataContext = ViewModel;

            // Subscribe to property changes to update visibility
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;

            if (existingProduct != null)
            {
                ViewModel.InitializeForEdit(existingProduct, categories);
            }
            else
            {
                ViewModel.InitializeWithCategories(categories);
            }
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Update error visibility based on ViewModel property changes
            switch (e.PropertyName)
            {
                case nameof(ViewModel.HasNameError):
                    NameError.Visibility = ViewModel.HasNameError ? Visibility.Visible : Visibility.Collapsed;
                    break;
                case nameof(ViewModel.HasCategoryError):
                    CategoryError.Visibility = ViewModel.HasCategoryError ? Visibility.Visible : Visibility.Collapsed;
                    break;
                case nameof(ViewModel.HasImportPriceError):
                    ImportPriceError.Visibility = ViewModel.HasImportPriceError ? Visibility.Visible : Visibility.Collapsed;
                    break;
                case nameof(ViewModel.HasRetailPriceError):
                    RetailPriceError.Visibility = ViewModel.HasRetailPriceError ? Visibility.Visible : Visibility.Collapsed;
                    break;
            }
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // Validate through ViewModel
            if (!ViewModel.Validate())
            {
                args.Cancel = true;
                return;
            }

            // Get product from ViewModel
            ResultProduct = ViewModel.GetProduct();
        }
    }
}
