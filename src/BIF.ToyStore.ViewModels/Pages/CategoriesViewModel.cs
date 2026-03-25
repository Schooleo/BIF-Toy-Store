using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.Core.Settings;
using BIF.ToyStore.ViewModels.Base;
using BIF.ToyStore.Infrastructure.GraphQL;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace BIF.ToyStore.ViewModels.Pages
{
    public partial class CategoriesViewModel : BaseViewModel
    {
        private readonly IGraphQLClient _graphQLClient;

        [ObservableProperty]
        private ObservableCollection<Category> _categories = [];

        // Filter
        [ObservableProperty]
        private string _searchText = string.Empty;

        // Paging
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

        public CategoriesViewModel(IGraphQLClient graphQLClient)
        {
            _graphQLClient = graphQLClient;
            Title = "Category Management";
        }

        [RelayCommand]
        public async Task LoadCategoriesAsync(string? direction = null)
        {
            IsBusy = true;
            try
            {
                string filterString = !string.IsNullOrWhiteSpace(SearchText)
                    ? $"where: {{ name: {{ contains: \"{SearchText}\" }} }},"
                    : "";

                string pagingArgs = $"first: {PageSize}";
                if (direction == "next" && !string.IsNullOrEmpty(AfterCursor))
                    pagingArgs = $"first: {PageSize}, after: \"{AfterCursor}\"";
                else if (direction == "prev" && !string.IsNullOrEmpty(BeforeCursor))
                    pagingArgs = $"last: {PageSize}, before: \"{BeforeCursor}\"";

                string query = $@"
                    query GetCategories {{
                        categories({pagingArgs}, {filterString} order: {{ id: ASC }}) {{
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
                                products {{
                                    id
                                    name
                                }}
                            }}
                        }}
                    }}";

                var result = await _graphQLClient.ExecuteAsync<CategoryConnection>(query, dataKey: "categories");

                if (result != null)
                {
                    Categories = new ObservableCollection<Category>(result.Nodes ?? new List<Category>());
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
            await LoadCategoriesAsync();
        }

        [RelayCommand]
        public async Task ClearFilterAsync()
        {
            SearchText = string.Empty;
            BeforeCursor = null;
            AfterCursor = null;
            await LoadCategoriesAsync();
        }

        [RelayCommand(CanExecute = nameof(HasNextPage))]
        public async Task NextPageAsync() => await LoadCategoriesAsync("next");

        [RelayCommand(CanExecute = nameof(HasPreviousPage))]
        public async Task PreviousPageAsync() => await LoadCategoriesAsync("prev");

        [RelayCommand]
        public async Task CreateCategoryAsync(CreateCategoryInput input)
        {
            const string query = @"
                mutation Create($input: CreateCategoryInput!) {
                    createCategory(input: $input) {
                        id
                        name
                    }
                }";
            await _graphQLClient.ExecuteAsync<Category>(query, new { input }, dataKey: "createCategory");
            await LoadCategoriesAsync();
        }

        [RelayCommand]
        public async Task UpdateCategoryAsync(UpdateCategoryInput input)
        {
            const string query = @"
                mutation Update($input: UpdateCategoryInput!) {
                    updateCategory(input: $input) {
                        id
                        name
                    }
                }";
            await _graphQLClient.ExecuteAsync<Category>(query, new { input }, dataKey: "updateCategory");
            await LoadCategoriesAsync();
        }

        [RelayCommand]
        public async Task DeleteCategoryAsync(int id)
        {
            if (id == AppConstants.OtherCategoryId)
                return; // Do not allow deleting the "Other" category

            const string query = @"
                mutation Delete($id: Int!) {
                    deleteCategory(id: $id)
                }";
            await _graphQLClient.ExecuteAsync<bool>(query, new { id }, dataKey: "deleteCategory");
            await LoadCategoriesAsync();
        }

        [RelayCommand]
        public async Task RestoreCategoryAsync(int id)
        {
            const string query = @"
                mutation Restore($id: Int!) {
                    restoreCategory(id: $id) {
                        id
                        name
                    }
                }";
            await _graphQLClient.ExecuteAsync<Category>(query, new { id }, dataKey: "restoreCategory");
            await LoadCategoriesAsync();
        }

        // Helper classes for GraphQL response mapping
        public class CategoryConnection
        {
            public int TotalCount { get; set; }
            public PageInfo? PageInfo { get; set; }
            public List<Category>? Nodes { get; set; }
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
