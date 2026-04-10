using BIF.ToyStore.Core.Models;

namespace BIF.ToyStore.Core.Interfaces
{
    public interface IProductService
    {
        Task<IReadOnlyList<Category>> GetCategoriesAsync(int take = 250);

        Task<ProductListResult> GetProductsAsync(ProductListQuery query);

        Task<Product> CreateProductAsync(Product product);

        Task<Product> UpdateProductAsync(Product product);

        Task<bool> DeleteProductAsync(int id);

        Task<ProductImportResult> ImportProductsAsync(string filePath);
    }
}
