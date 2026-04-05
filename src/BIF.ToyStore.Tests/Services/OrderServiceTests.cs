using BIF.ToyStore.Core.Enums;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.Infrastructure.Data;
using BIF.ToyStore.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace BIF.ToyStore.Tests.Services
{
    public class OrderServiceTests : IDisposable
    {
        private readonly AppDbContext _dbContext;
        private readonly OrderService _orderService;

        public OrderServiceTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _dbContext = new AppDbContext(options);
            _orderService = new OrderService(_dbContext);

            // Seed minimal stubs — Product/Category module is owned by another member
            SeedTestData();
        }

        private void SeedTestData()
        {
            _dbContext.Users.Add(new User
            {
                Id = 1,
                Username = "sale01",
                PasswordHash = "hashed",
                Role = UserRole.Sale
            });

            _dbContext.Customers.Add(new Customer
            {
                Id = 1,
                FullName = "Test Customer",
                PhoneNumber = "0123456789"
            });

            // Stub products — only fields needed for Order logic
            _dbContext.Products.AddRange(
                new Product { Id = 1, Name = "Stub A", RetailPrice = 50m, ImportPrice = 30m, StockQuantity = 100 },
                new Product { Id = 2, Name = "Stub B", RetailPrice = 80m, ImportPrice = 45m, StockQuantity = 5 }
            );

            _dbContext.SaveChanges();
        }

        public void Dispose() => _dbContext.Dispose();

        // ============================================================
        //  CreateOrderAsync
        // ============================================================

        [Fact]
        public async Task CreateOrderAsync_ValidItems_ReturnsOrderWithCorrectTotal()
        {
            var items = new List<(int, int, decimal)> { (1, 2, 50m), (2, 1, 80m) };
            var order = await _orderService.CreateOrderAsync(1, 1, items);

            Assert.Equal(180m, order.TotalAmount);  // (2×50) + (1×80)
            Assert.Equal(2, order.OrderDetails.Count);
        }

        [Fact]
        public async Task CreateOrderAsync_ValidItems_SetsStatusToNew()
        {
            var items = new List<(int, int, decimal)> { (1, 1, 50m) };
            var order = await _orderService.CreateOrderAsync(1, null, items);

            Assert.Equal(OrderStatus.New, order.Status);
        }

        [Fact]
        public async Task CreateOrderAsync_ValidItems_DeductsProductStock()
        {
            var items = new List<(int, int, decimal)> { (1, 3, 50m) };
            await _orderService.CreateOrderAsync(1, null, items);

            var product = await _dbContext.Products.FindAsync(1);
            Assert.Equal(97, product!.StockQuantity);  // 100 − 3
        }

        [Fact]
        public async Task CreateOrderAsync_ValidItems_CapturesImportPriceFromProduct()
        {
            var items = new List<(int, int, decimal)> { (1, 1, 50m) };
            var order = await _orderService.CreateOrderAsync(1, null, items);

            var detail = order.OrderDetails.First();
            Assert.Equal(30m, detail.UnitImportPrice);  // matches Stub A
        }

        [Fact]
        public async Task CreateOrderAsync_NullCustomerId_CreatesOrderWithoutCustomer()
        {
            var items = new List<(int, int, decimal)> { (1, 1, 50m) };
            var order = await _orderService.CreateOrderAsync(1, null, items);

            Assert.Null(order.CustomerId);
        }

        [Fact]
        public async Task CreateOrderAsync_WithCustomerId_AssociatesCustomer()
        {
            var items = new List<(int, int, decimal)> { (1, 1, 50m) };
            var order = await _orderService.CreateOrderAsync(1, 1, items);

            Assert.Equal(1, order.CustomerId);
        }

        [Fact]
        public async Task CreateOrderAsync_InsufficientStock_ThrowsInvalidOperation()
        {
            // Stub B has StockQuantity = 5
            var items = new List<(int, int, decimal)> { (2, 10, 80m) };

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _orderService.CreateOrderAsync(1, null, items));
        }

        [Fact]
        public async Task CreateOrderAsync_NonExistentProduct_ThrowsInvalidOperation()
        {
            var items = new List<(int, int, decimal)> { (999, 1, 10m) };

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _orderService.CreateOrderAsync(1, null, items));
        }

        // ============================================================
        //  UpdateOrderAsync
        // ============================================================

        [Fact]
        public async Task UpdateOrderAsync_ChangeStatusToPaid_UpdatesSuccessfully()
        {
            var order = await CreateSampleOrder();
            var updated = await _orderService.UpdateOrderAsync(order.Id, OrderStatus.Paid, null);

            Assert.Equal(OrderStatus.Paid, updated.Status);
        }

        [Fact]
        public async Task UpdateOrderAsync_ChangeStatusToCancelled_UpdatesSuccessfully()
        {
            var order = await CreateSampleOrder();
            var updated = await _orderService.UpdateOrderAsync(order.Id, OrderStatus.Cancelled, null);

            Assert.Equal(OrderStatus.Cancelled, updated.Status);
        }

        [Fact]
        public async Task UpdateOrderAsync_ChangeCustomerId_UpdatesSuccessfully()
        {
            var order = await CreateSampleOrder();
            var updated = await _orderService.UpdateOrderAsync(order.Id, null, 1);

            Assert.Equal(1, updated.CustomerId);
        }

        [Fact]
        public async Task UpdateOrderAsync_NullStatusAndCustomer_PreservesExistingValues()
        {
            var order = await CreateSampleOrder();
            var updated = await _orderService.UpdateOrderAsync(order.Id, null, null);

            Assert.Equal(OrderStatus.New, updated.Status);
            Assert.Null(updated.CustomerId);
        }

        [Fact]
        public async Task UpdateOrderAsync_NonExistentOrder_ThrowsInvalidOperation()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _orderService.UpdateOrderAsync(999, OrderStatus.Paid, null));
        }

        // ============================================================
        //  DeleteOrderAsync  (Soft-Delete)
        // ============================================================

        [Fact]
        public async Task DeleteOrderAsync_ExistingOrder_ReturnsTrue()
        {
            var order = await CreateSampleOrder();
            var result = await _orderService.DeleteOrderAsync(order.Id);

            Assert.True(result);
        }

        [Fact]
        public async Task DeleteOrderAsync_ExistingOrder_SetsIsDeletedFlag()
        {
            var order = await CreateSampleOrder();
            await _orderService.DeleteOrderAsync(order.Id);

            var raw = await _dbContext.Orders
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(o => o.Id == order.Id);

            Assert.NotNull(raw);
            Assert.True(raw.IsDeleted);
        }

        [Fact]
        public async Task DeleteOrderAsync_ExistingOrder_ExcludedFromNormalQueries()
        {
            var order = await CreateSampleOrder();
            await _orderService.DeleteOrderAsync(order.Id);

            var found = await _orderService.GetOrderByIdAsync(order.Id);
            Assert.Null(found);
        }

        [Fact]
        public async Task DeleteOrderAsync_NonExistentOrder_ReturnsFalse()
        {
            var result = await _orderService.DeleteOrderAsync(999);
            Assert.False(result);
        }

        // ============================================================
        //  GetOrdersAsync  (Pagination + Date Filter)
        // ============================================================

        [Fact]
        public async Task GetOrdersAsync_NoOrders_ReturnsEmptyListAndZeroCount()
        {
            var (items, totalCount) = await _orderService.GetOrdersAsync(1, 10, null, null);

            Assert.Empty(items);
            Assert.Equal(0, totalCount);
        }

        [Fact]
        public async Task GetOrdersAsync_MultipleOrders_ReturnsPaginatedPage()
        {
            await CreateSampleOrder();
            await CreateSampleOrder();
            await CreateSampleOrder();

            var (items, totalCount) = await _orderService.GetOrdersAsync(1, 2, null, null);

            Assert.Equal(2, items.Count);
            Assert.Equal(3, totalCount);
        }

        [Fact]
        public async Task GetOrdersAsync_SecondPage_ReturnsRemainingItems()
        {
            await CreateSampleOrder();
            await CreateSampleOrder();
            await CreateSampleOrder();

            var (items, totalCount) = await _orderService.GetOrdersAsync(2, 2, null, null);

            Assert.Single(items);
            Assert.Equal(3, totalCount);
        }

        [Fact]
        public async Task GetOrdersAsync_FutureDateRange_ReturnsEmptyResults()
        {
            await CreateSampleOrder();

            var (items, totalCount) = await _orderService.GetOrdersAsync(
                1, 10, DateTime.Now.AddDays(1), DateTime.Now.AddDays(2));

            Assert.Empty(items);
            Assert.Equal(0, totalCount);
        }

        [Fact]
        public async Task GetOrdersAsync_TodayDateRange_ReturnsCurrentOrders()
        {
            await CreateSampleOrder();

            var (items, totalCount) = await _orderService.GetOrdersAsync(
                1, 10, DateTime.Now.AddHours(-1), DateTime.Now.AddHours(1));

            Assert.Single(items);
            Assert.Equal(1, totalCount);
        }

        [Fact]
        public async Task GetOrdersAsync_SoftDeletedOrders_ExcludedFromResults()
        {
            var deleted = await CreateSampleOrder();
            await _orderService.DeleteOrderAsync(deleted.Id);
            await CreateSampleOrder();  // one live order

            var (items, totalCount) = await _orderService.GetOrdersAsync(1, 10, null, null);

            Assert.Single(items);
            Assert.Equal(1, totalCount);
        }

        // ============================================================
        //  GetOrderByIdAsync  (Nested Fetching)
        // ============================================================

        [Fact]
        public async Task GetOrderByIdAsync_ExistingOrder_ReturnsOrderWithDetails()
        {
            var created = await CreateSampleOrder();
            var order = await _orderService.GetOrderByIdAsync(created.Id);

            Assert.NotNull(order);
            Assert.NotEmpty(order.OrderDetails);
        }

        [Fact]
        public async Task GetOrderByIdAsync_ExistingOrder_IncludesProductNavigation()
        {
            var created = await CreateSampleOrder();
            var order = await _orderService.GetOrderByIdAsync(created.Id);

            var detail = order!.OrderDetails.First();
            Assert.NotNull(detail.Product);
            Assert.False(string.IsNullOrEmpty(detail.Product.Name));
        }

        [Fact]
        public async Task GetOrderByIdAsync_ExistingOrder_IncludesSaleNavigation()
        {
            var created = await CreateSampleOrder();
            var order = await _orderService.GetOrderByIdAsync(created.Id);

            Assert.NotNull(order!.Sale);
            Assert.Equal("sale01", order.Sale.Username);
        }

        [Fact]
        public async Task GetOrderByIdAsync_NonExistentId_ReturnsNull()
        {
            var order = await _orderService.GetOrderByIdAsync(999);
            Assert.Null(order);
        }

        // ─── Helper ──────────────────────────────────────────────────────────

        private async Task<Order> CreateSampleOrder()
        {
            var items = new List<(int, int, decimal)> { (1, 1, 50m) };
            return await _orderService.CreateOrderAsync(1, null, items);
        }
    }
}
