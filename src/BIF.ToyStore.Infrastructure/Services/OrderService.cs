using BIF.ToyStore.Core.Enums;
using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;

namespace BIF.ToyStore.Infrastructure.Services
{
    public class OrderService(IOrderRepository orderRepository) : IOrderService
    {
        private readonly IOrderRepository _orderRepository = orderRepository;

        public async Task<Order> CreateOrderAsync(
            int saleId,
            int? customerId,
            List<(int ProductId, int Quantity, decimal UnitPrice)> items)
        {
            return await _orderRepository.CreateOrderAsync(saleId, customerId, items);
        }

        public async Task<Order> UpdateOrderAsync(int id, OrderStatus? status, int? customerId)
        {
            return await _orderRepository.UpdateOrderAsync(id, status, customerId);
        }

        public async Task<bool> DeleteOrderAsync(int id)
        {
            return await _orderRepository.DeleteOrderAsync(id);
        }

        public async Task<(List<Order> Items, int TotalCount)> GetOrdersAsync(
            int page,
            int pageSize,
            DateTime? fromDate,
            DateTime? toDate,
            int? employeeId = null)
        {
            return await _orderRepository.GetOrdersAsync(page, pageSize, fromDate, toDate, employeeId);
        }

        public async Task<Order?> GetOrderByIdAsync(int id)
        {
            return await _orderRepository.GetOrderByIdAsync(id);
        }

        public async Task<List<SaleKpiRanking>> GetSaleKpiRankingAsync(DateTime? fromDate, DateTime? toDate)
        {
            return await _orderRepository.GetSaleKpiRankingAsync(fromDate, toDate);
        }

        public async Task<List<RevenueTrendPoint>> GetRevenueTrendAsync(int days)
        {
            return await _orderRepository.GetRevenueTrendAsync(days);
        }

        public async Task<List<BestSellingProductStat>> GetTopBestSellingProductsAsync(int take)
        {
            return await _orderRepository.GetTopBestSellingProductsAsync(take);
        }

        public async Task<List<ReportTimeSeriesPoint>> GetReportTimeSeriesAsync(
            DateTime startDate,
            DateTime endDate,
            ReportGroupBy groupBy)
        {
            return await _orderRepository.GetReportTimeSeriesAsync(startDate, endDate, groupBy);
        }

        public async Task<List<ReportTopProductPoint>> GetReportTopProductsAsync(
            DateTime startDate,
            DateTime endDate,
            int take)
        {
            return await _orderRepository.GetReportTopProductsAsync(startDate, endDate, take);
        }
    }
}
