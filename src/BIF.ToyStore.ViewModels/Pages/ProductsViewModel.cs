using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.ViewModels.Base;
using BIF.ToyStore.ViewModels.Utils;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Collections.Generic;
using BIF.ToyStore.Infrastructure.GraphQL;

namespace BIF.ToyStore.ViewModels.Pages
{
    public partial class ProductsViewModel : BaseViewModel
    {
        private readonly IGraphQLClient _graphQLClient;
        private readonly ILocalSettingsService _localSettingsService;

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
        private decimal? _minPrice;

        [ObservableProperty]
        private decimal? _maxPrice;

        // Paging properties
        [ObservableProperty]
        private int _pageSize = 20;

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

        public ProductsViewModel(IGraphQLClient graphQLClient, ILocalSettingsService localSettingsService)
        {
            _graphQLClient = graphQLClient;
            _localSettingsService = localSettingsService;
            Title = "Product Management";
            
            PageSize = _localSettingsService.GetInt(AppPreferenceKeys.ProductsItemsPerPage, 20);
        }

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
                
                if (MinPrice.HasValue)
                    filters.Add($"{{ retailPrice: {{ gte: {MinPrice.Value} }} }}");
                
                if (MaxPrice.HasValue)
                    filters.Add($"{{ retailPrice: {{ lte: {MaxPrice.Value} }} }}");

                string filterString = filters.Count > 0 ? $"where: {{ and: [ {string.Join(", ", filters)} ] }}," : "";
                
                string pagingArgs = $"first: {PageSize}";
                if (direction == "next" && !string.IsNullOrEmpty(AfterCursor))
                    pagingArgs = $"first: {PageSize}, after: \"{AfterCursor}\"";
                else if (direction == "prev" && !string.IsNullOrEmpty(BeforeCursor))
                    pagingArgs = $"last: {PageSize}, before: \"{BeforeCursor}\"";

                string query = $@"
                    query GetProducts {{
                        products({pagingArgs}, {filterString} order: {{ id: DESC }}) {{
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
            MinPrice = null;
            MaxPrice = null;
            BeforeCursor = null;
            AfterCursor = null;
            await LoadProductsAsync();
        }

        [RelayCommand(CanExecute = nameof(HasNextPage))]
        public async Task NextPageAsync() => await LoadProductsAsync("next");

        [RelayCommand(CanExecute = nameof(HasPreviousPage))]
        public async Task PreviousPageAsync() => await LoadProductsAsync("prev");

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
        public async Task ImportProductsAsync(object file)
        {
            IsBusy = true;
            try
            {
                const string query = @"
                    mutation Import($file: Upload!) {
                        importProducts(file: $file) {
                            importedCount
                            errors
                        }
                    }";
                var result = await _graphQLClient.ExecuteAsync<ImportProductsPayload>(query, new { file }, dataKey: "importProducts");
                if (result != null)
                {
                    await LoadProductsAsync();
                }
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
    }
}
