using BIF.ToyStore.Core.Models;
using BIF.ToyStore.Infrastructure.Data;
using BIF.ToyStore.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BIF.ToyStore.Tests.Repositories
{
    /// <summary>
    /// Tests for BaseRepository using the Product entity as the type parameter.
    /// All 5 CRUD operations (GetById, GetAll, Add, Update, Delete) are covered.
    /// </summary>
    public class BaseRepositoryTests : IDisposable
    {
        private readonly AppDbContext _dbContext;
        private readonly BaseRepository<Product> _repository;

        public BaseRepositoryTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _dbContext = new AppDbContext(options);
            _repository = new BaseRepository<Product>(_dbContext);
        }

        public void Dispose() => _dbContext.Dispose();

        // ─── GetByIdAsync ────────────────────────────────────────────────────────

        [Fact]
        public async Task GetByIdAsync_ExistingId_ReturnsEntity()
        {
            // Arrange
            var product = new Product { Id = 1, Name = "Lego Set", RetailPrice = 49.99m, StockQuantity = 10 };
            _dbContext.Products.Add(product);
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _repository.GetByIdAsync(1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Lego Set", result.Name);
        }

        [Fact]
        public async Task GetByIdAsync_NonExistentId_ReturnsNull()
        {
            // Act
            var result = await _repository.GetByIdAsync(999);

            // Assert
            Assert.Null(result);
        }

        // ─── GetAllAsync ─────────────────────────────────────────────────────────

        [Fact]
        public async Task GetAllAsync_EmptyDatabase_ReturnsEmptyCollection()
        {
            // Act
            var result = await _repository.GetAllAsync();

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetAllAsync_MultipleEntities_ReturnsAll()
        {
            // Arrange
            _dbContext.Products.AddRange(
                new Product { Id = 2, Name = "Action Figure", RetailPrice = 19.99m },
                new Product { Id = 3, Name = "Puzzle", RetailPrice = 14.99m },
                new Product { Id = 4, Name = "Board Game", RetailPrice = 34.99m }
            );
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _repository.GetAllAsync();

            // Assert
            Assert.Equal(3, result.Count());
        }

        // ─── AddAsync ────────────────────────────────────────────────────────────

        [Fact]
        public async Task AddAsync_ValidEntity_PersistsToDatabase()
        {
            // Arrange
            var product = new Product { Id = 5, Name = "Remote Car", RetailPrice = 59.99m, StockQuantity = 5 };

            // Act
            var returned = await _repository.AddAsync(product);

            // Assert
            Assert.NotNull(returned);
            Assert.Equal("Remote Car", returned.Name);

            var inDb = await _dbContext.Products.FindAsync(5);
            Assert.NotNull(inDb);
        }

        [Fact]
        public async Task AddAsync_ValidEntity_ReturnsAddedEntity()
        {
            // Arrange
            var product = new Product { Id = 6, Name = "Doll House", RetailPrice = 89.99m };

            // Act
            var result = await _repository.AddAsync(product);

            // Assert – returned object is the same entity
            Assert.Equal(product.Id, result.Id);
            Assert.Equal(product.Name, result.Name);
        }

        // ─── UpdateAsync ─────────────────────────────────────────────────────────

        [Fact]
        public async Task UpdateAsync_ExistingEntity_ChangesAreSaved()
        {
            // Arrange
            var product = new Product { Id = 7, Name = "Old Name", RetailPrice = 10m };
            _dbContext.Products.Add(product);
            await _dbContext.SaveChangesAsync();

            // Act
            product.Name = "Updated Name";
            product.RetailPrice = 25m;
            await _repository.UpdateAsync(product);

            // Assert
            var updated = await _dbContext.Products.FindAsync(7);
            Assert.Equal("Updated Name", updated!.Name);
            Assert.Equal(25m, updated.RetailPrice);
        }

        // ─── DeleteAsync ─────────────────────────────────────────────────────────

        [Fact]
        public async Task DeleteAsync_ExistingId_RemovesEntityFromDatabase()
        {
            // Arrange
            _dbContext.Products.Add(new Product { Id = 8, Name = "To Delete", RetailPrice = 5m });
            await _dbContext.SaveChangesAsync();

            // Act
            await _repository.DeleteAsync(8);

            // Assert
            var result = await _dbContext.Products.FindAsync(8);
            Assert.Null(result);
        }

        [Fact]
        public async Task DeleteAsync_NonExistentId_DoesNotThrow()
        {
            // Act & Assert – deleting a non-existent ID should be a no-op
            var exception = await Record.ExceptionAsync(() => _repository.DeleteAsync(9999));
            Assert.Null(exception);
        }

        [Fact]
        public async Task DeleteAsync_ExistingId_OtherEntitiesUnaffected()
        {
            // Arrange
            _dbContext.Products.AddRange(
                new Product { Id = 9, Name = "Keep Me", RetailPrice = 5m },
                new Product { Id = 10, Name = "Delete Me", RetailPrice = 5m }
            );
            await _dbContext.SaveChangesAsync();

            // Act
            await _repository.DeleteAsync(10);

            // Assert
            var remaining = await _dbContext.Products.ToListAsync();
            Assert.Single(remaining);
            Assert.Equal("Keep Me", remaining[0].Name);
        }
    }
}
