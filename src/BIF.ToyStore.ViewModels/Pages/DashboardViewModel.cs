using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.ViewModels.Base;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Globalization;

namespace BIF.ToyStore.ViewModels.Pages
{
    public partial class DashboardViewModel : BaseViewModel
    {
        private const int LowStockTake = 5;
        private const int RecentOrdersTake = 3;
        private const int CriticalStockThreshold = 0;

        private readonly IGraphQLClient _graphQLClient;

        [ObservableProperty]
        private ObservableCollection<LowStockProductViewModel> _lowStockProducts = new();

        [ObservableProperty]
        private ObservableCollection<RecentOrderViewModel> _recentOrders = new();

        [ObservableProperty]
        private int _totalProducts;

        [ObservableProperty]
        private int _ordersToday;

        [ObservableProperty]
        private decimal _todayRevenue;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        public DashboardViewModel(IGraphQLClient graphQLClient)
        {
            _graphQLClient = graphQLClient;
            Title = "Workshop Overview";
        }

        public async Task LoadAsync()
        {
            try
            {
                IsBusy = true;
                ErrorMessage = string.Empty;

                await LoadLowStockAndRecentOrdersAsync();
                await LoadTodaySummaryAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = "Unable to load dashboard data: " + ex.Message;
                LowStockProducts.Clear();
                RecentOrders.Clear();
                TotalProducts = 0;
                OrdersToday = 0;
                TodayRevenue = 0m;
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            await LoadAsync();
        }

        private async Task LoadLowStockAndRecentOrdersAsync()
        {
            const string query = @"query DashboardMain($lowStockTake: Int!, $recentOrdersPageSize: Int!) {
                products(first: $lowStockTake, order: { stockQuantity: ASC }) {
                    totalCount
                    nodes {
                        id
                        name
                        stockQuantity
                        retailPrice
                        category {
                            name
                        }
                    }
                }
                getOrders(page: 1, pageSize: $recentOrdersPageSize) {
                    items {
                        id
                        orderDate
                        status
                        totalAmount
                        customerName
                        orderDetails {
                            quantity
                        }
                    }
                }
            }";

            var variables = new
            {
                lowStockTake = LowStockTake,
                recentOrdersPageSize = RecentOrdersTake
            };

            var payload = await _graphQLClient.ExecuteAsync<DashboardMainQueryData>(query, variables)
                ?? new DashboardMainQueryData();

            LowStockProducts.Clear();
            foreach (var product in payload.Products.Nodes
                .OrderBy(x => x.StockQuantity)
                .Take(LowStockTake))
            {
                LowStockProducts.Add(new LowStockProductViewModel
                {
                    Id = product.Id,
                    Name = product.Name,
                    CategoryName = product.Category?.Name ?? "Unknown",
                    StockQuantity = product.StockQuantity,
                    RetailPrice = product.RetailPrice,
                    IsCritical = product.StockQuantity <= CriticalStockThreshold
                });
            }

            TotalProducts = payload.Products.TotalCount;

            RecentOrders.Clear();
            foreach (var order in payload.GetOrders.Items.Take(RecentOrdersTake))
            {
                int itemCount = order.OrderDetails.Sum(x => x.Quantity);

                RecentOrders.Add(new RecentOrderViewModel
                {
                    Id = order.Id,
                    Status = order.Status,
                    CustomerName = string.IsNullOrWhiteSpace(order.CustomerName)
                        ? "Walk-in Customer"
                        : order.CustomerName,
                    ItemCount = itemCount,
                    TotalAmount = order.TotalAmount,
                    OrderedAt = order.OrderDate
                });
            }
        }

        private async Task LoadTodaySummaryAsync()
        {
            var todayStart = DateTime.Today;
            var todayEnd = todayStart.AddDays(1).AddTicks(-1);

            const string todayQuery = @"query DashboardToday($fromDate: DateTime, $toDate: DateTime) {
                getOrders(page: 1, pageSize: 200, fromDate: $fromDate, toDate: $toDate) {
                    items {
                        totalAmount
                    }
                }
            }";

            var variables = new
            {
                fromDate = todayStart,
                toDate = todayEnd
            };

            var payload = await _graphQLClient.ExecuteAsync<DashboardTodayQueryData>(todayQuery, variables)
                ?? new DashboardTodayQueryData();

            OrdersToday = payload.GetOrders.Items.Count;
            TodayRevenue = payload.GetOrders.Items.Sum(x => x.TotalAmount);
        }
    }

    public sealed class LowStockProductViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public int StockQuantity { get; set; }
        public decimal RetailPrice { get; set; }
        public bool IsCritical { get; set; }

        public string PriceDisplay => RetailPrice.ToString("C", CultureInfo.GetCultureInfo("en-US"));
    }

    public sealed class RecentOrderViewModel
    {
        public int Id { get; set; }
        public string Status { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public int ItemCount { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime OrderedAt { get; set; }

        public string OrderIdDisplay => $"#{Id}";

        public string TotalAmountDisplay => TotalAmount.ToString("C", CultureInfo.GetCultureInfo("en-US"));

        public string ItemCountDisplay => ItemCount == 1
            ? "1 product"
            : $"{ItemCount} products";

        public string RelativeTimeDisplay
        {
            get
            {
                var delta = DateTime.Now - OrderedAt;
                if (delta.TotalMinutes < 1)
                {
                    return "Just now";
                }

                if (delta.TotalHours < 1)
                {
                    return $"{Math.Max(1, (int)delta.TotalMinutes)}m ago";
                }

                if (delta.TotalDays < 1)
                {
                    return $"{Math.Max(1, (int)delta.TotalHours)}h ago";
                }

                return OrderedAt.ToString("MMM dd");
            }
        }
    }

    public sealed class DashboardMainQueryData
    {
        public DashboardProductConnection Products { get; set; } = new();
        public DashboardOrderList GetOrders { get; set; } = new();
    }

    public sealed class DashboardTodayQueryData
    {
        public DashboardOrderList GetOrders { get; set; } = new();
    }

    public sealed class DashboardProductConnection
    {
        public int TotalCount { get; set; }
        public List<DashboardProductNode> Nodes { get; set; } = new();
    }

    public sealed class DashboardProductNode
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int StockQuantity { get; set; }
        public decimal RetailPrice { get; set; }
        public DashboardCategoryNode? Category { get; set; }
    }

    public sealed class DashboardCategoryNode
    {
        public string Name { get; set; } = string.Empty;
    }

    public sealed class DashboardOrderList
    {
        public List<DashboardOrderNode> Items { get; set; } = new();
    }

    public sealed class DashboardOrderNode
    {
        public int Id { get; set; }
        public DateTime OrderDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string? CustomerName { get; set; }
        public List<DashboardOrderDetailNode> OrderDetails { get; set; } = new();
    }

    public sealed class DashboardOrderDetailNode
    {
        public int Quantity { get; set; }
    }
}