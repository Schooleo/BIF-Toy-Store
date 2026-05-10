using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.Core.Settings;
using BIF.ToyStore.Core.Enums;
using BIF.ToyStore.ViewModels.Base;
using BIF.ToyStore.ViewModels.Utils;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace BIF.ToyStore.ViewModels.Pages
{
    public partial class CategoriesViewModel : PaginatedViewModel
    {
        private readonly ICategoryService _categoryService;
        private readonly ILocalSettingsService _localSettingsService;

        [ObservableProperty]
        private ObservableCollection<Category> _categories = [];

        // Filter
        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private bool _isAdminUser = true;

        // Computed property for total count label (notifies when TotalCount changes)
        public new string TotalCountLabel => $"Total categories in catalog: {TotalCount}";
        public bool CanCreateCategories => IsAdminUser;

        public CategoriesViewModel(ICategoryService categoryService, ILocalSettingsService localSettingsService)
        {
            _categoryService = categoryService;
            _localSettingsService = localSettingsService;
            Title = "Category Management";
            IsAdminUser = Enum.TryParse<UserRole>(
                _localSettingsService.GetString(AppPreferenceKeys.CurrentUserRole, UserRole.Admin.ToString()),
                true,
                out var currentRole)
                && currentRole == UserRole.Admin;
        }

        partial void OnIsAdminUserChanged(bool value) => OnPropertyChanged(nameof(CanCreateCategories));

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
            EnsureAdminCanCreateCategories();
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
            if (id <= 0 || id == AppConstants.OtherCategoryId)
                return; // Do not allow invalid ids or deleting the "Other" category

            await _categoryService.DeleteCategoryAsync(id);
            await LoadCategoriesAsync();
        }

        [RelayCommand]
        public async Task RestoreCategoryAsync(int id)
        {
            await _categoryService.RestoreCategoryAsync(id);
            await LoadCategoriesAsync();
        }

        private void EnsureAdminCanCreateCategories()
        {
            if (!CanCreateCategories)
            {
                throw new InvalidOperationException("Only admin users can add new categories.");
            }
        }
    }
}
