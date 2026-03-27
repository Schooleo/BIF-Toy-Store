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
    public partial class CategoriesViewModel : PaginatedViewModel
    {
        private readonly IGraphQLClient _graphQLClient;

        [ObservableProperty]
        private ObservableCollection<Category> _categories = [];

        // Filter
        [ObservableProperty]
        private string _searchText = string.Empty;

        public CategoriesViewModel(IGraphQLClient graphQLClient)
        {
            _graphQLClient = graphQLClient;
            Title = "Category Management";
        }

        [RelayCommand]
        public async Task LoadCategoriesAsync(string? direction = null)
        {
            await LoadPageAsync(direction);
        }

        protected override async Task LoadPageAsync(string? direction)
        {
            IsBusy = true;
            try
            {
                const string queryTemplate = @"
                    query GetCategories(
                        $first: Int, $last: Int, $after: String, $before: String,
                        $where: CategoryFilterInput, $order: [CategorySortInput!]
                    ) {
                        categories(
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
                                products {
                                    id
                                    name
                                }
                            }
                        }
                    }";

                object? whereClause = !string.IsNullOrWhiteSpace(SearchText)
                    ? new { name = new { contains = SearchText } }
                    : null;

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
                    order = new[] { new { id = "ASC" } }
                };

                var result = await _graphQLClient.ExecuteAsync<CategoryConnection>(queryTemplate, variables, dataKey: "categories");

                if (result != null)
                {
                    Categories = new ObservableCollection<Category>(result.Nodes ?? new List<Category>());
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
            BeforeCursor = null;
            AfterCursor = null;
            await LoadPageAsync(null);
        }

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
