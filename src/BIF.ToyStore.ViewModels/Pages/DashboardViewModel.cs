using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.ViewModels.Base;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace BIF.ToyStore.ViewModels.Pages
{
    public partial class DashboardViewModel : BaseViewModel
    {
        private const int LowStockTake = 5;
        private const int RecentOrdersTake = 3;
        private const int BestSellerTake = 5;
        private const int RevenueTrendDays = 26;
        private const int CriticalStockThreshold = 0;
        private const int WarningStockThreshold = 3;

        private const double RevenueChartWidth = 1000;
        private const double RevenueChartHeight = 260;
        private const double RevenueChartPadding = 12;

        private readonly IGraphQLClient _graphQLClient;

        [ObservableProperty]
        private ObservableCollection<LowStockProductViewModel> _lowStockProducts = new();

        [ObservableProperty]
        private ObservableCollection<RecentOrderViewModel> _recentOrders = new();

        [ObservableProperty]
        private ObservableCollection<BestSellingProductViewModel> _bestSellingProducts = new();

        [ObservableProperty]
        private ObservableCollection<RevenueTrendPointViewModel> _revenueTrendPoints = new();

        [ObservableProperty]
        private int _totalProducts;

        [ObservableProperty]
        private int _ordersToday;

        [ObservableProperty]
        private decimal _todayRevenue;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private string _revenueTrendPathData = "M 0,0";

        [ObservableProperty]
        private string _revenueTrendAreaData = "M 0,0 Z";

        [ObservableProperty]
        private int _revenueAxisMax;

        [ObservableProperty]
        private int _lowStockAlertCount;

        public string TotalProductsDisplay => TotalProducts.ToString(CultureInfo.InvariantCulture);

        public string OrdersTodayDisplay => OrdersToday.ToString(CultureInfo.InvariantCulture);

        public string TodayRevenueDisplay => TodayRevenue.ToString("C", CultureInfo.GetCultureInfo("en-US"));

        public string TotalProductsSubtext => $"{TotalProducts} items in catalog";

        public string OrdersTodaySubtext => $"+{OrdersToday} new today";

        public string TodayRevenueSubtext => "Today's revenue";

        public string RevenueAxisMaxDisplay => RevenueAxisMax.ToString(CultureInfo.InvariantCulture);

        public string RevenueAxisMidDisplay => (RevenueAxisMax / 2).ToString(CultureInfo.InvariantCulture);

        public string InventoryAlertSummary =>
            $"{LowStockAlertCount} high-demand items are reaching critical stock levels. Orders needed for weekend restock.";

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
                BestSellingProducts.Clear();
                RevenueTrendPoints.Clear();
                TotalProducts = 0;
                OrdersToday = 0;
                TodayRevenue = 0m;
                LowStockAlertCount = 0;
                RevenueTrendPathData = "M 0,0";
                RevenueTrendAreaData = "M 0,0 Z";
                RevenueAxisMax = 0;
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

        partial void OnTotalProductsChanged(int value)
        {
            OnPropertyChanged(nameof(TotalProductsDisplay));
            OnPropertyChanged(nameof(TotalProductsSubtext));
        }

        partial void OnOrdersTodayChanged(int value)
        {
            OnPropertyChanged(nameof(OrdersTodayDisplay));
            OnPropertyChanged(nameof(OrdersTodaySubtext));
        }

        partial void OnTodayRevenueChanged(decimal value)
        {
            OnPropertyChanged(nameof(TodayRevenueDisplay));
            OnPropertyChanged(nameof(TodayRevenueSubtext));
        }

        partial void OnRevenueAxisMaxChanged(int value)
        {
            OnPropertyChanged(nameof(RevenueAxisMaxDisplay));
            OnPropertyChanged(nameof(RevenueAxisMidDisplay));
        }

        partial void OnLowStockAlertCountChanged(int value)
        {
            OnPropertyChanged(nameof(InventoryAlertSummary));
        }

        private async Task LoadLowStockAndRecentOrdersAsync()
        {
            const string query = @"query DashboardMain($lowStockTake: Int!, $recentOrdersPageSize: Int!, $bestSellerTake: Int!, $revenueDays: Int!) {
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
                getTopBestSellingProducts(take: $bestSellerTake) {
                    productId
                    productName
                    categoryName
                    retailPrice
                    unitsSold
                    rank
                }
                getRevenueTrend(days: $revenueDays) {
                    dayLabel
                    revenue
                }
            }";

            var variables = new
            {
                lowStockTake = LowStockTake,
                recentOrdersPageSize = RecentOrdersTake,
                bestSellerTake = BestSellerTake,
                revenueDays = RevenueTrendDays
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

            LowStockAlertCount = LowStockProducts.Count(x => x.StockQuantity <= WarningStockThreshold);

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

            BestSellingProducts.Clear();
            foreach (var item in payload.GetTopBestSellingProducts.Take(BestSellerTake))
            {
                BestSellingProducts.Add(new BestSellingProductViewModel
                {
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    CategoryName = item.CategoryName,
                    RetailPrice = item.RetailPrice,
                    UnitsSold = item.UnitsSold,
                    Rank = item.Rank
                });
            }

            RevenueTrendPoints.Clear();
            foreach (var point in payload.GetRevenueTrend)
            {
                RevenueTrendPoints.Add(new RevenueTrendPointViewModel
                {
                    DayLabel = point.DayLabel,
                    Revenue = point.Revenue
                });
            }

            RebuildRevenueGeometry();
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

        private void RebuildRevenueGeometry()
        {
            if (RevenueTrendPoints.Count == 0)
            {
                RevenueTrendPathData = "M 0,0";
                RevenueTrendAreaData = "M 0,0 Z";
                RevenueAxisMax = 0;
                return;
            }

            decimal maxRevenueRaw = RevenueTrendPoints.Max(x => x.Revenue);
            int axisMax = (int)Math.Ceiling((double)Math.Max(maxRevenueRaw, 100m) / 100d) * 100;
            RevenueAxisMax = axisMax;

            double innerWidth = RevenueChartWidth - (RevenueChartPadding * 2d);
            double innerHeight = RevenueChartHeight - (RevenueChartPadding * 2d);
            int count = RevenueTrendPoints.Count;
            double xStep = count > 1 ? innerWidth / (count - 1) : 0;

            var lineBuilder = new StringBuilder();
            var areaBuilder = new StringBuilder();

            for (int i = 0; i < count; i++)
            {
                var item = RevenueTrendPoints[i];
                double x = RevenueChartPadding + (xStep * i);
                double normalized = axisMax == 0 ? 0d : (double)(item.Revenue / axisMax);
                normalized = Math.Clamp(normalized, 0d, 1d);
                double y = RevenueChartPadding + ((1d - normalized) * innerHeight);

                string point = string.Format(CultureInfo.InvariantCulture, "{0:0.##},{1:0.##}", x, y);
                if (i == 0)
                {
                    lineBuilder.Append("M ").Append(point);
                    areaBuilder.Append("M ").Append(point);
                }
                else
                {
                    lineBuilder.Append(" L ").Append(point);
                    areaBuilder.Append(" L ").Append(point);
                }
            }

            double right = RevenueChartPadding + innerWidth;
            double bottom = RevenueChartPadding + innerHeight;
            areaBuilder
                .Append(" L ")
                .Append(string.Format(CultureInfo.InvariantCulture, "{0:0.##},{1:0.##}", right, bottom))
                .Append(" L ")
                .Append(string.Format(CultureInfo.InvariantCulture, "{0:0.##},{1:0.##}", RevenueChartPadding, bottom))
                .Append(" Z");

            RevenueTrendPathData = lineBuilder.ToString();
            RevenueTrendAreaData = areaBuilder.ToString();
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

    public sealed class BestSellingProductViewModel
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public decimal RetailPrice { get; set; }
        public int UnitsSold { get; set; }
        public int Rank { get; set; }

        public string RankDisplay => Rank.ToString(CultureInfo.InvariantCulture);
        public string PriceDisplay => RetailPrice.ToString("C", CultureInfo.GetCultureInfo("en-US"));
        public string SoldDisplay => $"{UnitsSold} Sold";
    }

    public sealed class RevenueTrendPointViewModel
    {
        public string DayLabel { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
    }

    public sealed class DashboardMainQueryData
    {
        public DashboardProductConnection Products { get; set; } = new();
        public DashboardOrderList GetOrders { get; set; } = new();
        public List<DashboardBestSellingProductNode> GetTopBestSellingProducts { get; set; } = new();
        public List<DashboardRevenuePointNode> GetRevenueTrend { get; set; } = new();
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

    public sealed class DashboardBestSellingProductNode
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public decimal RetailPrice { get; set; }
        public int UnitsSold { get; set; }
        public int Rank { get; set; }
    }

    public sealed class DashboardRevenuePointNode
    {
        public string DayLabel { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
    }
}