using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.Infrastructure.GraphQL;
using HotChocolate.Types;
using Moq;
using Xunit;

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
        public async Task updateProduct_existingId_callsRepositoryUpdateAsync()
        {
            var mockRepo = new Mock<IProductRepository>();
            var existingProduct = new Product { Id = 1, Name = "Old Name", RetailPrice = 10.00m };

            mockRepo.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(existingProduct);

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
            mockRepo.Verify(x => x.UpdateAsync(It.Is<Product>(p => p.Id == 1 && p.Name == "New Name")), Times.Once);
        }

        [Fact]
        public async Task updateProduct_nonExistingId_throwsException()
        {
            var mockRepo = new Mock<IProductRepository>();
            mockRepo.Setup(x => x.GetByIdAsync(99)).ReturnsAsync((Product)null);

            var input = new UpdateProductInput { Id = 99, Name = "Ghost Product" };
            var mutation = new Mutations();

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => mutation.UpdateProduct(input, mockRepo.Object));

            Assert.Equal("Product not found.", exception.Message);
            mockRepo.Verify(x => x.UpdateAsync(It.IsAny<Product>()), Times.Never);
        }

        [Fact]
        public async Task deleteProduct_validId_callsRepositoryDeleteAsync()
        {
            var mockRepo = new Mock<IProductRepository>();
            var mutation = new Mutations();

            var result = await mutation.DeleteProduct(1, mockRepo.Object);

            Assert.True(result);
            mockRepo.Verify(x => x.DeleteAsync(1), Times.Once);
        }

        [Fact]
        public async Task importProducts_invalidFileFormat_returnsErrorsAndZeroImportedCount()
        {
            var mockRepo = new Mock<IProductRepository>();
            var mutation = new Mutations();

            var fakeFileBytes = System.Text.Encoding.UTF8.GetBytes("Invalid corrupted binary text data");
            var fakeStream = new MemoryStream(fakeFileBytes);

            var mockFile = new Mock<IFile>();
            mockFile.Setup(f => f.OpenReadStream()).Returns(fakeStream);
            mockFile.Setup(f => f.Name).Returns("virus.txt");

            var result = await mutation.ImportProducts(mockFile.Object, mockRepo.Object);

            Assert.Equal(0, result.ImportedCount);
            Assert.NotEmpty(result.Errors);
            mockRepo.Verify(x => x.BulkInsertAsync(It.IsAny<IEnumerable<Product>>()), Times.Never);
        }

        [Fact]
        public async Task importProducts_validExcelFile_callsBulkInsertAsync()
        {
            var mockRepo = new Mock<IProductRepository>();
            var mutation = new Mutations();

            var sampleFilePath = Path.Combine("TestFiles", "import_products_sample.xlsx");

            if (!File.Exists(sampleFilePath))
            {
                Assert.True(true, "Skipped: Physical Excel file not found in TestFiles directory.");
                return;
            }

            using var realFileStream = File.OpenRead(sampleFilePath);

            var mockFile = new Mock<IFile>();
            mockFile.Setup(f => f.OpenReadStream()).Returns(realFileStream);
            mockFile.Setup(f => f.Name).Returns("import_products_sample.xlsx");

            var result = await mutation.ImportProducts(mockFile.Object, mockRepo.Object);

            Assert.True(result.ImportedCount > 0);
            Assert.Empty(result.Errors);
            mockRepo.Verify(x => x.BulkInsertAsync(It.IsAny<IEnumerable<Product>>()), Times.Once);
        }
    }
}
