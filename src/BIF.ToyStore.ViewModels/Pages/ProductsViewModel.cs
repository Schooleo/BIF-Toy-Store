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
    public partial class ProductsViewModel : BaseViewModel
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
            new SortOption { Name = "Newest", Value = "{ id: DESC }" },
            new SortOption { Name = "Price: Low to High", Value = "{ retailPrice: ASC }" },
            new SortOption { Name = "Price: High to Low", Value = "{ retailPrice: DESC }" },
            new SortOption { Name = "Stock: Low to High", Value = "{ stockQuantity: ASC }" },
            new SortOption { Name = "Name: A-Z", Value = "{ name: ASC }" }
        };

        // Paging properties
        [ObservableProperty]
        private int _pageSize = 5;

        [ObservableProperty]
        private int _totalCount;

        [ObservableProperty]
        private string? _beforeCursor;

        [ObservableProperty]
        private string? _afterCursor;

        [ObservableProperty]
        private bool _hasNextPage;

        [ObservableProperty]
        private bool _hasPreviousPage;

        [ObservableProperty]
        private string _importSuccessMessage = string.Empty;

        [ObservableProperty]
        private string _importErrorMessage = string.Empty;

        public bool HasImportSuccessMessage => !string.IsNullOrWhiteSpace(ImportSuccessMessage);
        public bool HasImportErrorMessage => !string.IsNullOrWhiteSpace(ImportErrorMessage);

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
                    categories {
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
            IsBusy = true;
            try
            {
                var filters = new List<string>();
                if (!string.IsNullOrWhiteSpace(SearchText))
                    filters.Add($"{{ name: {{ contains: \"{SearchText}\" }} }}");
                
                if (SelectedCategory != null)
                    filters.Add($"{{ categoryId: {{ eq: {SelectedCategory.Id} }} }}");
                
                if (MinPrice > 0)
                    filters.Add($"{{ retailPrice: {{ gte: {MinPrice} }} }}");
                
                if (MaxPrice < 1000)
                    filters.Add($"{{ retailPrice: {{ lte: {MaxPrice} }} }}");

                string filterString = filters.Count > 0 ? $"where: {{ and: [ {string.Join(", ", filters)} ] }}," : "";
                
                string sortString = SelectedSort?.Value ?? "{ id: DESC }";

                string pagingArgs = $"first: {PageSize}";
                if (direction == "next" && !string.IsNullOrEmpty(AfterCursor))
                    pagingArgs = $"first: {PageSize}, after: \"{AfterCursor}\"";
                else if (direction == "prev" && !string.IsNullOrEmpty(BeforeCursor))
                    pagingArgs = $"last: {PageSize}, before: \"{BeforeCursor}\"";
                else if (direction == "last")
                    pagingArgs = $"last: {PageSize}";

                string query = $@"
                    query GetProducts {{
                        products({pagingArgs}, {filterString} order: {sortString}) {{
                            totalCount
                            pageInfo {{
                                hasNextPage
                                hasPreviousPage
                                startCursor
                                endCursor
                            }}
                            nodes {{
                                id
                                name
                                categoryId
                                category {{
                                    id
                                    name
                                }}
                                retailPrice
                                importPrice
                                stockQuantity
                            }}
                        }}
                    }}";

                var result = await _graphQLClient.ExecuteAsync<ProductConnection>(query, dataKey: "products");

                if (result != null)
                {
                    Products = new ObservableCollection<Product>(result.Nodes ?? new List<Product>());
                    TotalCount = result.TotalCount;
                    HasNextPage = result.PageInfo?.HasNextPage ?? false;
                    HasPreviousPage = result.PageInfo?.HasPreviousPage ?? false;
                    BeforeCursor = result.PageInfo?.StartCursor;
                    AfterCursor = result.PageInfo?.EndCursor;
                }

                // Explicitly notify pagination commands that execution state may have changed
                FirstPageCommand.NotifyCanExecuteChanged();
                PreviousPageCommand.NotifyCanExecuteChanged();
                NextPageCommand.NotifyCanExecuteChanged();
                LastPageCommand.NotifyCanExecuteChanged();
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
            await LoadProductsAsync();
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
            await LoadProductsAsync();
        }

        [RelayCommand(CanExecute = nameof(HasNextPage))]
        public async Task NextPageAsync() => await LoadProductsAsync("next");

        [RelayCommand(CanExecute = nameof(HasPreviousPage))]
        public async Task PreviousPageAsync() => await LoadProductsAsync("prev");

        [RelayCommand(CanExecute = nameof(HasPreviousPage))]
        public async Task FirstPageAsync()
        {
            BeforeCursor = null;
            AfterCursor = null;
            await LoadProductsAsync("first");
        }

        [RelayCommand(CanExecute = nameof(HasNextPage))]
        public async Task LastPageAsync()
        {
            BeforeCursor = null;
            AfterCursor = null;
            await LoadProductsAsync("last");
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
