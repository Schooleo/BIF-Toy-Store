using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.ViewModels.Base;
using BIF.ToyStore.ViewModels.Utils;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace BIF.ToyStore.ViewModels.Pages
{
    public partial class ProductsViewModel : PaginatedViewModel
    {
        private readonly IProductService _productService;
        private readonly IProductImageUploadService _productImageUploadService;
        private readonly ILocalSettingsService _localSettingsService;
        private readonly IExcelFilePickerService _excelFilePickerService;
        private nint _windowHandle;
        private string? _editingProductOriginalImageUrl;
        private Product? _editingProductSnapshot;

        [ObservableProperty]
        private ObservableCollection<Product> _products = [];

        [ObservableProperty]
        private ObservableCollection<Category> _categories = [];

        [ObservableProperty]
        private ObservableCollection<Category> _categoryFilterOptions = [];

        private static readonly Category AllCategoriesOption = new()
        {
            Id = 0,
            Name = "All Categories"
        };

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
        public string SelectedCategoryLabel => SelectedCategory?.Name ?? "All Categories";
        public string SelectedSortLabel => SelectedSort?.Name ?? "Newest";

        // Computed property for total count label (notifies when TotalCount changes)
        public new string TotalCountLabel => $"Total items in catalog: {TotalCount} Units";

        public ProductsViewModel(
            IProductService productService,
            IProductImageUploadService productImageUploadService,
            ILocalSettingsService localSettingsService,
            IExcelFilePickerService excelFilePickerService)
        {
            _productService = productService;
            _productImageUploadService = productImageUploadService;
            _localSettingsService = localSettingsService;
            _excelFilePickerService = excelFilePickerService;
            Title = "Product Management";
            
            PageSize = _localSettingsService.GetInt(AppPreferenceKeys.ProductsItemsPerPage, 20);
            SelectedSort = SortOptions.FirstOrDefault(option => option.Value == "id_desc") ?? SortOptions.FirstOrDefault();
        }

        public void SetWindowHandle(nint windowHandle)
        {
            _windowHandle = windowHandle;
        }

        partial void OnImportSuccessMessageChanged(string value) => OnPropertyChanged(nameof(HasImportSuccessMessage));
        partial void OnImportErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasImportErrorMessage));
        partial void OnEditErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasEditErrorMessage));

        partial void OnSelectedCategoryChanged(Category? value) => OnPropertyChanged(nameof(SelectedCategoryLabel));

        partial void OnSelectedSortChanged(SortOption? value) => OnPropertyChanged(nameof(SelectedSortLabel));

        [RelayCommand]
        public async Task LoadCategoriesAsync()
        {
            var categories = await _productService.GetCategoriesAsync();
            var previousCategoryId = SelectedCategory?.Id ?? 0;

            Categories = new ObservableCollection<Category>(categories);

            CategoryFilterOptions.Clear();
            CategoryFilterOptions.Add(AllCategoriesOption);

            foreach (var category in categories)
            {
                CategoryFilterOptions.Add(category);
            }

            SelectedCategory = CategoryFilterOptions.FirstOrDefault(c => c.Id == previousCategoryId) ?? AllCategoriesOption;
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
                    CategoryId = SelectedCategory is { Id: > 0 } ? SelectedCategory.Id : null,
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
            SelectedCategory = CategoryFilterOptions.FirstOrDefault(c => c.Id == 0) ?? AllCategoriesOption;
            MinPrice = 0;
            MaxPrice = 1000;
            SelectedSort = SortOptions.FirstOrDefault(option => option.Value == "id_desc") ?? SortOptions.FirstOrDefault();
            BeforeCursor = null;
            AfterCursor = null;
            await LoadPageAsync(null);
        }

        [RelayCommand]
        public async Task<Product> CreateProductAsync(Product input)
        {
            var pendingImagePath = ExtractPendingImagePath(input.ImageUrl);
            input.ImageUrl = null;

            var createdProduct = await _productService.CreateProductAsync(input);
            ProductImageUploadResult? uploadResult = null;

            try
            {
                if (!string.IsNullOrWhiteSpace(pendingImagePath))
                {
                    uploadResult = await _productImageUploadService.UploadProductImageAsync(createdProduct.Id, pendingImagePath);
                    createdProduct.ImageUrl = uploadResult.ImageUrl;
                    createdProduct = await _productService.UpdateProductAsync(createdProduct);
                }
            }
            catch
            {
                if (!string.IsNullOrWhiteSpace(uploadResult?.PublicId))
                {
                    await SafeDeleteProductImageAsync(uploadResult.PublicId);
                }

                await SafeDeleteProductAsync(createdProduct.Id);
                await SafeReloadProductsAsync();
                throw;
            }

            await LoadProductsAsync();
            return createdProduct;
        }

        [RelayCommand]
        public async Task<Product> UpdateProductAsync(Product input)
        {
            var pendingImagePath = ExtractPendingImagePath(input.ImageUrl);
            input.ImageUrl = !string.IsNullOrWhiteSpace(pendingImagePath)
                ? ResolvePersistedImageUrl(input.Id)
                : input.ImageUrl;

            var rollbackSnapshot = _editingProductSnapshot is { Id: var snapshotId } && snapshotId == input.Id
                ? CloneProduct(_editingProductSnapshot)
                : null;

            var updatedProduct = await _productService.UpdateProductAsync(input);
            if (!string.IsNullOrWhiteSpace(pendingImagePath))
            {
                var previousManagedPublicId = TryExtractManagedPublicId(rollbackSnapshot?.ImageUrl);
                ProductImageUploadResult? uploadResult = null;

                try
                {
                    uploadResult = await _productImageUploadService.UploadProductImageAsync(updatedProduct.Id, pendingImagePath);
                    updatedProduct.ImageUrl = uploadResult.ImageUrl;
                    updatedProduct = await _productService.UpdateProductAsync(updatedProduct);

                    if (!string.IsNullOrWhiteSpace(previousManagedPublicId))
                    {
                        await SafeDeleteProductImageAsync(previousManagedPublicId);
                    }

                    _editingProductOriginalImageUrl = updatedProduct.ImageUrl;
                }
                catch
                {
                    if (!string.IsNullOrWhiteSpace(uploadResult?.PublicId))
                    {
                        await SafeDeleteProductImageAsync(uploadResult.PublicId);
                    }

                    if (rollbackSnapshot is not null)
                    {
                        await _productService.UpdateProductAsync(rollbackSnapshot);
                    }

                    await SafeReloadProductsAsync();
                    throw;
                }
            }

            await LoadProductsAsync();
            return updatedProduct;
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
                ImageUrl = product.ImageUrl,
                IsDeleted = product.IsDeleted
            };

            _editingProductSnapshot = CloneProduct(product);
            _editingProductOriginalImageUrl = product.ImageUrl;
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
                    StockQuantity = EditingProduct.StockQuantity,
                    ImageUrl = EditingProduct.ImageUrl
                };

                await UpdateProductAsync(input);

                IsEditingProduct = false;
                EditingProduct = null;
                _editingProductOriginalImageUrl = null;
                _editingProductSnapshot = null;
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
            _editingProductOriginalImageUrl = null;
            _editingProductSnapshot = null;
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

        private static Product CloneProduct(Product product)
        {
            return new Product
            {
                Id = product.Id,
                Name = product.Name,
                CategoryId = product.CategoryId,
                Category = product.Category,
                RetailPrice = product.RetailPrice,
                ImportPrice = product.ImportPrice,
                StockQuantity = product.StockQuantity,
                ImageUrl = product.ImageUrl,
                IsDeleted = product.IsDeleted
            };
        }

        private string? ResolvePersistedImageUrl(int productId)
        {
            if (!string.IsNullOrWhiteSpace(_editingProductOriginalImageUrl))
            {
                return _editingProductOriginalImageUrl;
            }

            return Products.FirstOrDefault(p => p.Id == productId)?.ImageUrl;
        }

        private static string? ExtractPendingImagePath(string? imageValue)
        {
            return !string.IsNullOrWhiteSpace(imageValue) && Path.IsPathRooted(imageValue)
                ? imageValue
                : null;
        }

        private static string? TryExtractManagedPublicId(string? imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                return null;
            }

            const string marker = "/image/upload/";
            if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri))
            {
                return null;
            }

            var markerIndex = uri.AbsolutePath.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
            {
                return null;
            }

            var publicId = uri.AbsolutePath[(markerIndex + marker.Length)..].Trim('/');
            var segments = publicId
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            if (segments.Count == 0)
            {
                return null;
            }

            if (segments[0].Length > 1
                && segments[0][0] == 'v'
                && long.TryParse(segments[0][1..], out _))
            {
                segments.RemoveAt(0);
            }

            if (segments.Count == 0)
            {
                return null;
            }

            var lastSegment = segments[^1];
            var extensionIndex = lastSegment.LastIndexOf('.');
            if (extensionIndex > 0)
            {
                segments[^1] = lastSegment[..extensionIndex];
            }

            publicId = string.Join("/", segments);

            return publicId.Contains("bif-toy-store/products/product-", StringComparison.OrdinalIgnoreCase)
                ? publicId
                : null;
        }

        private async Task SafeDeleteProductAsync(int productId)
        {
            try
            {
                await _productService.DeleteProductAsync(productId);
            }
            catch
            {
                // Best-effort rollback only.
            }
        }

        private async Task SafeDeleteProductImageAsync(string publicId)
        {
            try
            {
                await _productImageUploadService.DeleteProductImageAsync(publicId);
            }
            catch
            {
                // Best-effort cleanup only.
            }
        }

        private async Task SafeReloadProductsAsync()
        {
            try
            {
                await LoadProductsAsync();
            }
            catch
            {
                // Best-effort refresh only.
            }
        }
    }
}
