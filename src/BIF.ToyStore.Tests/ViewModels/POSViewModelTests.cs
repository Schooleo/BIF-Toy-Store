using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.ViewModels.Messages;
using BIF.ToyStore.ViewModels.Pages;
using BIF.ToyStore.ViewModels.Utils;
using CommunityToolkit.Mvvm.Messaging;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace BIF.ToyStore.Tests.ViewModels.Pages
{
    public class POSViewModelTests
    {
        private readonly Mock<IGraphQLClient> _graphQLClientMock;
        private readonly Mock<ILocalSettingsService> _localSettingsServiceMock;
        private readonly Mock<IMessenger> _messengerMock;
        private readonly POSViewModel _viewModel;

        public POSViewModelTests()
        {
            _graphQLClientMock = new Mock<IGraphQLClient>();
            _localSettingsServiceMock = new Mock<ILocalSettingsService>();
            _messengerMock = new Mock<IMessenger>();

            _localSettingsServiceMock
                .Setup(x => x.GetInt(AppPreferenceKeys.CurrentUserId, 0))
                .Returns(7);

            _viewModel = new POSViewModel(_graphQLClientMock.Object, _localSettingsServiceMock.Object, _messengerMock.Object);
        }

        [Fact]
        public async Task LoadAsync_FetchesCategoriesAndProducts()
        {
            // Arrange
            var categoriesResponse = new CategoryConnection
            {
                Nodes = new List<Category>
                {
                    new Category { Id = 1, Name = "Action Figures" },
                    new Category { Id = 2, Name = "Dolls" }
                }
            };

            var productsResponse = new ProductConnectionSimple
            {
                Nodes = new List<Product>
                {
                    new Product { Id = 10, Name = "Batman", RetailPrice = 19.99m, StockQuantity = 50, Category = new Category { Name = "Action Figures" } },
                    new Product { Id = 11, Name = "Barbie", RetailPrice = 29.99m, StockQuantity = 3, Category = new Category { Name = "Dolls" } }
                }
            };

            // Setup mock responses based on dataKey since LoadAsync fires two queries
            _graphQLClientMock.Setup(x => x.ExecuteAsync<CategoryConnection>(
                It.Is<string>(q => q.Contains("GetCategories")),
                It.IsAny<object>(),
                "categories")).ReturnsAsync(categoriesResponse);

            _graphQLClientMock.Setup(x => x.ExecuteAsync<ProductConnectionSimple>(
                It.Is<string>(q => q.Contains("GetProductsForPOS")),
                It.IsAny<object>(),
                "products")).ReturnsAsync(productsResponse);

            // Act
            await _viewModel.LoadAsync();

            // Assert
            Assert.Equal(3, _viewModel.Categories.Count); // "All" + 2 Categories
            Assert.Contains("Action Figures", _viewModel.Categories);
            Assert.Contains("Dolls", _viewModel.Categories);
            
            Assert.Equal(2, _viewModel.FilteredProducts.Count);
            
            // Check computed badge logic for low stock
            var barbie = _viewModel.FilteredProducts.First(p => p.Name == "Barbie");
            Assert.True(barbie.IsLowStock); // Stock is 3 (<= 5)
            Assert.False(barbie.IsOutOfStock);
            Assert.Equal("LOW STOCK", barbie.LowStockBadgeLabel);

            var batman = _viewModel.FilteredProducts.First(p => p.Name == "Batman");
            Assert.False(batman.IsLowStock); // Stock is 50
            Assert.False(batman.IsOutOfStock);
        }

        [Fact]
        public void AddToCart_WhenNewItem_AddsToCartAndCalculatesTotals()
        {
            // Arrange
            var productMock = new ProductItemViewModel(new Product { Id = 1, Name = "Toy", RetailPrice = 100m, StockQuantity = 10 });

            // Act
            _viewModel.AddToCartCommand.Execute(productMock);

            // Assert
            Assert.Single(_viewModel.CartItems);
            Assert.Equal(1, _viewModel.CartItems[0].Quantity);
            Assert.Equal(100m, _viewModel.CartItems[0].LineTotal);
            
            // Check totals (8% tax)
            Assert.Equal(100m, _viewModel.Subtotal);
            Assert.Equal(8m, _viewModel.Tax);
            Assert.Equal(108m, _viewModel.TotalDue);
        }

        [Fact]
        public void AddToCart_WhenExistingItem_IncreasesQuantity()
        {
            // Arrange
            var productMock = new ProductItemViewModel(new Product { Id = 1, Name = "Toy", RetailPrice = 50m, StockQuantity = 10 });

            // Act
            _viewModel.AddToCartCommand.Execute(productMock); // qty 1
            _viewModel.AddToCartCommand.Execute(productMock); // qty 2

            // Assert
            Assert.Single(_viewModel.CartItems);
            Assert.Equal(2, _viewModel.CartItems[0].Quantity);
            Assert.Equal(100m, _viewModel.CartItems[0].LineTotal);
            Assert.Equal(100m, _viewModel.Subtotal);
        }

        [Fact]
        public void DecreaseQuantity_WhenQuantityIsOne_RemovesItemFromCart()
        {
            // Arrange
            var productMock = new ProductItemViewModel(new Product { Id = 1, Name = "Toy", RetailPrice = 50m, StockQuantity = 10 });
            _viewModel.AddToCartCommand.Execute(productMock);
            var cartItem = _viewModel.CartItems.First();

            // Act
            _viewModel.DecreaseQuantityCommand.Execute(cartItem);

            // Assert
            Assert.Empty(_viewModel.CartItems);
            Assert.Equal(0m, _viewModel.Subtotal);
        }

        [Fact]
        public async Task ProcessPayment_WhenCartHasItems_CallsMutationAndClearsCart()
        {
            // Arrange
            var productMock = new ProductItemViewModel(new Product { Id = 1, Name = "Toy", RetailPrice = 100m, StockQuantity = 10 });
            _viewModel.AddToCartCommand.Execute(productMock);

            // Mock successful order creation
            var orderResult = new OrderResult
            {
                Id = 999,
                TotalAmount = 108m,
                Status = "Completed"
            };

            _graphQLClientMock.Setup(x => x.ExecuteAsync<OrderResult>(
                It.Is<string>(q => q.Contains("CreateOrder")),
                It.IsAny<object>(),
                "createOrder")).ReturnsAsync(orderResult);

            // Mock refetching products after successful payment
            _graphQLClientMock.Setup(x => x.ExecuteAsync<ProductConnectionSimple>(
                It.Is<string>(q => q.Contains("GetProductsForPOS")),
                It.IsAny<object>(),
                "products")).ReturnsAsync(new ProductConnectionSimple { Nodes = new List<Product>() });

            // Act
            await _viewModel.ProcessPaymentCommand.ExecuteAsync(null);

            // Assert
            Assert.Empty(_viewModel.CartItems);
            Assert.Equal(0m, _viewModel.Subtotal);
            Assert.Equal(0m, _viewModel.Tax);
            Assert.Equal(0m, _viewModel.TotalDue);
            Assert.True(_viewModel.HasSuccess);
            Assert.Contains("Order #999", _viewModel.SuccessMessage);
            Assert.False(_viewModel.HasError);
            
            // Verify mutation and refresh queries were called
            _graphQLClientMock.Verify(x => x.ExecuteAsync<OrderResult>(
                It.IsAny<string>(), It.IsAny<object>(), "createOrder"), Times.Once);
        }

        [Fact]
        public async Task ProcessPayment_WhenCartIsEmpty_ReturnsError()
        {
            // Arrange (cart is empty)

            // Act
            await _viewModel.ProcessPaymentCommand.ExecuteAsync(null);

            // Assert
            Assert.True(_viewModel.HasError);
            Assert.Contains("Cart is empty", _viewModel.ErrorMessage);
            Assert.False(_viewModel.HasSuccess);
            
            // Verify mutation was NOT called
            _graphQLClientMock.Verify(x => x.ExecuteAsync<OrderResult>(
                It.IsAny<string>(), It.IsAny<object>(), "createOrder"), Times.Never);
        }

        [Fact]
        public async Task Receive_LoginSucceededMessage_SetsSaleId()
        {
            // Arrange
            var user = new LoginUser { Id = 42, Username = "StaffUser" };
            var message = new LoginSucceededMessage(user);

            // Act
            _viewModel.Receive(message);
            
            // To assert the SaleId is used, we add an item and process payment
            var productMock = new ProductItemViewModel(new Product { Id = 1, Name = "Toy", RetailPrice = 10m, StockQuantity = 100 });
            _viewModel.AddToCartCommand.Execute(productMock);

            _graphQLClientMock.Setup(x => x.ExecuteAsync<OrderResult>(
                It.IsAny<string>(),
                It.Is<object>(args => SerializeAndCheckSaleId(args, 42)),
                "createOrder")).ReturnsAsync(new OrderResult());

            await _viewModel.ProcessPaymentCommand.ExecuteAsync(null);

            // Assert handled by Moq Verify in the Setup
            _graphQLClientMock.Verify(x => x.ExecuteAsync<OrderResult>(
                It.IsAny<string>(),
                It.Is<object>(args => SerializeAndCheckSaleId(args, 42)),
                "createOrder"), Times.Once);
        }

        private static bool SerializeAndCheckSaleId(object args, int expectedSaleId)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(args);
            return json.Contains($"\"saleId\":{expectedSaleId}");
        }
    }
}
