using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;

namespace BIF.ToyStore.Infrastructure.Services
{
	public class ProductService(IProductApiRepository productApiRepository) : IProductService
	{
		private readonly IProductApiRepository _productApiRepository = productApiRepository;

		public async Task<IReadOnlyList<Category>> GetCategoriesAsync(int take = 250)
		{
			return await _productApiRepository.GetCategoriesAsync(take);
		}

		public async Task<ProductListResult> GetProductsAsync(ProductListQuery query)
		{
			return await _productApiRepository.GetProductsAsync(query);
		}

		public async Task<Product> CreateProductAsync(Product product)
		{
			return await _productApiRepository.CreateProductAsync(product);
		}

		public async Task<Product> UpdateProductAsync(Product product)
		{
			return await _productApiRepository.UpdateProductAsync(product);
		}

		public async Task<bool> DeleteProductAsync(int id)
		{
			return await _productApiRepository.DeleteProductAsync(id);
		}

		public async Task<ProductImportResult> ImportProductsAsync(string filePath)
		{
			return await _productApiRepository.ImportProductsAsync(filePath);
		}
	}
}
