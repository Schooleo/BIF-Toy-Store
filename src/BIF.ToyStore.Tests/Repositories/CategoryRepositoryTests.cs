using BIF.ToyStore.Core.Models;
using BIF.ToyStore.Infrastructure.Data;
using BIF.ToyStore.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BIF.ToyStore.Tests.Repositories
{
    public class CategoryRepositoryTests : IDisposable
    {
        private readonly AppDbContext _dbContext;
        private readonly CategoryRepository _repository;

        public CategoryRepositoryTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _dbContext = new AppDbContext(options);
            _repository = new CategoryRepository(_dbContext);
        }

        public void Dispose()
        {
            _dbContext.Dispose();
        }

        [Fact]
        public async Task RestoreAsync_ExistingSoftDeletedCategory_RestoresCategory()
        {
            var category = new Category
            {
                Id = 10,
                Name = "Seasonal",
                IsDeleted = true
            };
            _dbContext.Categories.Add(category);
            await _dbContext.SaveChangesAsync();

            var restored = await _repository.RestoreAsync(10);

            Assert.False(restored.IsDeleted);

            var inDb = await _dbContext.Categories
                .IgnoreQueryFilters()
                .SingleAsync(c => c.Id == 10);
            Assert.False(inDb.IsDeleted);
        }

        [Fact]
        public async Task RestoreAsync_MissingCategory_ThrowsInvalidOperationException()
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _repository.RestoreAsync(404));

            Assert.Contains("Category with ID 404 not found.", ex.Message);
        }
    }
}
