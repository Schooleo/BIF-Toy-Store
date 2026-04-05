using BIF.ToyStore.Core.Models;
using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.ViewModels.Base;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace BIF.ToyStore.ViewModels.Pages
{
    public partial class AddProductFormViewModel : BaseViewModel
    {
        private readonly IImageFilePickerService _imageFilePickerService;
        private readonly nint _windowHandle;

        [ObservableProperty]
        private string? name = string.Empty;

        [ObservableProperty]
        private Category? selectedCategory;

        [ObservableProperty]
        private decimal importPrice;

        [ObservableProperty]
        private decimal retailPrice;

        [ObservableProperty]
        private int stockQuantity;

        [ObservableProperty]
        private string? imageUrl;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(UploadImageCommand))]
        private bool isUploadingImage;

        [ObservableProperty]
        private string uploadErrorMessage = string.Empty;

        // Error flag properties (use boolean instead of Visibility)
        [ObservableProperty]
        private bool hasNameError;

        [ObservableProperty]
        private bool hasCategoryError;

        [ObservableProperty]
        private bool hasImportPriceError;

        [ObservableProperty]
        private bool hasRetailPriceError;

        public ObservableCollection<Category> Categories { get; }

        private bool _isEditMode;
        private int _editingProductId;

        public bool HasUploadError => !string.IsNullOrWhiteSpace(UploadErrorMessage);

        public AddProductFormViewModel(
            IImageFilePickerService imageFilePickerService,
            nint windowHandle)
        {
            _imageFilePickerService = imageFilePickerService;
            _windowHandle = windowHandle;
            Categories = [];
            Title = "Add New Product";
        }

        partial void OnUploadErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasUploadError));

        public void InitializeWithCategories(IEnumerable<Category> categories)
        {
            Categories.Clear();
            foreach (var category in categories)
            {
                Categories.Add(category);
            }
        }

        public void InitializeForEdit(Product existingProduct, IEnumerable<Category> categories)
        {
            InitializeWithCategories(categories);

            _isEditMode = true;
            _editingProductId = existingProduct.Id;
            Title = "Edit Product";

            Name = existingProduct.Name;
            SelectedCategory = categories.FirstOrDefault(c => c.Id == existingProduct.CategoryId);
            ImportPrice = existingProduct.ImportPrice;
            RetailPrice = existingProduct.RetailPrice;
            StockQuantity = existingProduct.StockQuantity;
            ImageUrl = existingProduct.ImageUrl;
        }

        public bool Validate()
        {
            bool isValid = true;

            // Validate Name
            if (string.IsNullOrWhiteSpace(Name))
            {
                HasNameError = true;
                isValid = false;
            }
            else
            {
                HasNameError = false;
            }

            // Validate Category
            if (SelectedCategory == null)
            {
                HasCategoryError = true;
                isValid = false;
            }
            else
            {
                HasCategoryError = false;
            }

            // Validate Import Price
            if (ImportPrice <= 0)
            {
                HasImportPriceError = true;
                isValid = false;
            }
            else
            {
                HasImportPriceError = false;
            }

            // Validate Retail Price
            if (RetailPrice <= 0)
            {
                HasRetailPriceError = true;
                isValid = false;
            }
            else
            {
                HasRetailPriceError = false;
            }

            return isValid;
        }

        public Product GetProduct()
        {
            return new Product
            {
                Id = _isEditMode ? _editingProductId : 0,
                Name = Name ?? string.Empty,
                CategoryId = SelectedCategory?.Id ?? 0,
                ImportPrice = ImportPrice,
                RetailPrice = RetailPrice,
                StockQuantity = StockQuantity,
                ImageUrl = ImageUrl
            };
        }

        private bool CanUploadImage() => !IsUploadingImage;

        [RelayCommand(CanExecute = nameof(CanUploadImage))]
        private async Task UploadImageAsync()
        {
            UploadErrorMessage = string.Empty;

            if (_windowHandle == 0)
            {
                UploadErrorMessage = "Cannot open image picker because the window is not ready.";
                return;
            }

            try
            {
                IsUploadingImage = true;

                var selectedFilePath = await _imageFilePickerService.PickImageFilePathAsync(_windowHandle);
                if (string.IsNullOrWhiteSpace(selectedFilePath))
                {
                    return;
                }

                ImageUrl = selectedFilePath;
            }
            catch (Exception ex)
            {
                UploadErrorMessage = $"Image selection failed: {ex.Message}";
            }
            finally
            {
                IsUploadingImage = false;
            }
        }

        public void ResetForm()
        {
            Name = string.Empty;
            SelectedCategory = null;
            ImportPrice = 0;
            RetailPrice = 0;
            StockQuantity = 0;
            ImageUrl = null;
            UploadErrorMessage = string.Empty;
            IsUploadingImage = false;

            HasNameError = false;
            HasCategoryError = false;
            HasImportPriceError = false;
            HasRetailPriceError = false;

            _isEditMode = false;
            Title = "Add New Product";
        }
    }
}
