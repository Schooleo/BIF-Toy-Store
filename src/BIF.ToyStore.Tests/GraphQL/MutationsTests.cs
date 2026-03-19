using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.Infrastructure.GraphQL;
using Moq;
using System;
using System.Collections.Generic;
using System.Text;

namespace BIF.ToyStore.Tests.GraphQL
{
    public class MutationsTests
    {
        [Fact]
        public async Task importProducts_invalidBase64String_returnsErrorsAndZeroImportedCount()
        {
            var mockRepo = new Mock<IProductRepository>();
            var mutation = new Mutations();
            var invalidBase64 = "This is not a valid Base64 string.";
            var result = await mutation.ImportProducts(invalidBase64, mockRepo.Object);
            Assert.Equal(0, result.ImportedCount);
            Assert.NotEmpty(result.Errors);
            mockRepo.Verify(x => x.BulkInsertAsync(It.IsAny<IEnumerable<Product>>()), Times.Never);
        }

        [Fact]
        public async Task createProduct_validProductInput_callsRepositoryAddAsyncAndReturnsProduct()
        {
            var mockRepo = new Mock<IProductRepository>();
            var input = new CreateProductInput
            {
                Name = "Gấu Bông",
                CategoryId = 1,
                RetailPrice = 50,
                ImportPrice = 20,
                StockQuantity = 10
            };
            mockRepo.Setup(x => x.AddAsync(It.IsAny<Product>()))
                    .ReturnsAsync((Product p) => { p.Id = 99; return p; });
            var mutation = new Mutations();
            var result = await mutation.CreateProduct(input, mockRepo.Object);
            Assert.NotNull(result);
            Assert.Equal(99, result.Id);
            mockRepo.Verify(x => x.AddAsync(It.Is<Product>(p => p.Name == "Gấu Bông")), Times.Once);
        }
    }
}
