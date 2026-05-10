using BIF.ToyStore.Core.Models;
using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.ViewModels.Base;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;

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
        private ObservableCollection<ProductImage> images = new();

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
        public string SelectedCategoryDisplay => SelectedCategory?.Name ?? "Select a category";

        private bool _isEditMode;
        private int _editingProductId;

        public bool HasUploadError => !string.IsNullOrWhiteSpace(UploadErrorMessage);

        public string? ImageUrl => Images?.FirstOrDefault(i => i.IsPrimary)?.ImageUrl ?? Images?.FirstOrDefault()?.ImageUrl;

        public AddProductFormViewModel(
            IImageFilePickerService imageFilePickerService,
            nint windowHandle)
        {
            _imageFilePickerService = imageFilePickerService;
            _windowHandle = windowHandle;
            Categories = [];
            Title = "Add New Product";
        }

        partial void OnSelectedCategoryChanged(Category? value)
        {
            OnPropertyChanged(nameof(SelectedCategoryDisplay));
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
            Images = new ObservableCollection<ProductImage>(existingProduct.Images ?? Enumerable.Empty<ProductImage>());
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
                Images = Images
            };
        }

        private bool CanUploadImage() => !IsUploadingImage;

        [RelayCommand(CanExecute = nameof(CanUploadImage))]
        private async Task UploadImageAsync()
        {
            UploadErrorMessage = string.Empty;

            if (Images.Count >= 3)
            {
                UploadErrorMessage = "Maximum 3 images allowed.";
                return;
            }

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

                Images.Add(new ProductImage 
                { 
                    ImageUrl = selectedFilePath, 
                    IsPrimary = Images.Count == 0, 
                    DisplayOrder = Images.Count 
                });
                OnPropertyChanged(nameof(ImageUrl));
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

        [RelayCommand]
        private void RemoveImage(ProductImage image)
        {
            if (image == null) return;
            
            bool wasPrimary = image.IsPrimary;
            Images.Remove(image);
            
            if (wasPrimary && Images.Count > 0)
            {
                Images[0].IsPrimary = true;
            }
            
            // Update display orders
            for (int i = 0; i < Images.Count; i++)
            {
                Images[i].DisplayOrder = i;
            }
            
            OnPropertyChanged(nameof(ImageUrl));
        }

        [RelayCommand]
        private void SetPrimary(ProductImage image)
        {
            if (image == null) return;
            
            foreach (var img in Images)
            {
                img.IsPrimary = (img == image);
            }
            
            OnPropertyChanged(nameof(ImageUrl));
        }

        public void ResetForm()
        {
            Name = string.Empty;
            SelectedCategory = null;
            ImportPrice = 0;
            RetailPrice = 0;
            StockQuantity = 0;
            Images.Clear();
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
