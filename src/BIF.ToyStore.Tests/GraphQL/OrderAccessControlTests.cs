using BIF.ToyStore.Core.Enums;
using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.Infrastructure.Data;
using BIF.ToyStore.Infrastructure.GraphQL;
using BIF.ToyStore.Infrastructure.Repositories;
using BIF.ToyStore.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace BIF.ToyStore.Tests.GraphQL
{
    public class OrderAccessControlTests : IDisposable
    {
        private readonly AppDbContext _dbContext;
        private readonly IOrderService _orderService;
        private readonly Queries _queries = new();
        private readonly Mutations _mutations = new();

        public OrderAccessControlTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _dbContext = new AppDbContext(options);
            _orderService = new OrderService(new OrderRepository(_dbContext));

            SeedData();
        }

        public void Dispose()
        {
            _dbContext.Dispose();
        }

        [Fact]
        public void Orders_SaleContext_ReturnsOnlyOwnOrders()
        {
            var orders = _queries.Orders(
                    fromDate: null,
                    toDate: null,
                    employeeId: null,
                    dbContext: _dbContext,
                    currentUserId: 1,
                    currentUserRole: UserRole.Sale.ToString())
                .ToList();

            Assert.NotEmpty(orders);
            Assert.All(orders, o => Assert.Equal(1, o.SaleId));
        }

        [Fact]
        public async Task GetOrderById_SaleContext_OtherUsersOrder_ReturnsNull()
        {
            var payload = await _queries.GetOrderById(
                id: 1002,
                orderService: _orderService,
                currentUserId: 1,
                currentUserRole: UserRole.Sale.ToString());

            Assert.Null(payload);
        }

        [Fact]
        public async Task UpdateOrder_SaleContext_OtherUsersOrder_ThrowsInvalidOperationException()
        {
            var input = new UpdateOrderInput
            {
                Id = 1002,
                Status = OrderStatus.Paid,
                CustomerId = null
            };

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _mutations.UpdateOrder(
                    input,
                    _orderService,
                    currentUserId: 1,
                    currentUserRole: UserRole.Sale.ToString()));

            Assert.Contains("own orders", exception.Message);
        }

        private void SeedData()
        {
            _dbContext.Users.AddRange(
                new User { Id = 1, Username = "alice", PasswordHash = "hash-1", Role = UserRole.Sale },
                new User { Id = 2, Username = "bob", PasswordHash = "hash-2", Role = UserRole.Sale },
                new User { Id = 3, Username = "admin", PasswordHash = "hash-3", Role = UserRole.Admin });

            _dbContext.Orders.AddRange(
                new Order
                {
                    Id = 1001,
                    SaleId = 1,
                    OrderDate = DateTime.UtcNow.AddMinutes(-20),
                    Status = OrderStatus.New,
                    TotalAmount = 120m
                },
                new Order
                {
                    Id = 1002,
                    SaleId = 2,
                    OrderDate = DateTime.UtcNow.AddMinutes(-10),
                    Status = OrderStatus.New,
                    TotalAmount = 85m
                });

            _dbContext.SaveChanges();
        }
    }
}
