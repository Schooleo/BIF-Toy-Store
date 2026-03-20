namespace BIF.ToyStore.Infrastructure.GraphQL
{
    using BIF.ToyStore.Core.Interfaces;

    public class Queries
    {
        public string Ping() => "The BIF Toy Store GraphQL server is running.";

        public async Task<SetupStatePayload> SetupState([Service] IConfigService configService)
        {
            return new SetupStatePayload
            {
                IsInitialSetupCompleted = await configService.IsInitialSetupCompletedAsync()
            };
        }

        public async Task<AppConfigPayload> AppConfig([Service] IConfigService configService)
        {
            var config = await configService.GetConfigAsync();
            return AppConfigPayload.FromConfig(config);
        }


        public async Task<OrderListPayload> GetOrders(
            int page,
            int pageSize,
            DateTime? fromDate,
            DateTime? toDate,
            [Service] IOrderService orderService)
        {
            var (items, totalCount) = await orderService.GetOrdersAsync(
                page, pageSize, fromDate, toDate);

            return new OrderListPayload
            {
                Items = items.Select(OrderPayload.FromOrder).ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<OrderPayload?> GetOrderById(
            int id,
            [Service] IOrderService orderService)
        {
            var order = await orderService.GetOrderByIdAsync(id);
            return order is not null ? OrderPayload.FromOrder(order) : null;
        }
    }
}

