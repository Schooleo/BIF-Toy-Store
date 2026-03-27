using BIF.ToyStore.Core.Enums;
using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BIF.ToyStore.Infrastructure.Services
{
    public class OrderService(AppDbContext dbContext) : IOrderService
    {
        private readonly AppDbContext _dbContext = dbContext;

        public async Task<Order> CreateOrderAsync(
            int saleId,
            int? customerId,
            List<(int ProductId, int Quantity, decimal UnitPrice)> items)
        {
            using var transaction = await _dbContext.Database.BeginTransactionAsync();

            try
            {
                var order = new Order
                {
                    SaleId = saleId,
                    CustomerId = customerId,
                    OrderDate = DateTime.Now,
                    Status = OrderStatus.New
                };

                _dbContext.Orders.Add(order);
                await _dbContext.SaveChangesAsync();

                decimal totalAmount = 0m;

                foreach (var item in items)
                {
                    var product = await _dbContext.Products.FindAsync(item.ProductId)
                        ?? throw new InvalidOperationException(
                            $"Product with ID {item.ProductId} not found.");

                    if (product.StockQuantity < item.Quantity)
                    {
                        throw new InvalidOperationException(
                            $"Insufficient stock for product '{product.Name}'. " +
                            $"Available: {product.StockQuantity}, Requested: {item.Quantity}.");
                    }

                    var detail = new OrderDetail
                    {
                        OrderId = order.Id,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        UnitImportPrice = product.ImportPrice
                    };

                    _dbContext.OrderDetails.Add(detail);
                    totalAmount += item.Quantity * item.UnitPrice;

                    // Deduct stock
                    product.StockQuantity -= item.Quantity;
                }

                order.TotalAmount = totalAmount;
                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                // Reload with navigation properties
                return (await GetOrderByIdAsync(order.Id))!;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<Order> UpdateOrderAsync(int id, OrderStatus? status, int? customerId)
        {
            var order = await _dbContext.Orders.FindAsync(id)
                ?? throw new InvalidOperationException($"Order with ID {id} not found.");

            if (status.HasValue)
            {
                order.Status = status.Value;
            }

            if (customerId.HasValue)
            {
                order.CustomerId = customerId.Value;
            }

            await _dbContext.SaveChangesAsync();

            return (await GetOrderByIdAsync(order.Id))!;
        }

        public async Task<bool> DeleteOrderAsync(int id)
        {
            var order = await _dbContext.Orders.FindAsync(id);
            if (order is null)
            {
                return false;
            }

            // Soft-delete: hide the row instead of removing it
            order.IsDeleted = true;
            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<(List<Order> Items, int TotalCount)> GetOrdersAsync(
            int page,
            int pageSize,
            DateTime? fromDate,
            DateTime? toDate,
            int? employeeId = null)
        {
            var query = _dbContext.Orders
                .Include(o => o.Sale)
                .Include(o => o.Customer)
                .AsQueryable();

            if (fromDate.HasValue)
            {
                query = query.Where(o => o.OrderDate >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(o => o.OrderDate <= toDate.Value);
            }

            if (employeeId.HasValue)
            {
                query = query.Where(o => o.SaleId == employeeId.Value);
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(o => o.OrderDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<Order?> GetOrderByIdAsync(int id)
        {
            return await _dbContext.Orders
                .Include(o => o.Sale)
                .Include(o => o.Customer)
                .Include(o => o.OrderDetails)
                    .ThenInclude(d => d.Product)
                .FirstOrDefaultAsync(o => o.Id == id);
        }

        public async Task<List<SaleKpiRanking>> GetSaleKpiRankingAsync(DateTime? fromDate, DateTime? toDate)
        {
            var sales = await _dbContext.Users
                .AsNoTracking()
                .Where(u => u.Role == UserRole.Sale)
                .Select(u => new { u.Id, u.Username })
                .ToListAsync();

            var orders = _dbContext.Orders.AsNoTracking().AsQueryable();

            if (fromDate.HasValue)
            {
                orders = orders.Where(o => o.OrderDate >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                orders = orders.Where(o => o.OrderDate <= toDate.Value);
            }

            var salesStats = await orders
                .GroupBy(o => o.SaleId)
                .Select(g => new
                {
                    SaleId = g.Key,
                    TotalOrders = g.Count(),
                    TotalRevenue = g.Sum(x => x.TotalAmount)
                })
                .ToListAsync();

            var ranking = sales
                .Select(sale =>
                {
                    var stat = salesStats.FirstOrDefault(x => x.SaleId == sale.Id);

                    return new SaleKpiRanking
                    {
                        SaleId = sale.Id,
                        SaleName = sale.Username,
                        TotalOrders = stat?.TotalOrders ?? 0,
                        TotalRevenue = stat?.TotalRevenue ?? 0m
                    };
                })
                .OrderByDescending(x => x.TotalRevenue)
                .ThenByDescending(x => x.TotalOrders)
                .ThenBy(x => x.SaleId)
                .ToList();

            for (int i = 0; i < ranking.Count; i++)
            {
                ranking[i].Rank = i + 1;
            }

            return ranking;
        }

        public async Task<List<RevenueTrendPoint>> GetRevenueTrendAsync(int days)
        {
            int safeDays = Math.Clamp(days, 1, 62);
            var endDate = DateTime.Today;
            var startDate = endDate.AddDays(-(safeDays - 1));

            var dailyRevenue = await _dbContext.Orders
                .AsNoTracking()
                .Where(o => !o.IsDeleted && o.OrderDate.Date >= startDate && o.OrderDate.Date <= endDate)
                .GroupBy(o => o.OrderDate.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Revenue = g.Sum(x => x.TotalAmount)
                })
                .ToListAsync();

            var revenueByDate = dailyRevenue.ToDictionary(x => x.Date, x => x.Revenue);

            var result = new List<RevenueTrendPoint>(safeDays);
            for (int i = 0; i < safeDays; i++)
            {
                var date = startDate.AddDays(i);
                revenueByDate.TryGetValue(date, out var revenue);

                result.Add(new RevenueTrendPoint
                {
                    Date = date,
                    Revenue = revenue
                });
            }

            return result;
        }

        public async Task<List<BestSellingProductStat>> GetTopBestSellingProductsAsync(int take)
        {
            int safeTake = Math.Clamp(take, 1, 20);

            var rows = await (
                from detail in _dbContext.OrderDetails.AsNoTracking()
                join order in _dbContext.Orders.AsNoTracking() on detail.OrderId equals order.Id
                join product in _dbContext.Products.AsNoTracking() on detail.ProductId equals product.Id
                join category in _dbContext.Categories.AsNoTracking()
                    on product.CategoryId equals category.Id into categoryGroup
                from category in categoryGroup.DefaultIfEmpty()
                where !order.IsDeleted
                group new { detail, product, category } by new
                {
                    product.Id,
                    product.Name,
                    product.RetailPrice,
                    CategoryName = category != null ? category.Name : "Unknown"
                }
                into g
                orderby g.Sum(x => x.detail.Quantity) descending, g.Key.Name
                select new BestSellingProductStat
                {
                    ProductId = g.Key.Id,
                    ProductName = g.Key.Name,
                    CategoryName = g.Key.CategoryName,
                    RetailPrice = g.Key.RetailPrice,
                    UnitsSold = g.Sum(x => x.detail.Quantity)
                })
                .Take(safeTake)
                .ToListAsync();

            for (int i = 0; i < rows.Count; i++)
            {
                rows[i].Rank = i + 1;
            }

            return rows;
        }
    }
}
