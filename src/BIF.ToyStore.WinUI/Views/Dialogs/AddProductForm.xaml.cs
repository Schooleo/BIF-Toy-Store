using BIF.ToyStore.Core.Models;
using BIF.ToyStore.ViewModels.Pages;
using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.WinUI.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using WinRT.Interop;

namespace BIF.ToyStore.WinUI.Views.Dialogs
{
    public sealed partial class AddProductForm : ContentDialog
    {
        public AddProductFormViewModel ViewModel { get; }
        public Product? ResultProduct {  get; private set; }

        public AddProductForm(IEnumerable<Category> categories, Product? existingProduct = null)
        {
            InitializeComponent();

            var imageFilePickerService = App.Current.Services.GetRequiredService<IImageFilePickerService>();
            var windowHandle = App.Current.MainWindowInstance is null
                ? 0
                : WindowNative.GetWindowHandle(App.Current.MainWindowInstance);
            
            // Create and set ViewModel
            ViewModel = new AddProductFormViewModel(imageFilePickerService, windowHandle);
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
            if (ViewModel.IsUploadingImage)
            {
                ViewModel.UploadErrorMessage = "Please wait for the image upload to finish.";
                args.Cancel = true;
                return;
            }

            // Validate through ViewModel
            if (!ViewModel.Validate())
            {
                args.Cancel = true;
                return;
            }

            // Get product from ViewModel
            ResultProduct = ViewModel.GetProduct();
        }

        private void CategoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView { SelectedItem: not null })
            {
                CommonFlyout.HideAttachedFlyout(CategorySelectorButton);
            }
        }

    }
}
