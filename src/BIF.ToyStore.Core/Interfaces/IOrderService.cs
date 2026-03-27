using BIF.ToyStore.Core.Enums;
using BIF.ToyStore.Core.Models;

namespace BIF.ToyStore.Core.Interfaces
{
    public interface IOrderService
    {
        Task<Order> CreateOrderAsync(
            int saleId,
            int? customerId,
            List<(int ProductId, int Quantity, decimal UnitPrice)> items);

        Task<Order> UpdateOrderAsync(int id, OrderStatus? status, int? customerId);

        Task<bool> DeleteOrderAsync(int id);

        Task<(List<Order> Items, int TotalCount)> GetOrdersAsync(
            int page,
            int pageSize,
            DateTime? fromDate,
            DateTime? toDate,
            int? employeeId = null);

        Task<Order?> GetOrderByIdAsync(int id);

        Task<List<SaleKpiRanking>> GetSaleKpiRankingAsync(DateTime? fromDate, DateTime? toDate);

        Task<List<RevenueTrendPoint>> GetRevenueTrendAsync(int days);

        Task<List<BestSellingProductStat>> GetTopBestSellingProductsAsync(int take);
    }
}
