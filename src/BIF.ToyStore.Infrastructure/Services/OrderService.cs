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
            DateTime? toDate)
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
    }
}
