using BIF.ToyStore.Core.Models;
using BIF.ToyStore.Infrastructure.Data;
using BIF.ToyStore.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace BIF.ToyStore.Tests.Repositories
{
    public class ProductRepositoryTests : IDisposable
    {
        private readonly AppDbContext _dbContext;
        private readonly ProductRepository _repository;

        public ProductRepositoryTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _dbContext = new AppDbContext(options);
            _repository = new ProductRepository(_dbContext);
        }

        public void Dispose() => _dbContext.Dispose();
        
        [Fact]
        public async Task bulkInsertAsync_validProductsList_returnsCorrectInsertedCount()
        {
            var products = new List<Product>
            {
                new Product { Id = 1, Name = "Búp bê", CategoryId = 1, RetailPrice = 10 },
                new Product { Id = 2, Name = "Lego", CategoryId = 1, RetailPrice = 20 }
            };
            var count = await _repository.BulkInsertAsync(products);
            Assert.Equal(2, count);
            Assert.Equal(2, _dbContext.Products.Count());
        }

        [Fact]
        public async Task UpdateDetailsAsync_UpdatesImageUrl()
        {
            var product = new Product
            {
                Id = 10,
                Name = "Robot",
                CategoryId = 1,
                RetailPrice = 50m,
                ImportPrice = 25m,
                StockQuantity = 4
            };

            _dbContext.Products.Add(product);
            await _dbContext.SaveChangesAsync();

            var updated = await _repository.UpdateDetailsAsync(new Product
            {
                Id = 10,
                Name = "Robot",
                CategoryId = 1,
                RetailPrice = 50m,
                ImportPrice = 25m,
                StockQuantity = 4,
                ImageUrl = "https://example.com/robot.png"
            });

            Assert.Equal("https://example.com/robot.png", updated.ImageUrl);
            Assert.Equal("https://example.com/robot.png", _dbContext.Products.Single(p => p.Id == 10).ImageUrl);
        }
    }
}
