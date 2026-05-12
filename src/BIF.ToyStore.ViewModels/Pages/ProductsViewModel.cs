using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.Core.Enums;
using BIF.ToyStore.ViewModels.Base;
using BIF.ToyStore.ViewModels.Utils;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Text;

namespace BIF.ToyStore.ViewModels.Pages
{
    public partial class ProductsViewModel : PaginatedViewModel
    {
        private readonly IGraphQLClient _graphQLClient;
        private readonly IProductService _productService;
        private readonly IProductImageUploadService _productImageUploadService;
        private readonly ILocalSettingsService _localSettingsService;
        private readonly IExcelFilePickerService _excelFilePickerService;
        private nint _windowHandle;
        private Product? _editingProductSnapshot;
        private bool _hasInitializedPriceRange;

        public event Func<ImportFeedback, Task>? ImportFeedbackRequested;

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
        private double _priceRangeMinimum = 0;

        [ObservableProperty]
        private double _priceRangeMaximum = 1000;

        [ObservableProperty]
        private double _priceRangeStepFrequency = 10;

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

        [ObservableProperty]
        private string _currencySymbol = "USD";

        [ObservableProperty]
        private bool _isAdminUser = true;

        public bool HasImportSuccessMessage => !string.IsNullOrWhiteSpace(ImportSuccessMessage);
        public bool HasImportErrorMessage => !string.IsNullOrWhiteSpace(ImportErrorMessage);
        public bool HasEditErrorMessage => !string.IsNullOrWhiteSpace(EditErrorMessage);
        public bool HasProducts => Products.Count > 0;
        public bool IsProductsEmpty => !HasProducts;
        public bool CanCreateProducts => IsAdminUser;
        public string SelectedCategoryLabel => SelectedCategory?.Name ?? "All Categories";
        public string SelectedSortLabel => SelectedSort?.Name ?? "Newest";

        // Computed property for total count label (notifies when TotalCount changes)
        public new string TotalCountLabel => $"Total items in catalog: {TotalCount} Units";

        public ProductsViewModel(
            IGraphQLClient graphQLClient,
            IProductService productService,
            IProductImageUploadService productImageUploadService,
            ILocalSettingsService localSettingsService,
            IExcelFilePickerService excelFilePickerService)
        {
            _graphQLClient = graphQLClient;
            _productService = productService;
            _productImageUploadService = productImageUploadService;
            _localSettingsService = localSettingsService;
            _excelFilePickerService = excelFilePickerService;
            Title = "Product Management";
            IsAdminUser = Enum.TryParse<UserRole>(
                _localSettingsService.GetString(AppPreferenceKeys.CurrentUserRole, UserRole.Admin.ToString()),
                true,
                out var currentRole)
                && currentRole == UserRole.Admin;
            
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
        partial void OnProductsChanged(ObservableCollection<Product> value)
        {
            OnPropertyChanged(nameof(HasProducts));
            OnPropertyChanged(nameof(IsProductsEmpty));
        }

        partial void OnIsAdminUserChanged(bool value) => OnPropertyChanged(nameof(CanCreateProducts));

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
                await LoadCurrencySymbolAsync();
                await LoadPriceRangeAsync();

                var result = await _productService.GetProductsAsync(new ProductListQuery
                {
                    PageSize = PageSize,
                    Direction = direction,
                    AfterCursor = AfterCursor,
                    BeforeCursor = BeforeCursor,
                    SearchText = SearchText,
                    CategoryId = SelectedCategory is { Id: > 0 } ? SelectedCategory.Id : null,
                    MinRetailPrice = ShouldApplyMinPriceFilter() ? (decimal)MinPrice : null,
                    MaxRetailPrice = ShouldApplyMaxPriceFilter() ? (decimal)MaxPrice : null,
                    SortValue = SelectedSort?.Value
                });

                foreach (var product in result.Items)
                {
                    product.CurrencySymbol = CurrencySymbol;
                    if (product.Images is { Count: > 3 })
                    {
                        var trimmedImages = product.Images.Take(3).ToList();
                        product.Images.Clear();
                        foreach (var image in trimmedImages)
                        {
                            product.Images.Add(image);
                        }
                    }
                }

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

        private async Task LoadCurrencySymbolAsync()
        {
            const string query = @"
                query GetProductsAppConfig {
                    appConfig {
                        currencySymbol
                    }
                }";

            try
            {
                var config = await _graphQLClient.ExecuteAsync<ProductAppConfigNode>(query, dataKey: "appConfig");
                CurrencySymbol = string.IsNullOrWhiteSpace(config?.CurrencySymbol) ? "USD" : config.CurrencySymbol;
            }
            catch
            {
                CurrencySymbol = "USD";
            }
        }

        private async Task LoadPriceRangeAsync()
        {
            var previousRangeMinimum = PriceRangeMinimum;
            var previousRangeMaximum = PriceRangeMaximum;
            var previousSelectionMinimum = MinPrice;
            var previousSelectionMaximum = MaxPrice;

            var selectionWasFullRange = !_hasInitializedPriceRange
                || (previousSelectionMinimum == previousRangeMinimum && previousSelectionMaximum == previousRangeMaximum);

            var priceRangeBaseQuery = new ProductListQuery
            {
                PageSize = 1,
                SearchText = SearchText,
                CategoryId = SelectedCategory is { Id: > 0 } ? SelectedCategory.Id : null
            };

            var minResult = await _productService.GetProductsAsync(new ProductListQuery
            {
                PageSize = priceRangeBaseQuery.PageSize,
                SearchText = priceRangeBaseQuery.SearchText,
                CategoryId = priceRangeBaseQuery.CategoryId,
                SortValue = "price_asc"
            });

            var maxResult = await _productService.GetProductsAsync(new ProductListQuery
            {
                PageSize = priceRangeBaseQuery.PageSize,
                SearchText = priceRangeBaseQuery.SearchText,
                CategoryId = priceRangeBaseQuery.CategoryId,
                SortValue = "price_desc"
            });

            if (minResult.Items.Count == 0 || maxResult.Items.Count == 0)
            {
                PriceRangeMinimum = 0;
                PriceRangeMaximum = 0;
                PriceRangeStepFrequency = 1;
                _hasInitializedPriceRange = false;

                MinPrice = 0;
                MaxPrice = 0;

                return;
            }

            var roundedMinimum = RoundPriceDown(minResult.Items[0].RetailPrice);
            var roundedMaximum = RoundPriceUp(maxResult.Items[0].RetailPrice);

            PriceRangeMinimum = roundedMinimum;
            PriceRangeMaximum = roundedMaximum;
            PriceRangeStepFrequency = CalculatePriceStepFrequency(roundedMinimum, roundedMaximum);

            if (selectionWasFullRange)
            {
                MinPrice = roundedMinimum;
                MaxPrice = roundedMaximum;
                _hasInitializedPriceRange = true;
                return;
            }

            MinPrice = Clamp(MinPrice, PriceRangeMinimum, PriceRangeMaximum);
            MaxPrice = Clamp(MaxPrice, PriceRangeMinimum, PriceRangeMaximum);

            if (MinPrice > MaxPrice)
            {
                MaxPrice = MinPrice;
            }
        }

        private bool ShouldApplyMinPriceFilter()
        {
            return PriceRangeMinimum > 0 && MinPrice > PriceRangeMinimum;
        }

        private bool ShouldApplyMaxPriceFilter()
        {
            return PriceRangeMaximum > 0 && MaxPrice < PriceRangeMaximum;
        }

        private static double RoundPriceDown(decimal price)
        {
            var value = (double)price;
            if (value <= 0)
            {
                return 0;
            }

            var step = GetPriceRoundingStep(value);
            return Math.Floor(value / step) * step;
        }

        private static double RoundPriceUp(decimal price)
        {
            var value = (double)price;
            if (value <= 0)
            {
                return 0;
            }

            var step = GetPriceRoundingStep(value);
            return Math.Ceiling(value / step) * step;
        }

        private static double GetPriceRoundingStep(double value)
        {
            var absValue = Math.Abs(value);

            if (absValue < 100_000)
            {
                return 10_000;
            }

            if (absValue < 10_000_000)
            {
                return 100_000;
            }

            return 1_000_000;
        }

        private static double CalculatePriceStepFrequency(double minimum, double maximum)
        {
            var range = maximum - minimum;
            if (range <= 0)
            {
                return 1;
            }

            if (range <= 1000)
            {
                return 100;
            }

            if (range <= 10000)
            {
                return 1000;
            }

            if (range <= 100000)
            {
                return 10000;
            }

            return 50000;
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            if (value < minimum)
            {
                return minimum;
            }

            if (value > maximum)
            {
                return maximum;
            }

            return value;
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
            SelectedSort = SortOptions.FirstOrDefault(option => option.Value == "id_desc") ?? SortOptions.FirstOrDefault();
            BeforeCursor = null;
            AfterCursor = null;
            if (_hasInitializedPriceRange)
            {
                MinPrice = PriceRangeMinimum;
                MaxPrice = PriceRangeMaximum;
            }
            else
            {
                MinPrice = 0;
                MaxPrice = 1000;
            }
            await LoadPageAsync(null);
        }

        [RelayCommand]
        public async Task<Product> CreateProductAsync(Product input)
        {
            EnsureAdminCanCreateProducts();

            var pendingImages = input.Images?.Where(i => ExtractPendingImagePath(i.ImageUrl) != null).ToList() ?? new List<ProductImage>();
            
            var safeImages = input.Images?.Where(i => ExtractPendingImagePath(i.ImageUrl) == null).ToList() ?? new List<ProductImage>();
            input.Images = new ObservableCollection<ProductImage>(safeImages);

            var createdProduct = await _productService.CreateProductAsync(input);
            var uploadedPublicIds = new List<string>();

            try
            {
                if (pendingImages.Any())
                {
                    foreach (var pendingImage in pendingImages)
                    {
                        var uploadResult = await _productImageUploadService.UploadProductImageAsync(createdProduct.Id, pendingImage.ImageUrl);
                        uploadedPublicIds.Add(uploadResult.PublicId);
                        
                        createdProduct.Images.Add(new ProductImage 
                        {
                            ImageUrl = uploadResult.ImageUrl,
                            IsPrimary = pendingImage.IsPrimary,
                            DisplayOrder = pendingImage.DisplayOrder
                        });
                    }

                    createdProduct = await _productService.UpdateProductAsync(createdProduct);
                }
            }
            catch
            {
                foreach (var publicId in uploadedPublicIds)
                {
                    await SafeDeleteProductImageAsync(publicId);
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
            var pendingImages = input.Images?.Where(i => ExtractPendingImagePath(i.ImageUrl) != null).ToList() ?? new List<ProductImage>();
            
            var rollbackSnapshot = _editingProductSnapshot is { Id: var snapshotId } && snapshotId == input.Id
                ? CloneProduct(_editingProductSnapshot)
                : null;

            var safeImages = input.Images?.Where(i => ExtractPendingImagePath(i.ImageUrl) == null).ToList() ?? new List<ProductImage>();
            input.Images = new ObservableCollection<ProductImage>(safeImages);

            var updatedProduct = await _productService.UpdateProductAsync(input);
            
            if (pendingImages.Any())
            {
                var uploadedPublicIds = new List<string>();
                try
                {
                    foreach (var pendingImage in pendingImages)
                    {
                        var uploadResult = await _productImageUploadService.UploadProductImageAsync(updatedProduct.Id, pendingImage.ImageUrl);
                        uploadedPublicIds.Add(uploadResult.PublicId);
                        
                        updatedProduct.Images.Add(new ProductImage 
                        {
                            ImageUrl = uploadResult.ImageUrl,
                            IsPrimary = pendingImage.IsPrimary,
                            DisplayOrder = pendingImage.DisplayOrder
                        });
                    }
                    updatedProduct = await _productService.UpdateProductAsync(updatedProduct);
                }
                catch
                {
                    foreach (var publicId in uploadedPublicIds)
                    {
                        await SafeDeleteProductImageAsync(publicId);
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
                Images = new ObservableCollection<ProductImage>(product.Images?.Select(i => new ProductImage 
                {
                    Id = i.Id,
                    ImageUrl = i.ImageUrl,
                    DisplayOrder = i.DisplayOrder,
                    IsPrimary = i.IsPrimary
                }) ?? Enumerable.Empty<ProductImage>()),
                IsDeleted = product.IsDeleted,
                CurrencySymbol = product.CurrencySymbol
            };

            _editingProductSnapshot = CloneProduct(product);
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

                var input = BuildEditableProductUpdateInput();

                await UpdateProductAsync(input);

                IsEditingProduct = false;
                EditingProduct = null;
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
            EnsureAdminCanCreateProducts();

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
                    var noResultFeedback = new ImportFeedback
                    {
                        ImportedCount = 0,
                        ErrorCount = 1,
                        HasErrors = true,
                        SummaryMessage = "Import failed: server did not return a result.",
                        DetailMessage = "Import failed: server did not return a result."
                    };

                    ImportErrorMessage = noResultFeedback.SummaryMessage;
                    await NotifyImportFeedbackAsync(noResultFeedback);
                    return;
                }

                var feedback = BuildImportFeedback(result);

                if (feedback.HasErrors)
                {
                    ImportErrorMessage = feedback.SummaryMessage;
                    await NotifyImportFeedbackAsync(feedback);
                }
                else
                {
                    ImportSuccessMessage = feedback.SummaryMessage;
                }

                await LoadProductsAsync();
            }
            catch (Exception ex)
            {
                var failedFeedback = new ImportFeedback
                {
                    ImportedCount = 0,
                    ErrorCount = 1,
                    HasErrors = true,
                    SummaryMessage = $"Import failed: {ex.Message}",
                    DetailMessage = $"Import failed: {ex.Message}"
                };

                ImportErrorMessage = failedFeedback.SummaryMessage;
                await NotifyImportFeedbackAsync(failedFeedback);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private static ImportFeedback BuildImportFeedback(ProductImportResult result)
        {
            var errorCount = result.Errors.Count;
            var hasErrors = errorCount > 0;

            var summary = hasErrors
                ? $"Imported {result.ImportedCount} product(s). Failed rows: {errorCount}."
                : $"Imported {result.ImportedCount} product(s) successfully.";

            var detailsBuilder = new StringBuilder();
            detailsBuilder.AppendLine($"Successful rows: {result.ImportedCount}");
            detailsBuilder.AppendLine($"Failed rows: {errorCount}");

            if (hasErrors)
            {
                detailsBuilder.AppendLine();
                detailsBuilder.AppendLine("Failed row details:");

                foreach (var error in result.Errors)
                {
                    detailsBuilder.Append("- ");
                    detailsBuilder.AppendLine(error);
                }
            }

            return new ImportFeedback
            {
                ImportedCount = result.ImportedCount,
                ErrorCount = errorCount,
                HasErrors = hasErrors,
                SummaryMessage = summary,
                DetailMessage = detailsBuilder.ToString().TrimEnd(),
                RequiresScrollableDialog = errorCount > 3
            };
        }

        private async Task NotifyImportFeedbackAsync(ImportFeedback feedback)
        {
            var handlers = ImportFeedbackRequested;
            if (handlers is null)
            {
                return;
            }

            foreach (var handler in handlers.GetInvocationList())
            {
                if (handler is Func<ImportFeedback, Task> asyncHandler)
                {
                    await asyncHandler(feedback);
                }
            }
        }

        public sealed class ImportFeedback
        {
            public int ImportedCount { get; set; }
            public int ErrorCount { get; set; }
            public bool HasErrors { get; set; }
            public bool RequiresScrollableDialog { get; set; }
            public string SummaryMessage { get; set; } = string.Empty;
            public string DetailMessage { get; set; } = string.Empty;
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
                Images = new ObservableCollection<ProductImage>(product.Images?.Select(i => new ProductImage 
                {
                    Id = i.Id,
                    ImageUrl = i.ImageUrl,
                    DisplayOrder = i.DisplayOrder,
                    IsPrimary = i.IsPrimary
                }) ?? Enumerable.Empty<ProductImage>()),
                IsDeleted = product.IsDeleted,
                CurrencySymbol = product.CurrencySymbol
            };
        }

        private Product BuildEditableProductUpdateInput()
        {
            if (EditingProduct is null)
            {
                throw new InvalidOperationException("No product is selected for editing.");
            }

            if (IsAdminUser || _editingProductSnapshot is null || _editingProductSnapshot.Id != EditingProduct.Id)
            {
                return new Product
                {
                    Id = EditingProduct.Id,
                    Name = EditingProduct.Name,
                    CategoryId = EditingProduct.CategoryId,
                    ImportPrice = EditingProduct.ImportPrice,
                    RetailPrice = EditingProduct.RetailPrice,
                    StockQuantity = EditingProduct.StockQuantity,
                    Images = EditingProduct.Images,
                    CurrencySymbol = EditingProduct.CurrencySymbol
                };
            }

            var originalSnapshot = CloneProduct(_editingProductSnapshot);
            originalSnapshot.StockQuantity = EditingProduct.StockQuantity;
            return originalSnapshot;
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

        private void EnsureAdminCanCreateProducts()
        {
            if (!CanCreateProducts)
            {
                throw new InvalidOperationException("Only admin users can add new products.");
            }
        }
    }

    public sealed class ProductAppConfigNode
    {
        public string CurrencySymbol { get; set; } = "USD";
    }
}
