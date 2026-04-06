using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Enums;
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
        private const int DashboardTodayBatchSize = 50;
        private const int CriticalStockThreshold = 0;
        private const int WarningStockThreshold = 3;

        private const double RevenueChartMinWidth = 760;
        private const double RevenuePointSlotWidth = 40;
        private const double RevenueChartHeight = 220;
        private const double RevenueChartPadding = 12;
        private const double RevenueMarkerHitSize = 18;
        private const double RevenueMarkerHitHalf = RevenueMarkerHitSize / 2d;

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
        private ObservableCollection<RevenueTrendMarkerViewModel> _revenueTrendMarkers = new();

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
        private double _revenueTrendScrollableWidth = RevenueChartMinWidth;

        [ObservableProperty]
        private string _revenuePeriodLabel = string.Empty;

        [ObservableProperty]
        private string _revenuePeriodBadge = "CURRENT MONTH";

        [ObservableProperty]
        private int _lowStockAlertCount;

        [ObservableProperty]
        private string _currencySymbol = "$";

        public string TotalProductsDisplay => TotalProducts.ToString(CultureInfo.InvariantCulture);

        public string OrdersTodayDisplay => OrdersToday.ToString(CultureInfo.InvariantCulture);

        public string TodayRevenueDisplay => FormatCurrency(TodayRevenue);

        public string TotalProductsSubtext => $"{TotalProducts} items in catalog";

        public string OrdersTodaySubtext => $"+{OrdersToday} new today";

        public string TodayRevenueSubtext => "Today's revenue";

        public string RevenueAxisMaxDisplay => RevenueAxisMax.ToString(CultureInfo.InvariantCulture);

        public string RevenueAxisMidDisplay => (RevenueAxisMax / 2).ToString(CultureInfo.InvariantCulture);

        public bool HasLowStockProducts => LowStockProducts.Count > 0;

        public bool IsLowStockProductsEmpty => !HasLowStockProducts;

        public bool HasRecentOrders => RecentOrders.Count > 0;

        public bool IsRecentOrdersEmpty => !HasRecentOrders;

        public bool HasBestSellingProducts => BestSellingProducts.Count > 0;

        public bool IsBestSellingProductsEmpty => !HasBestSellingProducts;

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
                NotifyCollectionStateChanged();
                RevenueTrendPoints.Clear();
                RevenueTrendMarkers.Clear();
                TotalProducts = 0;
                OrdersToday = 0;
                TodayRevenue = 0m;
                LowStockAlertCount = 0;
                RevenueTrendPathData = "M 0,0";
                RevenueTrendAreaData = "M 0,0 Z";
                RevenueAxisMax = 0;
                RevenueTrendScrollableWidth = RevenueChartMinWidth;
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

        partial void OnCurrencySymbolChanged(string value)
        {
            OnPropertyChanged(nameof(TodayRevenueDisplay));

            foreach (var product in LowStockProducts)
            {
                product.CurrencySymbol = value;
            }

            foreach (var order in RecentOrders)
            {
                order.CurrencySymbol = value;
            }

            foreach (var product in BestSellingProducts)
            {
                product.CurrencySymbol = value;
            }

            if (RevenueTrendPoints.Count > 0)
            {
                RebuildRevenueGeometry();
            }
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
            var now = DateTime.Now;
            var revenueFrom = new DateTime(now.Year, now.Month, 1);
            var revenueTo = now.Date;

            RevenuePeriodLabel = $"Daily Revenue ({revenueFrom:MMMM yyyy})";
            RevenuePeriodBadge = "CURRENT MONTH";

            const string query = @"query DashboardMain($lowStockTake: Int!, $recentOrdersTake: Int!, $bestSellerTake: Int!, $revenueFrom: DateTime!, $revenueTo: DateTime!, $revenueGroupBy: ReportGroupBy!) {
                products(first: $lowStockTake, order: { stockQuantity: ASC }) {
                    totalCount
                    nodes {
                        id
                        name
                        stockQuantity
                        retailPrice
                        imageUrl
                        category {
                            name
                        }
                    }
                }
                getOrders: orders(first: $recentOrdersTake) {
                    nodes {
                        id
                        orderDate
                        status
                        totalAmount
                        customer {
                            fullName
                        }
                        orderDetails {
                            quantity
                        }
                    }
                }
                getTopBestSellingProducts: topBestSellingProducts(take: $bestSellerTake) {
                    productId
                    productName
                    categoryName
                    retailPrice
                    unitsSold
                    rank
                    imageUrl
                }
                getReportTimeSeries: reportTimeSeries(startDate: $revenueFrom, endDate: $revenueTo, groupBy: $revenueGroupBy) {
                    periodStart
                    periodLabel
                    totalRevenue
                }
                appConfig {
                    currencySymbol
                }
            }";

            var variables = new
            {
                lowStockTake = LowStockTake,
                recentOrdersTake = RecentOrdersTake,
                bestSellerTake = BestSellerTake,
                revenueFrom = new DateTimeOffset(DateTime.SpecifyKind(revenueFrom, DateTimeKind.Utc)),
                revenueTo = new DateTimeOffset(DateTime.SpecifyKind(revenueTo.AddDays(1).AddTicks(-1), DateTimeKind.Utc)),
                revenueGroupBy = "DAY"
            };

            var payload = await _graphQLClient.ExecuteAsync<DashboardMainQueryData>(query, variables)
                ?? new DashboardMainQueryData();

            CurrencySymbol = string.IsNullOrWhiteSpace(payload.AppConfig?.CurrencySymbol)
                ? "$"
                : payload.AppConfig.CurrencySymbol;

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
                    ImageUrl = product.ImageUrl,
                    CurrencySymbol = CurrencySymbol,
                    IsCritical = product.StockQuantity <= CriticalStockThreshold
                });
            }

            LowStockAlertCount = LowStockProducts.Count(x => x.StockQuantity <= WarningStockThreshold);
            OnPropertyChanged(nameof(HasLowStockProducts));
            OnPropertyChanged(nameof(IsLowStockProductsEmpty));

            TotalProducts = payload.Products.TotalCount;

            RecentOrders.Clear();
            foreach (var order in payload.GetOrders.Nodes.Take(RecentOrdersTake))
            {
                int itemCount = order.OrderDetails.Sum(x => x.Quantity);

                RecentOrders.Add(new RecentOrderViewModel
                {
                    Id = order.Id,
                    Status = order.Status,
                    CustomerName = string.IsNullOrWhiteSpace(order.Customer?.FullName)
                        ? "Walk-in Customer"
                        : order.Customer.FullName,
                    ItemCount = itemCount,
                    TotalAmount = order.TotalAmount,
                    CurrencySymbol = CurrencySymbol,
                    OrderedAt = order.OrderDate
                });
            }

            OnPropertyChanged(nameof(HasRecentOrders));
            OnPropertyChanged(nameof(IsRecentOrdersEmpty));

            BestSellingProducts.Clear();
            foreach (var item in payload.GetTopBestSellingProducts.Take(BestSellerTake))
            {
                BestSellingProducts.Add(new BestSellingProductViewModel
                {
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    CategoryName = item.CategoryName,
                    RetailPrice = item.RetailPrice,
                    CurrencySymbol = CurrencySymbol,
                    UnitsSold = item.UnitsSold,
                    Rank = item.Rank,
                    ImageUrl = item.ImageUrl
                });
            }

            OnPropertyChanged(nameof(HasBestSellingProducts));
            OnPropertyChanged(nameof(IsBestSellingProductsEmpty));

            var monthlySeries = BuildCurrentMonthElapsedSeries(payload.GetReportTimeSeries, revenueFrom, revenueTo);

            RevenueTrendPoints.Clear();
            foreach (var point in monthlySeries)
            {
                RevenueTrendPoints.Add(new RevenueTrendPointViewModel
                {
                    PeriodStart = point.PeriodStart,
                    DayLabel = point.PeriodStart.ToString("MMM dd", CultureInfo.InvariantCulture),
                    Revenue = point.TotalRevenue
                });
            }

            RebuildRevenueGeometry();
        }

        private async Task LoadTodaySummaryAsync()
        {
            var todayStart = DateTime.Today;
            var todayEnd = todayStart.AddDays(1).AddTicks(-1);

            const string todayQuery = @"query DashboardToday($fromDate: DateTime, $toDate: DateTime, $first: Int!, $after: String) {
                getOrders: orders(first: $first, after: $after, fromDate: $fromDate, toDate: $toDate) {
                    totalCount
                    pageInfo {
                        hasNextPage
                        endCursor
                    }
                    nodes {
                        status
                        totalAmount
                    }
                }
            }";

            var allTodayOrders = new List<DashboardOrderNode>();
            int? totalCount = null;
            string? afterCursor = null;

            while (true)
            {
                var variables = new
                {
                    fromDate = todayStart,
                    toDate = todayEnd,
                    first = DashboardTodayBatchSize,
                    after = afterCursor
                };

                var payload = await _graphQLClient.ExecuteAsync<DashboardTodayQueryData>(todayQuery, variables)
                    ?? new DashboardTodayQueryData();

                totalCount ??= payload.GetOrders.TotalCount;
                allTodayOrders.AddRange(payload.GetOrders.Nodes);

                if (payload.GetOrders.PageInfo?.HasNextPage == true
                    && !string.IsNullOrWhiteSpace(payload.GetOrders.PageInfo.EndCursor))
                {
                    afterCursor = payload.GetOrders.PageInfo.EndCursor;
                    continue;
                }

                break;
            }

            OrdersToday = totalCount.GetValueOrDefault() > 0
                ? totalCount.Value
                : allTodayOrders.Count;
            TodayRevenue = allTodayOrders
                .Where(x => string.Equals(x.Status, OrderStatus.Paid.ToString(), StringComparison.OrdinalIgnoreCase))
                .Sum(x => x.TotalAmount);
        }

        private void RebuildRevenueGeometry()
        {
            if (RevenueTrendPoints.Count == 0)
            {
                RevenueTrendPathData = "M 0,0";
                RevenueTrendAreaData = "M 0,0 Z";
                RevenueAxisMax = 0;
                RevenueTrendScrollableWidth = RevenueChartMinWidth;
                RevenueTrendMarkers.Clear();
                return;
            }

            decimal maxRevenueRaw = RevenueTrendPoints.Max(x => x.Revenue);
            int axisMax = (int)Math.Ceiling((double)Math.Max(maxRevenueRaw, 100m) / 100d) * 100;
            RevenueAxisMax = axisMax;

            double chartWidth = Math.Max(RevenueChartMinWidth, RevenueTrendPoints.Count * RevenuePointSlotWidth);
            RevenueTrendScrollableWidth = chartWidth;

            double innerWidth = chartWidth - (RevenueChartPadding * 2d);
            double innerHeight = RevenueChartHeight - (RevenueChartPadding * 2d);
            int count = RevenueTrendPoints.Count;
            double xStep = count > 1 ? innerWidth / (count - 1) : 0;

            var lineBuilder = new StringBuilder();
            var areaBuilder = new StringBuilder();
            RevenueTrendMarkers.Clear();

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

                RevenueTrendMarkers.Add(new RevenueTrendMarkerViewModel
                {
                    X = x - RevenueMarkerHitHalf,
                    Y = y - RevenueMarkerHitHalf,
                    Tooltip = string.Concat(item.DayLabel, ": ", FormatCurrency(item.Revenue))
                });
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

        private string FormatCurrency(decimal amount)
        {
            var number = amount.ToString("N2", CultureInfo.GetCultureInfo("en-US"));
            var spacing = CurrencySymbol.Length == 1 ? string.Empty : " ";
            return string.Concat(CurrencySymbol, spacing, number);
        }

        private void NotifyCollectionStateChanged()
        {
            OnPropertyChanged(nameof(HasLowStockProducts));
            OnPropertyChanged(nameof(IsLowStockProductsEmpty));
            OnPropertyChanged(nameof(HasRecentOrders));
            OnPropertyChanged(nameof(IsRecentOrdersEmpty));
            OnPropertyChanged(nameof(HasBestSellingProducts));
            OnPropertyChanged(nameof(IsBestSellingProductsEmpty));
        }

        private static List<DashboardReportTimeSeriesNode> BuildCurrentMonthElapsedSeries(
            IEnumerable<DashboardReportTimeSeriesNode> rawSeries,
            DateTime monthStart,
            DateTime monthEnd)
        {
            var start = monthStart.Date;
            var end = monthEnd.Date;
            if (end < start)
            {
                return [];
            }

            var revenueByDate = rawSeries
                .Where(x => x.PeriodStart.Date >= start && x.PeriodStart.Date <= end)
                .GroupBy(x => x.PeriodStart.Date)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.TotalRevenue));

            int totalDays = (end - start).Days + 1;
            var normalized = new List<DashboardReportTimeSeriesNode>(totalDays);

            for (int i = 0; i < totalDays; i++)
            {
                var day = start.AddDays(i);
                revenueByDate.TryGetValue(day, out var totalRevenue);

                normalized.Add(new DashboardReportTimeSeriesNode
                {
                    PeriodStart = day,
                    PeriodLabel = day.ToString("dd MMM", CultureInfo.InvariantCulture),
                    TotalRevenue = totalRevenue
                });
            }

            return normalized;
        }
    }

    public sealed class LowStockProductViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public int StockQuantity { get; set; }
        public decimal RetailPrice { get; set; }
        public string? ImageUrl { get; set; }
        public string CurrencySymbol { get; set; } = "$";
        public bool IsCritical { get; set; }

        public string PriceDisplay => FormatCurrency(RetailPrice, CurrencySymbol);

        private static string FormatCurrency(decimal amount, string currencySymbol)
        {
            var number = amount.ToString("N2", CultureInfo.GetCultureInfo("en-US"));
            var spacing = currencySymbol.Length == 1 ? string.Empty : " ";
            return string.Concat(currencySymbol, spacing, number);
        }
    }

    public sealed class RecentOrderViewModel
    {
        public int Id { get; set; }
        public string Status { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public int ItemCount { get; set; }
        public decimal TotalAmount { get; set; }
        public string CurrencySymbol { get; set; } = "$";
        public DateTime OrderedAt { get; set; }

        public string OrderIdDisplay => $"#{Id}";

        public string TotalAmountDisplay => FormatCurrency(TotalAmount, CurrencySymbol);

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

        private static string FormatCurrency(decimal amount, string currencySymbol)
        {
            var number = amount.ToString("N2", CultureInfo.GetCultureInfo("en-US"));
            var spacing = currencySymbol.Length == 1 ? string.Empty : " ";
            return string.Concat(currencySymbol, spacing, number);
        }
    }

    public sealed class BestSellingProductViewModel
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public decimal RetailPrice { get; set; }
        public string? ImageUrl { get; set; }
        public string CurrencySymbol { get; set; } = "$";
        public int UnitsSold { get; set; }
        public int Rank { get; set; }

        public string RankDisplay => Rank.ToString(CultureInfo.InvariantCulture);
        public string PriceDisplay => FormatCurrency(RetailPrice, CurrencySymbol);
        public string SoldDisplay => $"{UnitsSold} Sold";

        private static string FormatCurrency(decimal amount, string currencySymbol)
        {
            var number = amount.ToString("N2", CultureInfo.GetCultureInfo("en-US"));
            var spacing = currencySymbol.Length == 1 ? string.Empty : " ";
            return string.Concat(currencySymbol, spacing, number);
        }
    }

    public sealed class RevenueTrendPointViewModel
    {
        public DateTime PeriodStart { get; set; }
        public string DayLabel { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
    }

    public sealed class RevenueTrendMarkerViewModel
    {
        public double X { get; set; }
        public double Y { get; set; }
        public string Tooltip { get; set; } = string.Empty;
    }

    public sealed class DashboardMainQueryData
    {
        public DashboardProductConnection Products { get; set; } = new();
        public DashboardOrderConnection GetOrders { get; set; } = new();
        public List<DashboardBestSellingProductNode> GetTopBestSellingProducts { get; set; } = new();
        public List<DashboardReportTimeSeriesNode> GetReportTimeSeries { get; set; } = new();
        public DashboardAppConfigNode? AppConfig { get; set; }
    }

    public sealed class DashboardAppConfigNode
    {
        public string CurrencySymbol { get; set; } = "$";
    }

    public sealed class DashboardTodayQueryData
    {
        public DashboardOrderConnection GetOrders { get; set; } = new();
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
        public string? ImageUrl { get; set; }
        public DashboardCategoryNode? Category { get; set; }
    }

    public sealed class DashboardCategoryNode
    {
        public string Name { get; set; } = string.Empty;
    }

    public sealed class DashboardOrderConnection
    {
        public int TotalCount { get; set; }
        public DashboardOrderPageInfo? PageInfo { get; set; }
        public List<DashboardOrderNode> Nodes { get; set; } = new();
    }

    public sealed class DashboardOrderPageInfo
    {
        public bool HasNextPage { get; set; }
        public string? EndCursor { get; set; }
    }

    public sealed class DashboardOrderNode
    {
        public int Id { get; set; }
        public DateTime OrderDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public DashboardOrderCustomerNode? Customer { get; set; }
        public List<DashboardOrderDetailNode> OrderDetails { get; set; } = new();
    }

    public sealed class DashboardOrderCustomerNode
    {
        public string? FullName { get; set; }
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
        public string? ImageUrl { get; set; }
    }

    public sealed class DashboardReportTimeSeriesNode
    {
        public DateTime PeriodStart { get; set; }
        public string PeriodLabel { get; set; } = string.Empty;
        public decimal TotalRevenue { get; set; }
    }
}