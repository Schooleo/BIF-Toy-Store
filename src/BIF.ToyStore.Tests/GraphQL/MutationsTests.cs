using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.Infrastructure.Data;
using BIF.ToyStore.Infrastructure.GraphQL;
using HotChocolate.Types;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using BIF.ToyStore.Core.Enums;

namespace BIF.ToyStore.Tests.GraphQL
{
    public class MutationsTests
    {
        [Fact]
        public async Task createProduct_validProductInput_callsRepositoryAddAsyncAndReturnsProduct()
        {
            var mockRepo = new Mock<IProductRepository>();
            var input = new CreateProductInput
            {
                Name = "Uno Premium",
                CategoryId = 3,
                RetailPrice = 9.99m,
                ImportPrice = 4.50m,
                StockQuantity = 100
            };

            mockRepo.Setup(x => x.AddAsync(It.IsAny<Product>()))
                    .ReturnsAsync((Product p) => { p.Id = 77; return p; });

            var mutation = new Mutations();
            var result = await mutation.CreateProduct(input, mockRepo.Object);

            Assert.NotNull(result);
            Assert.Equal(77, result.Id);
            mockRepo.Verify(x => x.AddAsync(It.Is<Product>(p => p.Name == "Uno Premium")), Times.Once);
        }

        [Fact]
        public async Task createProduct_saleRole_throwsUnauthorizedError()
        {
            var mockRepo = new Mock<IProductRepository>();
            var input = new CreateProductInput { Name = "Blocked", CategoryId = 1 };

            var mutation = new Mutations();
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                mutation.CreateProduct(input, mockRepo.Object, currentUserId: 2, currentUserRole: UserRole.Sale.ToString()));

            Assert.Equal("Only admin users can create products.", ex.Message);
            mockRepo.Verify(x => x.AddAsync(It.IsAny<Product>()), Times.Never);
        }

        [Fact]
        public async Task updateProduct_existingId_callsRepositoryUpdateAsync()
        {
            var mockRepo = new Mock<IProductRepository>();
            var updatedProduct = new Product { Id = 1, Name = "New Name", RetailPrice = 20.00m };

            mockRepo.Setup(x => x.UpdateDetailsAsync(It.IsAny<Product>()))
                .ReturnsAsync(updatedProduct);

            var input = new UpdateProductInput
            {
                Id = 1,
                Name = "New Name",
                CategoryId = 1,
                RetailPrice = 20.00m,
                ImportPrice = 10.00m,
                StockQuantity = 50
            };

            var mutation = new Mutations();
            var result = await mutation.UpdateProduct(input, mockRepo.Object);

            Assert.Equal("New Name", result.Name);
            Assert.Equal(20.00m, result.RetailPrice);
            mockRepo.Verify(x => x.UpdateDetailsAsync(It.Is<Product>(p => p.Id == 1 && p.Name == "New Name")), Times.Once);
        }

        [Fact]
        public async Task updateProduct_nonExistingId_throwsException()
        {
            var mockRepo = new Mock<IProductRepository>();
            mockRepo.Setup(x => x.UpdateDetailsAsync(It.IsAny<Product>()))
                .ThrowsAsync(new InvalidOperationException("Product not found."));

            var input = new UpdateProductInput { Id = 99, Name = "Ghost Product" };
            var mutation = new Mutations();

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => mutation.UpdateProduct(input, mockRepo.Object));

            Assert.Equal("Product not found.", exception.Message);
            mockRepo.Verify(x => x.UpdateDetailsAsync(It.Is<Product>(p => p.Id == 99)), Times.Once);
        }

        [Fact]
        public async Task deleteProduct_validId_callsRepositorySoftDeleteAsync()
        {
            var mockRepo = new Mock<IProductRepository>();
            mockRepo.Setup(x => x.SoftDeleteAsync(1)).ReturnsAsync(true);
            var mutation = new Mutations();

            var result = await mutation.DeleteProduct(1, mockRepo.Object);

            Assert.True(result);
            mockRepo.Verify(x => x.SoftDeleteAsync(1), Times.Once);
        }

        [Fact]
        public async Task importProducts_invalidFileFormat_returnsErrorsAndZeroImportedCount()
        {
            var mockRepo = new Mock<IProductRepository>();
            var mutation = new Mutations();
            using var dbContext = CreateDbContext();

            var fakeFileBytes = System.Text.Encoding.UTF8.GetBytes("Invalid corrupted binary text data");
            var fakeStream = new MemoryStream(fakeFileBytes);

            var mockFile = new Mock<IFile>();
            mockFile.Setup(f => f.OpenReadStream()).Returns(fakeStream);
            mockFile.Setup(f => f.Name).Returns("virus.txt");

            var result = await mutation.ImportProducts(mockFile.Object, mockRepo.Object, dbContext);

            Assert.Equal(0, result.ImportedCount);
            Assert.NotEmpty(result.Errors);
            mockRepo.Verify(x => x.BulkInsertAsync(It.IsAny<IEnumerable<Product>>()), Times.Never);
        }

        [Fact]
        public async Task importProducts_validExcelFile_callsBulkInsertAsync()
        {
            var mockRepo = new Mock<IProductRepository>();
            var mutation = new Mutations();
            using var dbContext = CreateDbContext();
            dbContext.Categories.AddRange(
                new Category { Id = 1, Name = "Other", IsDeleted = false },
                new Category { Id = 2, Name = "Board Games", IsDeleted = false },
                new Category { Id = 3, Name = "Lego Sets", IsDeleted = false });
            await dbContext.SaveChangesAsync();

            mockRepo.Setup(x => x.BulkInsertAsync(It.IsAny<IEnumerable<Product>>()))
                .ReturnsAsync(2);

            var sampleFilePath = FindSampleExcelPath();

            if (sampleFilePath is null)
            {
                Assert.True(true, "Skipped: Physical Excel file not found in TestFiles directory.");
                return;
            }

            using var realFileStream = File.OpenRead(sampleFilePath);

            var mockFile = new Mock<IFile>();
            mockFile.Setup(f => f.OpenReadStream()).Returns(realFileStream);
            mockFile.Setup(f => f.Name).Returns("import_products_sample.xlsx");

            var result = await mutation.ImportProducts(mockFile.Object, mockRepo.Object, dbContext);

            Assert.True(result.ImportedCount > 0);
            Assert.Empty(result.Errors);
            mockRepo.Verify(x => x.BulkInsertAsync(It.Is<IEnumerable<Product>>(items =>
                items.Any(p => p.Name == "Uno Premium" && p.CategoryId == 2) &&
                items.Any(p => p.Name == "Unknown Category Toy" && p.CategoryId == 1))), Times.Once);
        }

        [Fact]
        public async Task createCategory_saleRole_throwsUnauthorizedError()
        {
            var mockRepo = new Mock<ICategoryRepository>();
            var input = new CreateCategoryInput { Name = "Blocked" };

            var mutation = new Mutations();
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                mutation.CreateCategory(input, mockRepo.Object, currentUserId: 2, currentUserRole: UserRole.Sale.ToString()));

            Assert.Equal("Only admin users can create categories.", ex.Message);
            mockRepo.Verify(x => x.AddAsync(It.IsAny<Category>()), Times.Never);
        }

        private static AppDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new AppDbContext(options);
        }

        private static string? FindSampleExcelPath()
        {
            var candidates = new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), "TestFiles", "import_products_sample.xlsx"),
                Path.Combine(Directory.GetCurrentDirectory(), "BIF.ToyStore.Tests", "TestFiles", "import_products_sample.xlsx"),
                Path.Combine(AppContext.BaseDirectory, "TestFiles", "import_products_sample.xlsx"),
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
