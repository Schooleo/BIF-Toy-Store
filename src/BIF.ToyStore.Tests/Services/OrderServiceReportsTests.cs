using BIF.ToyStore.Core.Enums;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.Infrastructure.Data;
using BIF.ToyStore.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace BIF.ToyStore.Tests.Services
{
    public class OrderServiceReportsTests : IDisposable
    {
        private readonly AppDbContext _dbContext;
        private readonly OrderService _orderService;

        public OrderServiceReportsTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _dbContext = new AppDbContext(options);
            _orderService = new OrderService(_dbContext);

            Seed();
        }

        public void Dispose() => _dbContext.Dispose();

        [Fact]
        public async Task GetReportTimeSeriesAsync_DayGrouping_FillsMissingDaysAndAggregates()
        {
            var orderA = await _orderService.CreateOrderAsync(1, null, [(1, 2, 10m)]);
            var orderB = await _orderService.CreateOrderAsync(1, null, [(2, 1, 20m)]);
            var unpaidOrder = await _orderService.CreateOrderAsync(1, null, [(1, 4, 10m)]);

            await SetOrderDateAsync(orderA.Id, new DateTime(2026, 3, 1, 10, 0, 0));
            await SetOrderDateAsync(orderB.Id, new DateTime(2026, 3, 3, 11, 0, 0));
            await SetOrderDateAsync(unpaidOrder.Id, new DateTime(2026, 3, 1, 12, 0, 0));
            await SetOrderStatusAsync(orderA.Id, OrderStatus.Paid);
            await SetOrderStatusAsync(orderB.Id, OrderStatus.Paid);

            var points = await _orderService.GetReportTimeSeriesAsync(
                new DateTime(2026, 3, 1),
                new DateTime(2026, 3, 3),
                ReportGroupBy.Day);

            Assert.Equal(3, points.Count);

            Assert.Equal(new DateTime(2026, 3, 1), points[0].PeriodStart.Date);
            Assert.Equal(2, points[0].TotalQuantity);
            Assert.Equal(20m, points[0].TotalRevenue);
            Assert.Equal(10m, points[0].TotalProfit);

            Assert.Equal(new DateTime(2026, 3, 2), points[1].PeriodStart.Date);
            Assert.Equal(0, points[1].TotalQuantity);
            Assert.Equal(0m, points[1].TotalRevenue);
            Assert.Equal(0m, points[1].TotalProfit);

            Assert.Equal(new DateTime(2026, 3, 3), points[2].PeriodStart.Date);
            Assert.Equal(1, points[2].TotalQuantity);
            Assert.Equal(20m, points[2].TotalRevenue);
            Assert.Equal(8m, points[2].TotalProfit);
        }

        [Fact]
        public async Task GetReportTimeSeriesAsync_WeekGrouping_AggregatesByMondayBucket()
        {
            var orderA = await _orderService.CreateOrderAsync(1, null, [(1, 1, 10m)]);
            var orderB = await _orderService.CreateOrderAsync(1, null, [(1, 3, 10m)]);

            await SetOrderDateAsync(orderA.Id, new DateTime(2026, 3, 3, 10, 0, 0));  // week of Mar 2
            await SetOrderDateAsync(orderB.Id, new DateTime(2026, 3, 10, 10, 0, 0)); // week of Mar 9
            await SetOrderStatusAsync(orderA.Id, OrderStatus.Paid);
            await SetOrderStatusAsync(orderB.Id, OrderStatus.Paid);

            var points = await _orderService.GetReportTimeSeriesAsync(
                new DateTime(2026, 3, 2),
                new DateTime(2026, 3, 15),
                ReportGroupBy.Week);

            Assert.Equal(2, points.Count);
            Assert.Equal(new DateTime(2026, 3, 2), points[0].PeriodStart.Date);
            Assert.Equal(1, points[0].TotalQuantity);
            Assert.Equal(new DateTime(2026, 3, 9), points[1].PeriodStart.Date);
            Assert.Equal(3, points[1].TotalQuantity);
        }

        [Fact]
        public async Task GetReportTopProductsAsync_ReturnsRankedRows_ExcludingSoftDeletedOrders()
        {
            var liveA = await _orderService.CreateOrderAsync(1, null, [(1, 3, 10m)]);
            var liveB = await _orderService.CreateOrderAsync(1, null, [(2, 2, 20m)]);
            var deleted = await _orderService.CreateOrderAsync(1, null, [(1, 5, 10m)]);
            var unpaid = await _orderService.CreateOrderAsync(1, null, [(2, 10, 20m)]);

            await SetOrderDateAsync(liveA.Id, new DateTime(2026, 2, 10, 10, 0, 0));
            await SetOrderDateAsync(liveB.Id, new DateTime(2026, 2, 10, 11, 0, 0));
            await SetOrderDateAsync(deleted.Id, new DateTime(2026, 2, 11, 10, 0, 0));
            await SetOrderDateAsync(unpaid.Id, new DateTime(2026, 2, 11, 11, 0, 0));
            await SetOrderStatusAsync(liveA.Id, OrderStatus.Paid);
            await SetOrderStatusAsync(liveB.Id, OrderStatus.Paid);
            await SetOrderStatusAsync(deleted.Id, OrderStatus.Paid);

            await _orderService.DeleteOrderAsync(deleted.Id);

            var rows = await _orderService.GetReportTopProductsAsync(
                new DateTime(2026, 2, 1),
                new DateTime(2026, 2, 28),
                10);

            Assert.Equal(2, rows.Count);

            Assert.Equal(1, rows[0].Rank);
            Assert.Equal(1, rows[0].ProductId);
            Assert.Equal(3, rows[0].TotalQuantity);
            Assert.Equal(30m, rows[0].TotalRevenue);
            Assert.Equal(15m, rows[0].TotalProfit);

            Assert.Equal(2, rows[1].Rank);
            Assert.Equal(2, rows[1].ProductId);
            Assert.Equal(2, rows[1].TotalQuantity);
            Assert.Equal(40m, rows[1].TotalRevenue);
            Assert.Equal(16m, rows[1].TotalProfit);
        }

        private void Seed()
        {
            _dbContext.Users.Add(new User
            {
                Id = 1,
                Username = "sale01",
                PasswordHash = "hash",
                Role = UserRole.Sale
            });

            _dbContext.Categories.AddRange(
                new Category { Id = 1, Name = "Puzzles" },
                new Category { Id = 2, Name = "Figures" });

            _dbContext.Products.AddRange(
                new Product
                {
                    Id = 1,
                    Name = "Puzzle A",
                    CategoryId = 1,
                    RetailPrice = 10m,
                    ImportPrice = 5m,
                    StockQuantity = 100
                },
                new Product
                {
                    Id = 2,
                    Name = "Figure B",
                    CategoryId = 2,
                    RetailPrice = 20m,
                    ImportPrice = 12m,
                    StockQuantity = 100
                });

            _dbContext.SaveChanges();
        }

        private async Task SetOrderDateAsync(int orderId, DateTime value)
        {
            var order = await _dbContext.Orders.IgnoreQueryFilters().FirstAsync(x => x.Id == orderId);
            order.OrderDate = value;
            await _dbContext.SaveChangesAsync();
        }

        private async Task SetOrderStatusAsync(int orderId, OrderStatus status)
        {
            var order = await _dbContext.Orders.IgnoreQueryFilters().FirstAsync(x => x.Id == orderId);
            order.Status = status;
            await _dbContext.SaveChangesAsync();
        }
    }
}
