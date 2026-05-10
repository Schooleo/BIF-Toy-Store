using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.Infrastructure.Services;

namespace BIF.ToyStore.Tests.Services
{
    public class ProductServiceTests
    {
        [Fact]
        public async Task GetCategoriesAsync_UsesDefaultTakeValue()
        {
            var expectedCategories = new List<Category> { new() { Id = 1, Name = "Other" } };
            var repository = new RecordingProductApiRepository(expectedCategories);
            var service = new ProductService(repository);

            var result = await service.GetCategoriesAsync();

            Assert.Same(expectedCategories, result);
            Assert.Equal(250, repository.LastTake);
        }

        private sealed class RecordingProductApiRepository : IProductApiRepository
        {
            private readonly IReadOnlyList<Category> _categories;

            public RecordingProductApiRepository(IReadOnlyList<Category> categories)
            {
                _categories = categories;
            }

            public int? LastTake { get; private set; }

            public Task<IReadOnlyList<Category>> GetCategoriesAsync(int take = 50)
            {
                LastTake = take;
                return Task.FromResult(_categories);
            }

            public Task<ProductListResult> GetProductsAsync(ProductListQuery query) => throw new NotSupportedException();

            public Task<Product> CreateProductAsync(Product product) => throw new NotSupportedException();

            public Task<Product> UpdateProductAsync(Product product) => throw new NotSupportedException();

            public Task<bool> DeleteProductAsync(int id) => throw new NotSupportedException();

            public Task<ProductImportResult> ImportProductsAsync(string filePath) => throw new NotSupportedException();
        }
    }
}
