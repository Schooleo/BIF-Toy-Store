using BIF.ToyStore.Core.Models;
using BIF.ToyStore.Infrastructure.Data;
using BIF.ToyStore.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;
using System.Collections.ObjectModel;
using System.Linq;

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
        public async Task bulkInsertAsync_existingName_updatesRecordAndReturnsChangedCount()
        {
            _dbContext.Products.Add(new Product
            {
                Name = "Lego",
                CategoryId = 1,
                RetailPrice = 20m,
                ImportPrice = 8m,
                StockQuantity = 5
            });
            await _dbContext.SaveChangesAsync();

            var count = await _repository.BulkInsertAsync(new List<Product>
            {
                new Product
                {
                    Name = "Lego",
                    CategoryId = 2,
                    RetailPrice = 25m,
                    ImportPrice = 10m,
                    StockQuantity = 7
                }
            });

            var updated = await _dbContext.Products.SingleAsync(p => p.Name == "Lego");

            Assert.Equal(1, count);
            Assert.Equal(2, updated.CategoryId);
            Assert.Equal(25m, updated.RetailPrice);
            Assert.NotEqual(8m, updated.ImportPrice);
            Assert.True(updated.StockQuantity > 5);
            Assert.Equal(1, _dbContext.Products.Count(p => p.Name == "Lego"));
        }

        [Fact]
        public async Task bulkInsertAsync_unchangedExistingRecord_returnsZero()
        {
            _dbContext.Products.Add(new Product
            {
                Name = "Robot",
                CategoryId = 1,
                RetailPrice = 50m,
                ImportPrice = 25m,
                StockQuantity = 4
            });
            await _dbContext.SaveChangesAsync();

            var count = await _repository.BulkInsertAsync(new List<Product>
            {
                new Product
                {
                    Name = "Robot",
                    CategoryId = 1,
                    RetailPrice = 50m,
                    ImportPrice = 25m,
                    StockQuantity = 4
                }
            });

            Assert.Equal(0, count);
            Assert.Equal(1, _dbContext.Products.Count(p => p.Name == "Robot"));
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
                Images = new ObservableCollection<ProductImage> 
                { 
                    new ProductImage { ImageUrl = "https://example.com/robot.png", IsPrimary = true } 
                }
            });

            Assert.Equal("https://example.com/robot.png", updated.Images.First().ImageUrl);
            Assert.Equal("https://example.com/robot.png", _dbContext.Products.Include(p => p.Images).Single(p => p.Id == 10).Images.First().ImageUrl);
        }
    }
}
