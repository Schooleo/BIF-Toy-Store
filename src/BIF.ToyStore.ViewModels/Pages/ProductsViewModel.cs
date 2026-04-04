using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.ViewModels.Base;
using BIF.ToyStore.ViewModels.Utils;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;

namespace BIF.ToyStore.ViewModels.Pages
{
    public partial class ProductsViewModel : PaginatedViewModel
    {
        private readonly IProductService _productService;
        private readonly ILocalSettingsService _localSettingsService;
        private readonly IExcelFilePickerService _excelFilePickerService;
        private nint _windowHandle;

        [ObservableProperty]
        private ObservableCollection<Product> _products = [];

        [ObservableProperty]
        private ObservableCollection<Category> _categories = [];

        // Filter properties
        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private Category? _selectedCategory;

        [ObservableProperty]
        private double _minPrice = 0;

        [ObservableProperty]
        private double _maxPrice = 1000;

        [ObservableProperty]
        private SortOption? _selectedSort;

        public ObservableCollection<SortOption> SortOptions { get; } = new()
        {
            new SortOption { Name = "Newest", Value = "id_desc" },
            new SortOption { Name = "Price: Low to High", Value = "price_asc" },
            new SortOption { Name = "Price: High to Low", Value = "price_desc" },
            new SortOption { Name = "Stock: Low to High", Value = "stock_asc" },
            new SortOption { Name = "Name: A-Z", Value = "name_asc" }
        };

        [ObservableProperty]
        private string _importSuccessMessage = string.Empty;

        [ObservableProperty]
        private string _importErrorMessage = string.Empty;

        [ObservableProperty]
        private bool _isEditingProduct;

        [ObservableProperty]
        private Product? _editingProduct;

        [ObservableProperty]
        private string _editErrorMessage = string.Empty;

        public bool HasImportSuccessMessage => !string.IsNullOrWhiteSpace(ImportSuccessMessage);
        public bool HasImportErrorMessage => !string.IsNullOrWhiteSpace(ImportErrorMessage);
        public bool HasEditErrorMessage => !string.IsNullOrWhiteSpace(EditErrorMessage);

        // Computed property for total count label (notifies when TotalCount changes)
        public new string TotalCountLabel => $"Total items in catalog: {TotalCount} Units";

        public ProductsViewModel(
            IProductService productService,
            ILocalSettingsService localSettingsService,
            IExcelFilePickerService excelFilePickerService)
        {
            _productService = productService;
            _localSettingsService = localSettingsService;
            _excelFilePickerService = excelFilePickerService;
            Title = "Product Management";
            
            PageSize = _localSettingsService.GetInt(AppPreferenceKeys.ProductsItemsPerPage, 20);
        }

        public void SetWindowHandle(nint windowHandle)
        {
            _windowHandle = windowHandle;
        }

        partial void OnImportSuccessMessageChanged(string value) => OnPropertyChanged(nameof(HasImportSuccessMessage));
        partial void OnImportErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasImportErrorMessage));
        partial void OnEditErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasEditErrorMessage));

        [RelayCommand]
        public async Task LoadCategoriesAsync()
        {
            var categories = await _productService.GetCategoriesAsync();
            Categories = new ObservableCollection<Category>(categories);
        }

        [RelayCommand]
        public async Task LoadProductsAsync(string? direction = null)
        {
            await LoadPageAsync(direction);
        }

        protected override async Task LoadPageAsync(string? direction)
        {
            IsBusy = true;
            try
            {
                var result = await _productService.GetProductsAsync(new ProductListQuery
                {
                    PageSize = PageSize,
                    Direction = direction,
                    AfterCursor = AfterCursor,
                    BeforeCursor = BeforeCursor,
                    SearchText = SearchText,
                    CategoryId = SelectedCategory?.Id,
                    MinRetailPrice = MinPrice > 0 ? (decimal)MinPrice : null,
                    MaxRetailPrice = MaxPrice < 1000 ? (decimal)MaxPrice : null,
                    SortValue = SelectedSort?.Value
                });

                Products = new ObservableCollection<Product>(result.Items);
                ApplyPageInfo(
                    result.TotalCount,
                    result.HasNextPage,
                    result.HasPreviousPage,
                    result.StartCursor,
                    result.EndCursor);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public async Task ApplyFilterAsync()
        {
            BeforeCursor = null;
            AfterCursor = null;
            await LoadPageAsync(null);
        }

        [RelayCommand]
        public async Task ClearFilterAsync()
        {
            SearchText = string.Empty;
            SelectedCategory = null;
            MinPrice = 0;
            MaxPrice = 1000;
            BeforeCursor = null;
            AfterCursor = null;
            await LoadPageAsync(null);
        }

        [RelayCommand]
        public async Task CreateProductAsync(Product input)
        {
            await _productService.CreateProductAsync(input);
            await LoadProductsAsync();
        }

        [RelayCommand]
        public async Task UpdateProductAsync(Product input)
        {
            await _productService.UpdateProductAsync(input);
            await LoadProductsAsync();
        }

        [RelayCommand]
        public void OpenEditPanel(Product product)
        {
            if (product is null)
            {
                return;
            }

            EditingProduct = new Product
            {
                Id = product.Id,
                Name = product.Name,
                CategoryId = product.CategoryId,
                Category = product.Category,
                RetailPrice = product.RetailPrice,
                ImportPrice = product.ImportPrice,
                StockQuantity = product.StockQuantity,
                IsDeleted = product.IsDeleted
            };

            EditErrorMessage = string.Empty;
            IsEditingProduct = true;
        }

        [RelayCommand]
        public async Task SaveProductEditAsync()
        {
            if (EditingProduct is null)
            {
                return;
            }

            EditErrorMessage = string.Empty;

            try
            {
                if (string.IsNullOrWhiteSpace(EditingProduct.Name))
                {
                    EditErrorMessage = "Product name is required.";
                    return;
                }

                if (EditingProduct.CategoryId <= 0)
                {
                    EditErrorMessage = "Please select a category.";
                    return;
                }

                if (EditingProduct.Id <= 0)
                {
                    EditErrorMessage = "Invalid product id. Please close and reopen the editor.";
                    return;
                }

                var input = new Product
                {
                    Id = EditingProduct.Id,
                    Name = EditingProduct.Name,
                    CategoryId = EditingProduct.CategoryId,
                    ImportPrice = EditingProduct.ImportPrice,
                    RetailPrice = EditingProduct.RetailPrice,
                    StockQuantity = EditingProduct.StockQuantity
                };

                await UpdateProductAsync(input);

                IsEditingProduct = false;
                EditingProduct = null;
            }
            catch (Exception ex)
            {
                EditErrorMessage = $"Unable to save product changes: {ex.Message}";
            }
        }

        [RelayCommand]
        public void CancelProductEdit()
        {
            EditErrorMessage = string.Empty;
            IsEditingProduct = false;
            EditingProduct = null;
        }

        [RelayCommand]
        public async Task DeleteProductAsync(int id)
        {
            await _productService.DeleteProductAsync(id);
            await LoadProductsAsync();
        }

        [RelayCommand]
        public async Task ImportExcelAsync()
        {
            ImportSuccessMessage = string.Empty;
            ImportErrorMessage = string.Empty;

            if (_windowHandle == 0)
            {
                ImportErrorMessage = "Cannot open file picker because the window is not ready.";
                return;
            }

            try
            {
                var selectedFilePath = await _excelFilePickerService.PickExcelFilePathAsync(_windowHandle);
                if (string.IsNullOrWhiteSpace(selectedFilePath))
                {
                    return;
                }

                IsBusy = true;

                var result = await _productService.ImportProductsAsync(selectedFilePath);

                if (result is null)
                {
                    ImportErrorMessage = "Import failed: server did not return a result.";
                    return;
                }

                if (result.Errors.Count > 0)
                {
                    var firstError = result.Errors[0];
                    ImportErrorMessage =
                        $"Imported {result.ImportedCount} products with {result.Errors.Count} error(s). First error: {firstError}";
                }
                else
                {
                    ImportSuccessMessage = $"Imported {result.ImportedCount} product(s) successfully.";
                }

                await LoadProductsAsync();
            }
            catch (Exception ex)
            {
                ImportErrorMessage = $"Import failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        public class SortOption
        {
            public string Name { get; set; } = string.Empty;
            public string Value { get; set; } = string.Empty;
        }
    }
}
