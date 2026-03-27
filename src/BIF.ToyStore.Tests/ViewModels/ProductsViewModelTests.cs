using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.ViewModels.Pages;
using BIF.ToyStore.ViewModels.Utils;
using Moq;
using Xunit;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;

namespace BIF.ToyStore.Tests.ViewModels.Pages
{
    public class ProductsViewModelTests
    {
        private readonly Mock<IGraphQLClient> _graphQLClientMock;
        private readonly Mock<ILocalSettingsService> _localSettingsServiceMock;
        private readonly Mock<IExcelFilePickerService> _excelFilePickerServiceMock;
        private readonly ProductsViewModel _viewModel;

        public ProductsViewModelTests()
        {
            _graphQLClientMock = new Mock<IGraphQLClient>();
            _localSettingsServiceMock = new Mock<ILocalSettingsService>();
            _excelFilePickerServiceMock = new Mock<IExcelFilePickerService>();

            // Setup default setting
            _localSettingsServiceMock
                .Setup(s => s.GetInt(AppPreferenceKeys.ProductsItemsPerPage, 20))
                .Returns(20);

            _viewModel = new ProductsViewModel(
                _graphQLClientMock.Object,
                _localSettingsServiceMock.Object,
                _excelFilePickerServiceMock.Object);
        }

        [Fact]
        public async Task LoadProductsAsync_ValidResponse_UpdatesCollectionAndPagingInfo()
        {
            // Arrange
            var fakeResponse = new ProductsViewModel.ProductConnection
            {
                TotalCount = 45,
                PageInfo = new ProductsViewModel.PageInfo
                {
                    HasNextPage = true,
                    HasPreviousPage = false,
                    StartCursor = "cursor1",
                    EndCursor = "cursor20"
                },
                Nodes = new List<Product>
                {
                    new Product { Id = 1, Name = "Test Product 1" },
                    new Product { Id = 2, Name = "Test Product 2" }
                }
            };

            _graphQLClientMock.Setup(x => x.ExecuteAsync<ProductsViewModel.ProductConnection>(
                It.IsAny<string>(),
                It.IsAny<object>(),
                "products"
            )).ReturnsAsync(fakeResponse);

            // Act
            await _viewModel.LoadProductsAsync();

            // Assert
            Assert.Equal(2, _viewModel.Products.Count);
            Assert.Equal("Test Product 1", _viewModel.Products[0].Name);
            Assert.Equal(45, _viewModel.TotalCount);
            Assert.True(_viewModel.HasNextPage);
            Assert.False(_viewModel.HasPreviousPage);
            Assert.Equal("cursor20", _viewModel.AfterCursor);
        }

        [Fact]
        public async Task ApplyFilterAsync_WhenCalled_ResetsCursorsAndReloads()
        {
            // Arrange
            _viewModel.AfterCursor = "some_cursor";
            _viewModel.SearchText = "Lego";

            _graphQLClientMock.Setup(x => x.ExecuteAsync<ProductsViewModel.ProductConnection>(
                It.IsAny<string>(),
                It.Is<object>(variables => JsonSerializer.Serialize(variables).Contains("\"contains\":\"Lego\"")),
                "products"
            )).ReturnsAsync(new ProductsViewModel.ProductConnection { Nodes = new List<Product>() });

            // Act
            await _viewModel.ApplyFilterCommand.ExecuteAsync(null);

            // Assert
            Assert.Null(_viewModel.AfterCursor);
            _graphQLClientMock.Verify(x => x.ExecuteAsync<ProductsViewModel.ProductConnection>(
                It.IsAny<string>(),
                It.Is<object>(variables => JsonSerializer.Serialize(variables).Contains("\"contains\":\"Lego\"")),
                "products"
            ), Times.Once);
        }

        [Fact]
        public async Task ClearFilterAsync_WhenCalled_ResetsFiltersAndReloads()
        {
            // Arrange
            _viewModel.SearchText = "Lego";
            _viewModel.MinPrice = 100;
            _viewModel.SelectedCategory = new Category { Id = 1, Name = "Other" };

            _graphQLClientMock.Setup(x => x.ExecuteAsync<ProductsViewModel.ProductConnection>(
                It.IsAny<string>(),
                It.IsAny<object>(),
                "products"
            )).ReturnsAsync(new ProductsViewModel.ProductConnection { Nodes = new List<Product>() });

            // Act
            await _viewModel.ClearFilterCommand.ExecuteAsync(null);

            // Assert
            Assert.Empty(_viewModel.SearchText);
            Assert.Equal(0, _viewModel.MinPrice);
            Assert.Null(_viewModel.SelectedCategory);
            _graphQLClientMock.Verify(x => x.ExecuteAsync<ProductsViewModel.ProductConnection>(
                It.IsAny<string>(),
                It.IsAny<object>(),
                "products"
            ), Times.Once);
        }
    }
}
