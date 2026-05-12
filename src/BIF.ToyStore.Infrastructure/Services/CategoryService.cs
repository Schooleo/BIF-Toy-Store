using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;

namespace BIF.ToyStore.Infrastructure.Services
{
	public class CategoryService(ICategoryApiRepository categoryApiRepository) : ICategoryService
	{
		private readonly ICategoryApiRepository _categoryApiRepository = categoryApiRepository;

		public async Task<CategoryListResult> GetCategoriesAsync(CategoryListQuery query)
		{
			return await _categoryApiRepository.GetCategoriesAsync(query);
		}

		public async Task<Category> CreateCategoryAsync(Category category)
		{
			return await _categoryApiRepository.CreateCategoryAsync(category);
		}

		public async Task<Category> UpdateCategoryAsync(Category category)
		{
			return await _categoryApiRepository.UpdateCategoryAsync(category);
		}

		public async Task<bool> DeleteCategoryAsync(int id)
		{
			return await _categoryApiRepository.DeleteCategoryAsync(id);
		}

		public async Task<Category> RestoreCategoryAsync(int id)
		{
			return await _categoryApiRepository.RestoreCategoryAsync(id);
		}
	}
}
