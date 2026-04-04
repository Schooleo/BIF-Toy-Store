using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.Core.Settings;
using BIF.ToyStore.ViewModels.Base;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace BIF.ToyStore.ViewModels.Pages
{
    public partial class CategoriesViewModel : PaginatedViewModel
    {
        private readonly ICategoryService _categoryService;

        [ObservableProperty]
        private ObservableCollection<Category> _categories = [];

        // Filter
        [ObservableProperty]
        private string _searchText = string.Empty;

        // Computed property for total count label (notifies when TotalCount changes)
        public new string TotalCountLabel => $"Total categories in catalog: {TotalCount}";

        public CategoriesViewModel(ICategoryService categoryService)
        {
            _categoryService = categoryService;
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
                var result = await _categoryService.GetCategoriesAsync(new CategoryListQuery
                {
                    PageSize = PageSize,
                    Direction = direction,
                    AfterCursor = AfterCursor,
                    BeforeCursor = BeforeCursor,
                    SearchText = SearchText
                });

                Categories = new ObservableCollection<Category>(result.Items);
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
            BeforeCursor = null;
            AfterCursor = null;
            await LoadPageAsync(null);
        }

        [RelayCommand]
        public async Task CreateCategoryAsync(Category input)
        {
            await _categoryService.CreateCategoryAsync(input);
            await LoadCategoriesAsync();
        }

        [RelayCommand]
        public async Task UpdateCategoryAsync(Category input)
        {
            await _categoryService.UpdateCategoryAsync(input);
            await LoadCategoriesAsync();
        }

        [RelayCommand]
        public async Task DeleteCategoryAsync(int id)
        {
            if (id == AppConstants.OtherCategoryId)
                return; // Do not allow deleting the "Other" category

            await _categoryService.DeleteCategoryAsync(id);
            await LoadCategoriesAsync();
        }

        [RelayCommand]
        public async Task RestoreCategoryAsync(int id)
        {
            await _categoryService.RestoreCategoryAsync(id);
            await LoadCategoriesAsync();
        }
    }
}
