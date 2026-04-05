using BIF.ToyStore.Core.Models;

namespace BIF.ToyStore.Core.Interfaces
{
    public interface ICategoryService
    {
        Task<CategoryListResult> GetCategoriesAsync(CategoryListQuery query);

        Task<Category> CreateCategoryAsync(Category category);

        Task<Category> UpdateCategoryAsync(Category category);

        Task<bool> DeleteCategoryAsync(int id);

        Task<Category> RestoreCategoryAsync(int id);
    }
}
