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
using BIF.ToyStore.Infrastructure.GraphQL;

namespace BIF.ToyStore.ViewModels.Pages
{
    public partial class ProductsViewModel : PaginatedViewModel
    {
        private readonly IGraphQLClient _graphQLClient;
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

        public bool HasImportSuccessMessage => !string.IsNullOrWhiteSpace(ImportSuccessMessage);
        public bool HasImportErrorMessage => !string.IsNullOrWhiteSpace(ImportErrorMessage);

        // Computed property for total count label (notifies when TotalCount changes)
        public new string TotalCountLabel => $"Total items in catalog: {TotalCount} Units";

        public ProductsViewModel(
            IGraphQLClient graphQLClient,
            ILocalSettingsService localSettingsService,
            IExcelFilePickerService excelFilePickerService)
        {
            _graphQLClient = graphQLClient;
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

        [RelayCommand]
        public async Task LoadCategoriesAsync()
        {
            const string query = @"
                query GetCategories {
                    categories(first: 50) {
                        nodes {
                            id
                            name
                        }
                    }
                }";

            var result = await _graphQLClient.ExecuteAsync<CategoryConnection>(query, dataKey: "categories");
            if (result?.Nodes != null)
            {
                Categories = new ObservableCollection<Category>(result.Nodes);
            }
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
                const string queryTemplate = @"
                    query GetProducts(
                        $first: Int, $last: Int, $after: String, $before: String,
                        $where: ProductFilterInput, $order: [ProductSortInput!]
                    ) {
                        products(
                            first: $first,
                            last: $last,
                            after: $after,
                            before: $before,
                            where: $where,
                            order: $order
                        ) {
                            totalCount
                            pageInfo {
                                hasNextPage
                                hasPreviousPage
                                startCursor
                                endCursor
                            }
                            nodes {
                                id
                                name
                                categoryId
                                category {
                                    id
                                    name
                                }
                                retailPrice
                                importPrice
                                stockQuantity
                            }
                        }
                    }";

                var whereConditions = new List<object>();

                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    whereConditions.Add(new { name = new { contains = SearchText } });
                }

                if (SelectedCategory != null)
                {
                    whereConditions.Add(new { categoryId = new { eq = SelectedCategory.Id } });
                }

                if (MinPrice > 0)
                {
                    whereConditions.Add(new { retailPrice = new { gte = (decimal)MinPrice } });
                }

                if (MaxPrice < 1000)
                {
                    whereConditions.Add(new { retailPrice = new { lte = (decimal)MaxPrice } });
                }

                object? whereClause = whereConditions.Count > 0
                    ? new { and = whereConditions }
                    : null;

                object orderClause = SelectedSort?.Value switch
                {
                    "price_asc" => new[] { new { retailPrice = "ASC" } },
                    "price_desc" => new[] { new { retailPrice = "DESC" } },
                    "stock_asc" => new[] { new { stockQuantity = "ASC" } },
                    "name_asc" => new[] { new { name = "ASC" } },
                    _ => new[] { new { id = "DESC" } }
                };

                int? firstVar = null;
                int? lastVar = null;
                string? afterVar = null;
                string? beforeVar = null;

                if (direction == "next" && !string.IsNullOrEmpty(AfterCursor))
                {
                    firstVar = PageSize;
                    afterVar = AfterCursor;
                }
                else if (direction == "prev" && !string.IsNullOrEmpty(BeforeCursor))
                {
                    lastVar = PageSize;
                    beforeVar = BeforeCursor;
                }
                else if (direction == "last")
                {
                    lastVar = PageSize;
                }
                else
                {
                    firstVar = PageSize;
                }

                var variables = new
                {
                    first = firstVar,
                    last = lastVar,
                    after = afterVar,
                    before = beforeVar,
                    where = whereClause,
                    order = orderClause
                };

                var result = await _graphQLClient.ExecuteAsync<ProductConnection>(queryTemplate, variables, dataKey: "products");

                if (result != null)
                {
                    Products = new ObservableCollection<Product>(result.Nodes ?? new List<Product>());
                    ApplyPageInfo(result.TotalCount, result.PageInfo?.HasNextPage ?? false,
                        result.PageInfo?.HasPreviousPage ?? false,
                        result.PageInfo?.StartCursor, result.PageInfo?.EndCursor);
                }
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
        public async Task CreateProductAsync(CreateProductInput input)
        {
            const string query = @"
                mutation Create($input: CreateProductInput!) {
                    createProduct(input: $input) {
                        id
                    }
                }";
            await _graphQLClient.ExecuteAsync<Product>(query, new { input }, dataKey: "createProduct");
            await LoadProductsAsync();
        }

        [RelayCommand]
        public async Task UpdateProductAsync(UpdateProductInput input)
        {
            const string query = @"
                mutation Update($input: UpdateProductInput!) {
                    updateProduct(input: $input) {
                        id
                    }
                }";
            await _graphQLClient.ExecuteAsync<Product>(query, new { input }, dataKey: "updateProduct");
            await LoadProductsAsync();
        }

        [RelayCommand]
        public async Task DeleteProductAsync(int id)
        {
            const string query = @"
                mutation Delete($id: Int!) {
                    deleteProduct(id: $id)
                }";
            await _graphQLClient.ExecuteAsync<bool>(query, new { id }, dataKey: "deleteProduct");
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

                const string query = @"
                    mutation Import($file: Upload!) {
                        importProducts(file: $file) {
                            importedCount
                            errors
                        }
                    }";

                var result = await _graphQLClient.UploadFileAsync<ImportProductsPayload>(
                    query,
                    "file",
                    selectedFilePath,
                    dataKey: "importProducts");

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

        // Helper classes for GraphQL response mapping
        public class CategoryConnection { public List<Category>? Nodes { get; set; } }
        public class ProductConnection 
        { 
            public int TotalCount { get; set; }
            public PageInfo? PageInfo { get; set; }
            public List<Product>? Nodes { get; set; } 
        }
        public class PageInfo
        {
            public bool HasNextPage { get; set; }
            public bool HasPreviousPage { get; set; }
            public string? StartCursor { get; set; }
            public string? EndCursor { get; set; }
        }

        public class SortOption
        {
            public string Name { get; set; } = string.Empty;
            public string Value { get; set; } = string.Empty;
        }
    }
}
