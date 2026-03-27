using BIF.ToyStore.Core.Enums;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.Infrastructure.Data;
using BIF.ToyStore.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace BIF.ToyStore.Tests.Services
{
    /// <summary>
    /// Tests for the new employeeId filter added to GetOrdersAsync.
    /// The existing OrderServiceTests cover all other scenarios;
    /// this file focuses solely on the new filtering capability.
    /// </summary>
    public class OrderServiceEmployeeFilterTests : IDisposable
    {
        private readonly AppDbContext _dbContext;
        private readonly OrderService _orderService;

        public OrderServiceEmployeeFilterTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _dbContext = new AppDbContext(options);
            _orderService = new OrderService(_dbContext);

            SeedTestData();
        }

        private void SeedTestData()
        {
            _dbContext.Users.AddRange(
                new User { Id = 1, Username = "alice", PasswordHash = "h1", Role = UserRole.Sale },
                new User { Id = 2, Username = "bob",   PasswordHash = "h2", Role = UserRole.Sale }
            );
            _dbContext.Products.Add(
                new Product { Id = 1, Name = "Item", RetailPrice = 10m, ImportPrice = 5m, StockQuantity = 100 }
            );
            _dbContext.SaveChanges();
        }

        public void Dispose() => _dbContext.Dispose();

        // ── Helper ────────────────────────────────────────────────────────────

        private async Task<Order> CreateOrderForEmployee(int employeeId)
        {
            var items = new List<(int, int, decimal)> { (1, 1, 10m) };
            return await _orderService.CreateOrderAsync(employeeId, null, items);
        }

        // ── Tests ─────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetOrdersAsync_EmployeeIdNull_ReturnsAllOrders()
        {
            await CreateOrderForEmployee(1);
            await CreateOrderForEmployee(2);
            await CreateOrderForEmployee(1);

            var (items, total) = await _orderService.GetOrdersAsync(1, 10, null, null, employeeId: null);

            Assert.Equal(3, total);
            Assert.Equal(3, items.Count);
        }

        [Fact]
        public async Task GetOrdersAsync_EmployeeId1_ReturnsOnlyAlicesOrders()
        {
            await CreateOrderForEmployee(1);   // alice
            await CreateOrderForEmployee(2);   // bob
            await CreateOrderForEmployee(1);   // alice

            var (items, total) = await _orderService.GetOrdersAsync(1, 10, null, null, employeeId: 1);

            Assert.Equal(2, total);
            Assert.All(items, o => Assert.Equal(1, o.SaleId));
        }

        [Fact]
        public async Task GetOrdersAsync_EmployeeId2_ReturnsOnlyBobsOrders()
        {
            await CreateOrderForEmployee(1);
            await CreateOrderForEmployee(2);

            var (items, total) = await _orderService.GetOrdersAsync(1, 10, null, null, employeeId: 2);

            Assert.Equal(1, total);
            Assert.Single(items);
            Assert.Equal(2, items[0].SaleId);
        }

        [Fact]
        public async Task GetOrdersAsync_EmployeeIdWithNoOrders_ReturnsEmpty()
        {
            await CreateOrderForEmployee(1);

            var (items, total) = await _orderService.GetOrdersAsync(1, 10, null, null, employeeId: 2);

            Assert.Empty(items);
            Assert.Equal(0, total);
        }

        [Fact]
        public async Task GetOrdersAsync_EmployeeIdCombinedWithDateFilter_IntersectsCorrectly()
        {
            var old = await CreateOrderForEmployee(1);

            // Manually back-date that order so it falls outside the date window
            var order = await _dbContext.Orders.FindAsync(old.Id);
            order!.OrderDate = DateTime.Now.AddDays(-10);
            await _dbContext.SaveChangesAsync();

            await CreateOrderForEmployee(1);  // recent order

            var from = DateTime.Now.AddHours(-1);
            var to   = DateTime.Now.AddHours(1);

            var (items, total) = await _orderService.GetOrdersAsync(1, 10, from, to, employeeId: 1);

            Assert.Equal(1, total);
            Assert.Single(items);
        }

        [Fact]
        public async Task GetOrdersAsync_EmployeeFilterRespectsPagination()
        {
            for (int i = 0; i < 5; i++)
                await CreateOrderForEmployee(1);
            await CreateOrderForEmployee(2);

            var (page1, total) = await _orderService.GetOrdersAsync(1, 3, null, null, employeeId: 1);

            Assert.Equal(5, total);
            Assert.Equal(3, page1.Count);

            var (page2, _) = await _orderService.GetOrdersAsync(2, 3, null, null, employeeId: 1);
            Assert.Equal(2, page2.Count);
        }

        [Fact]
        public async Task GetOrdersAsync_SoftDeletedOrders_ExcludedEvenWithEmployeeFilter()
        {
            var order = await CreateOrderForEmployee(1);
            await _orderService.DeleteOrderAsync(order.Id);

            await CreateOrderForEmployee(1);  // live order

            var (items, total) = await _orderService.GetOrdersAsync(1, 10, null, null, employeeId: 1);

            Assert.Equal(1, total);
            Assert.Single(items);
        }
    }
}
