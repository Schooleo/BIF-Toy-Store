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
        private readonly Mock<IProductImageUploadService> _productImageUploadServiceMock;
        private readonly Mock<ILocalSettingsService> _localSettingsServiceMock;
        private readonly Mock<IExcelFilePickerService> _excelFilePickerServiceMock;
        private readonly Mock<IGraphQLClient> _graphQLClientMock;
        private readonly ProductsViewModel _viewModel;

        public ProductsViewModelTests()
        {
            _productServiceMock = new Mock<IProductService>();
            _productImageUploadServiceMock = new Mock<IProductImageUploadService>();
            _localSettingsServiceMock = new Mock<ILocalSettingsService>();
            _excelFilePickerServiceMock = new Mock<IExcelFilePickerService>();
            _graphQLClientMock = new Mock<IGraphQLClient>();

            // Setup default setting
            _localSettingsServiceMock
                .Setup(s => s.GetInt(AppPreferenceKeys.ProductsItemsPerPage, 20))
                .Returns(20);

            _viewModel = new ProductsViewModel(
                _graphQLClientMock.Object,
                _productServiceMock.Object,
                _productImageUploadServiceMock.Object,
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

            _graphQLClientMock
                .Setup(x => x.ExecuteAsync<ProductAppConfigNode>(
                    It.Is<string>(q => q.Contains("GetProductsAppConfig")),
                    It.IsAny<object?>(),
                    "appConfig"))
                .ReturnsAsync(new ProductAppConfigNode { CurrencySymbol = "USD" });

            // Act
            await _viewModel.LoadProductsAsync();

            // Assert
            Assert.Equal(2, _viewModel.Products.Count);
            Assert.Equal("Test Product 1", _viewModel.Products[0].Name);
            Assert.Equal(45, _viewModel.TotalCount);
            Assert.True(_viewModel.HasNextPage);
            Assert.False(_viewModel.HasPreviousPage);
            Assert.Equal("cursor20", _viewModel.AfterCursor);
            Assert.Equal("USD", _viewModel.Products[0].CurrencySymbol);
        }

        [Fact]
        public async Task LoadProductsAsync_UsesGlobalCurrencyForRetailDisplay()
        {
            var fakeResponse = new ProductListResult
            {
                TotalCount = 1,
                Items = new List<Product>
                {
                    new Product { Id = 1, Name = "Test Product 1", RetailPrice = 12.5m }
                }
            };

            _graphQLClientMock
                .Setup(x => x.ExecuteAsync<ProductAppConfigNode>(
                    It.Is<string>(q => q.Contains("GetProductsAppConfig")),
                    It.IsAny<object?>(),
                    "appConfig"))
                .ReturnsAsync(new ProductAppConfigNode { CurrencySymbol = "USD" });

            _productServiceMock
                .Setup(x => x.GetProductsAsync(It.IsAny<ProductListQuery>()))
                .ReturnsAsync(fakeResponse);

            await _viewModel.LoadProductsAsync();

            Assert.Single(_viewModel.Products);
            Assert.Equal("USD 12.50", _viewModel.Products[0].RetailPriceDisplay);
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
            Assert.NotNull(_viewModel.SelectedCategory);
            Assert.Equal(0, _viewModel.SelectedCategory!.Id);
            Assert.Equal("All Categories", _viewModel.SelectedCategory.Name);
            _productServiceMock.Verify(x => x.GetProductsAsync(It.IsAny<ProductListQuery>()), Times.Once);
        }

        [Fact]
        public void OpenEditPanel_CopiesExistingImageUrl()
        {
            var product = new Product
            {
                Id = 9,
                Name = "RC Car",
                CategoryId = 2,
                ImageUrl = "https://example.com/rc-car.png"
            };

            _viewModel.OpenEditPanel(product);

            Assert.NotNull(_viewModel.EditingProduct);
            Assert.Equal("https://example.com/rc-car.png", _viewModel.EditingProduct!.ImageUrl);
        }

        [Fact]
        public async Task SaveProductEditAsync_PreservesImageUrlInUpdatePayload()
        {
            var updatedImageUrls = new List<string?>();

            _productServiceMock
                .Setup(x => x.UpdateProductAsync(It.IsAny<Product>()))
                .Callback<Product>(input => updatedImageUrls.Add(input.ImageUrl))
                .ReturnsAsync((Product input) => input);

            _productImageUploadServiceMock
                .Setup(x => x.UploadProductImageAsync(12, "C:\\images\\spaceship.png", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProductImageUploadResult
                {
                    ImageUrl = "https://example.com/spaceship.png",
                    PublicId = "bif-toy-store/products/product-12-upload"
                });

            _productServiceMock
                .Setup(x => x.GetProductsAsync(It.IsAny<ProductListQuery>()))
                .ReturnsAsync(new ProductListResult { Items = new List<Product>() });

            _viewModel.OpenEditPanel(new Product
            {
                Id = 12,
                Name = "Spaceship",
                CategoryId = 4,
                ImportPrice = 20m,
                RetailPrice = 40m,
                StockQuantity = 7,
                ImageUrl = "https://example.com/old-spaceship.png"
            });

            _viewModel.EditingProduct!.ImageUrl = "C:\\images\\spaceship.png";

            await _viewModel.SaveProductEditCommand.ExecuteAsync(null);

            Assert.Equal(2, updatedImageUrls.Count);
            Assert.Equal("https://example.com/old-spaceship.png", updatedImageUrls[0]);
            Assert.Equal("https://example.com/spaceship.png", updatedImageUrls[1]);
        }

        [Fact]
        public async Task SaveProductEditAsync_WhenManagedUrlUpdateFails_DeletesNewCloudinaryAssetForLegacyImage()
        {
            _productServiceMock
                .SetupSequence(x => x.UpdateProductAsync(It.IsAny<Product>()))
                .ReturnsAsync(new Product
                {
                    Id = 22,
                    Name = "Legacy Product",
                    CategoryId = 2,
                    ImportPrice = 5m,
                    RetailPrice = 10m,
                    StockQuantity = 2,
                    ImageUrl = "https://legacy.example.com/legacy.png"
                })
                .ThrowsAsync(new InvalidOperationException("db write failed"))
                .ReturnsAsync(new Product
                {
                    Id = 22,
                    Name = "Legacy Product",
                    CategoryId = 2,
                    ImportPrice = 5m,
                    RetailPrice = 10m,
                    StockQuantity = 2,
                    ImageUrl = "https://legacy.example.com/legacy.png"
                });

            _productImageUploadServiceMock
                .Setup(x => x.UploadProductImageAsync(22, "C:\\images\\legacy.png", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProductImageUploadResult
                {
                    ImageUrl = "https://res.cloudinary.com/demo/image/upload/bif-toy-store/products/product-22-upload",
                    PublicId = "bif-toy-store/products/product-22-upload"
                });

            _productServiceMock
                .Setup(x => x.GetProductsAsync(It.IsAny<ProductListQuery>()))
                .ReturnsAsync(new ProductListResult { Items = new List<Product>() });

            _viewModel.OpenEditPanel(new Product
            {
                Id = 22,
                Name = "Legacy Product",
                CategoryId = 2,
                ImportPrice = 5m,
                RetailPrice = 10m,
                StockQuantity = 2,
                ImageUrl = "https://legacy.example.com/legacy.png"
            });

            _viewModel.EditingProduct!.ImageUrl = "C:\\images\\legacy.png";

            await _viewModel.SaveProductEditCommand.ExecuteAsync(null);

            _productImageUploadServiceMock.Verify(
                x => x.DeleteProductImageAsync("bif-toy-store/products/product-22-upload", It.IsAny<CancellationToken>()),
                Times.Once);
            Assert.Contains("db write failed", _viewModel.EditErrorMessage);
        }

        [Fact]
        public async Task SaveProductEditAsync_WhenExistingManagedImageIsReplaced_DeletesOldPublicId()
        {
            _productServiceMock
                .SetupSequence(x => x.UpdateProductAsync(It.IsAny<Product>()))
                .ReturnsAsync(new Product
                {
                    Id = 33,
                    Name = "Managed Product",
                    CategoryId = 2,
                    ImportPrice = 6m,
                    RetailPrice = 12m,
                    StockQuantity = 3,
                    ImageUrl = "https://res.cloudinary.com/demo/image/upload/v1712345678/bif-toy-store/products/product-33-old.png"
                })
                .ReturnsAsync(new Product
                {
                    Id = 33,
                    Name = "Managed Product",
                    CategoryId = 2,
                    ImportPrice = 6m,
                    RetailPrice = 12m,
                    StockQuantity = 3,
                    ImageUrl = "https://res.cloudinary.com/demo/image/upload/v1712349999/bif-toy-store/products/product-33-upload.png"
                });

            _productImageUploadServiceMock
                .Setup(x => x.UploadProductImageAsync(33, "C:\\images\\managed.png", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProductImageUploadResult
                {
                    ImageUrl = "https://res.cloudinary.com/demo/image/upload/v1712349999/bif-toy-store/products/product-33-upload.png",
                    PublicId = "bif-toy-store/products/product-33-upload"
                });

            _productServiceMock
                .Setup(x => x.GetProductsAsync(It.IsAny<ProductListQuery>()))
                .ReturnsAsync(new ProductListResult { Items = new List<Product>() });

            _viewModel.OpenEditPanel(new Product
            {
                Id = 33,
                Name = "Managed Product",
                CategoryId = 2,
                ImportPrice = 6m,
                RetailPrice = 12m,
                StockQuantity = 3,
                ImageUrl = "https://res.cloudinary.com/demo/image/upload/v1712345678/bif-toy-store/products/product-33-old.png"
            });

            _viewModel.EditingProduct!.ImageUrl = "C:\\images\\managed.png";

            await _viewModel.SaveProductEditCommand.ExecuteAsync(null);

            _productImageUploadServiceMock.Verify(
                x => x.DeleteProductImageAsync("bif-toy-store/products/product-33-old", It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task CreateProductAsync_WhenRefreshFails_DoesNotRollbackCreatedProduct()
        {
            _productServiceMock
                .Setup(x => x.CreateProductAsync(It.IsAny<Product>()))
                .ReturnsAsync(new Product
                {
                    Id = 44,
                    Name = "Create Test",
                    CategoryId = 3,
                    ImportPrice = 8m,
                    RetailPrice = 16m,
                    StockQuantity = 2
                });

            _productImageUploadServiceMock
                .Setup(x => x.UploadProductImageAsync(44, "C:\\images\\create.png", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProductImageUploadResult
                {
                    ImageUrl = "https://res.cloudinary.com/demo/image/upload/v1/bif-toy-store/products/product-44-upload.png",
                    PublicId = "bif-toy-store/products/product-44-upload"
                });

            _productServiceMock
                .Setup(x => x.UpdateProductAsync(It.IsAny<Product>()))
                .ReturnsAsync((Product input) => input);

            _productServiceMock
                .Setup(x => x.GetProductsAsync(It.IsAny<ProductListQuery>()))
                .ThrowsAsync(new InvalidOperationException("refresh failed"));

            await Assert.ThrowsAsync<InvalidOperationException>(() => _viewModel.CreateProductAsync(new Product
            {
                Name = "Create Test",
                CategoryId = 3,
                ImportPrice = 8m,
                RetailPrice = 16m,
                StockQuantity = 2,
                ImageUrl = "C:\\images\\create.png"
            }));

            _productServiceMock.Verify(x => x.DeleteProductAsync(It.IsAny<int>()), Times.Never);
            _productImageUploadServiceMock.Verify(
                x => x.DeleteProductImageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }
    }
}
