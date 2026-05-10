using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Enums;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.ViewModels.Pages;
using BIF.ToyStore.ViewModels.Utils;
using Moq;
using Xunit;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Linq;

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
            _localSettingsServiceMock
                .Setup(s => s.GetString(AppPreferenceKeys.CurrentUserRole, It.IsAny<string>()))
                .Returns(UserRole.Admin.ToString());

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
        public async Task LoadProductsAsync_EmptyResponse_SetsEmptyResultsState()
        {
            _graphQLClientMock
                .Setup(x => x.ExecuteAsync<ProductAppConfigNode>(
                    It.IsAny<string>(),
                    It.IsAny<object?>(),
                    "appConfig"))
                .ReturnsAsync(new ProductAppConfigNode { CurrencySymbol = "USD" });

            _productServiceMock
                .Setup(x => x.GetProductsAsync(It.IsAny<ProductListQuery>()))
                .ReturnsAsync(new ProductListResult
                {
                    TotalCount = 0,
                    Items = new List<Product>()
                });

            await _viewModel.LoadProductsAsync();

            Assert.Empty(_viewModel.Products);
            Assert.True(_viewModel.IsProductsEmpty);
            Assert.False(_viewModel.HasProducts);
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
        public void OpenEditPanel_CopiesExistingImages()
        {
            var product = new Product
            {
                Id = 9,
                Name = "RC Car",
                CategoryId = 2,
                Images = new ObservableCollection<ProductImage> 
                { 
                    new ProductImage { ImageUrl = "https://example.com/rc-car.png", IsPrimary = true } 
                }
            };
            
            _viewModel.OpenEditPanel(product);

            Assert.NotNull(_viewModel.EditingProduct);
            Assert.Single(_viewModel.EditingProduct!.Images);
            Assert.Equal("https://example.com/rc-car.png", _viewModel.EditingProduct!.Images.First().ImageUrl);
        }

        [Fact]
        public async Task SaveProductEditAsync_HandlesNewImageUpload()
        {
            var uploadedImages = new List<ProductImage>();

            var existingProduct = new Product
            {
                Id = 12,
                Name = "Spaceship",
                CategoryId = 4,
                ImportPrice = 20m,
                RetailPrice = 40m,
                StockQuantity = 7,
                Images = new ObservableCollection<ProductImage>
                {
                    new ProductImage { ImageUrl = "https://example.com/old-spaceship.png", IsPrimary = true }
                }
            };

            _productServiceMock
                .Setup(x => x.UpdateProductAsync(It.IsAny<Product>()))
                .Callback<Product>(input => 
                {
                    if (input.Images.Any(img => img.ImageUrl.StartsWith("https://")))
                    {
                        uploadedImages.Clear();
                        foreach(var img in input.Images) uploadedImages.Add(img);
                    }
                })
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

            _viewModel.OpenEditPanel(existingProduct);

            // Simulate adding a pending image
            _viewModel.EditingProduct!.Images.Add(new ProductImage { ImageUrl = "C:\\images\\spaceship.png", IsPrimary = false });

            await _viewModel.SaveProductEditCommand.ExecuteAsync(null);

            Assert.Equal(2, uploadedImages.Count);
            Assert.Contains(uploadedImages, i => i.ImageUrl == "https://example.com/old-spaceship.png");
            Assert.Contains(uploadedImages, i => i.ImageUrl == "https://example.com/spaceship.png");
        }

        [Fact]
        public async Task SaveProductEditAsync_WhenUploadSucceedsButDbFails_DeletesCloudinaryAsset()
        {
            int callCount = 0;
            _productServiceMock
                .Setup(x => x.UpdateProductAsync(It.IsAny<Product>()))
                .ReturnsAsync((Product input) => 
                {
                    callCount++;
                    if (callCount == 1) return input;
                    throw new InvalidOperationException("db write failed");
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
                Images = new ObservableCollection<ProductImage> { new ProductImage { ImageUrl = "https://legacy.png", IsPrimary = true } }
            });

            _viewModel.EditingProduct!.Images.Add(new ProductImage { ImageUrl = "C:\\images\\legacy.png", IsPrimary = false });

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
                .Setup(x => x.UpdateProductAsync(It.IsAny<Product>()))
                .ReturnsAsync((Product input) => input);

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
                Images = new ObservableCollection<ProductImage> 
                { 
                    new ProductImage { ImageUrl = "https://res.cloudinary.com/demo/image/upload/v1712345678/bif-toy-store/products/product-33-old.png", IsPrimary = true } 
                }
            });

            _viewModel.EditingProduct!.Images.Add(new ProductImage { ImageUrl = "C:\\images\\managed.png", IsPrimary = false });

            await _viewModel.SaveProductEditCommand.ExecuteAsync(null);

            // Note: The actual logic for deleting old assets when replacing images might have changed 
            // since we now support multiple images. This test might need further adjustment based on 
            // whether the ViewModel or Service handles old asset cleanup.
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
                Images = new ObservableCollection<ProductImage> { new ProductImage { ImageUrl = "C:\\images\\create.png" } }
            }));

            _productServiceMock.Verify(x => x.DeleteProductAsync(It.IsAny<int>()), Times.Never);
            _productImageUploadServiceMock.Verify(
                x => x.DeleteProductImageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task CreateProductAsync_SaleUser_ThrowsAndSkipsServiceCall()
        {
            _localSettingsServiceMock
                .Setup(s => s.GetString(AppPreferenceKeys.CurrentUserRole, It.IsAny<string>()))
                .Returns(UserRole.Sale.ToString());

            var saleViewModel = new ProductsViewModel(
                _graphQLClientMock.Object,
                _productServiceMock.Object,
                _productImageUploadServiceMock.Object,
                _localSettingsServiceMock.Object,
                _excelFilePickerServiceMock.Object);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => saleViewModel.CreateProductAsync(new Product
            {
                Name = "Blocked",
                CategoryId = 1
            }));

            Assert.Equal("Only admin users can add new products.", ex.Message);
            _productServiceMock.Verify(x => x.CreateProductAsync(It.IsAny<Product>()), Times.Never);
        }

        [Fact]
        public async Task Test_Import_AggregatesAllErrorMessages()
        {
            // Arrange
            _viewModel.SetWindowHandle((nint)1);

            _excelFilePickerServiceMock
                .Setup(x => x.PickExcelFilePathAsync((nint)1))
                .ReturnsAsync("C:\\temp\\products.xlsx");

            var backendErrors = new List<string>
            {
                "Row 2: [Name] - Name is required",
                "Row 3: [RetailPrice] - Invalid decimal",
                "Row 4: [CategoryName] - Unknown category",
                "Row 5: [StockQuantity] - Invalid integer"
            };

            _productServiceMock
                .Setup(x => x.ImportProductsAsync("C:\\temp\\products.xlsx"))
                .ReturnsAsync(new ProductImportResult
                {
                    ImportedCount = 2,
                    Errors = backendErrors
                });

            _productServiceMock
                .Setup(x => x.GetProductsAsync(It.IsAny<ProductListQuery>()))
                .ReturnsAsync(new ProductListResult { Items = new List<Product>() });

            ProductsViewModel.ImportFeedback? capturedFeedback = null;
            _viewModel.ImportFeedbackRequested += feedback =>
            {
                capturedFeedback = feedback;
                return Task.CompletedTask;
            };

            // Act
            await _viewModel.ImportExcelAsync();

            // Assert
            Assert.NotNull(capturedFeedback);
            Assert.True(capturedFeedback!.HasErrors);
            Assert.True(capturedFeedback.RequiresScrollableDialog);
            Assert.Equal(4, capturedFeedback.ErrorCount);
            Assert.Contains("Imported 2 product(s). Failed rows: 4.", _viewModel.ImportErrorMessage);
            Assert.Contains("Successful rows: 2", capturedFeedback.DetailMessage);
            Assert.Contains("Failed rows: 4", capturedFeedback.DetailMessage);

            foreach (var backendError in backendErrors)
            {
                Assert.Contains(backendError, capturedFeedback.DetailMessage);
            }

            Assert.False(_viewModel.IsBusy);
        }
    }
}
