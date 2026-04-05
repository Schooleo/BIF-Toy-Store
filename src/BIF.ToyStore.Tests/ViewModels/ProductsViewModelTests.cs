using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.ViewModels.Pages;
using BIF.ToyStore.ViewModels.Utils;
using Moq;
using Xunit;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BIF.ToyStore.Tests.ViewModels.Pages
{
    public class ProductsViewModelTests
    {
        private readonly Mock<IProductService> _productServiceMock;
        private readonly Mock<ILocalSettingsService> _localSettingsServiceMock;
        private readonly Mock<IExcelFilePickerService> _excelFilePickerServiceMock;
        private readonly ProductsViewModel _viewModel;

        public ProductsViewModelTests()
        {
            _productServiceMock = new Mock<IProductService>();
            _localSettingsServiceMock = new Mock<ILocalSettingsService>();
            _excelFilePickerServiceMock = new Mock<IExcelFilePickerService>();

            // Setup default setting
            _localSettingsServiceMock
                .Setup(s => s.GetInt(AppPreferenceKeys.ProductsItemsPerPage, 20))
                .Returns(20);

            _viewModel = new ProductsViewModel(
                _productServiceMock.Object,
                _localSettingsServiceMock.Object,
                _excelFilePickerServiceMock.Object);
        }

        [Fact]
        public async Task LoadProductsAsync_ValidResponse_UpdatesCollectionAndPagingInfo()
        {
            // Arrange
            var fakeResponse = new ProductListResult
            {
                TotalCount = 45,
                HasNextPage = true,
                HasPreviousPage = false,
                StartCursor = "cursor1",
                EndCursor = "cursor20",
                Items = new List<Product>
                {
                    new Product { Id = 1, Name = "Test Product 1" },
                    new Product { Id = 2, Name = "Test Product 2" }
                }
            };

            _productServiceMock
                .Setup(x => x.GetProductsAsync(It.IsAny<ProductListQuery>()))
                .ReturnsAsync(fakeResponse);

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

            _productServiceMock
                .Setup(x => x.GetProductsAsync(It.IsAny<ProductListQuery>()))
                .ReturnsAsync(new ProductListResult { Items = new List<Product>() });

            // Act
            await _viewModel.ApplyFilterCommand.ExecuteAsync(null);

            // Assert
            Assert.Null(_viewModel.AfterCursor);
            _productServiceMock.Verify(x => x.GetProductsAsync(
                It.Is<ProductListQuery>(q => q.SearchText == "Lego")), Times.Once);
        }

        [Fact]
        public async Task ClearFilterAsync_WhenCalled_ResetsFiltersAndReloads()
        {
            // Arrange
            _viewModel.SearchText = "Lego";
            _viewModel.MinPrice = 100;
            _viewModel.SelectedCategory = new Category { Id = 1, Name = "Other" };

            _productServiceMock
                .Setup(x => x.GetProductsAsync(It.IsAny<ProductListQuery>()))
                .ReturnsAsync(new ProductListResult { Items = new List<Product>() });

            // Act
            await _viewModel.ClearFilterCommand.ExecuteAsync(null);

            // Assert
            Assert.Empty(_viewModel.SearchText);
            Assert.Equal(0, _viewModel.MinPrice);
            Assert.Null(_viewModel.SelectedCategory);
            _productServiceMock.Verify(x => x.GetProductsAsync(It.IsAny<ProductListQuery>()), Times.Once);
        }
    }
}
