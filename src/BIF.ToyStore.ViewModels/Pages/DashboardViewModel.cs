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
        private ObservableCollection<QuickRestockProductViewModel> _quickRestockItems = new();

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

        [ObservableProperty]
        private bool _isQuickRestockPanelOpen;

        [ObservableProperty]
        private string _quickRestockErrorMessage = string.Empty;

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

        public bool HasQuickRestockItems => QuickRestockItems.Count > 0;

        public bool IsQuickRestockItemsEmpty => !HasQuickRestockItems;

        public bool HasQuickRestockErrorMessage => !string.IsNullOrWhiteSpace(QuickRestockErrorMessage);

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
                QuickRestockItems.Clear();
                RecentOrders.Clear();
                BestSellingProducts.Clear();
                NotifyCollectionStateChanged();
                NotifyQuickRestockStateChanged();
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

        [RelayCommand]
        private void OpenQuickRestockPanel()
        {
            QuickRestockErrorMessage = string.Empty;
            QuickRestockItems.Clear();

            foreach (var product in LowStockProducts.OrderBy(x => x.StockQuantity).ThenBy(x => x.Name))
            {
                QuickRestockItems.Add(new QuickRestockProductViewModel
                {
                    Id = product.Id,
                    Name = product.Name,
                    CategoryId = product.CategoryId,
                    CategoryName = product.CategoryName,
                    RetailPrice = product.RetailPrice,
                    ImportPrice = product.ImportPrice,
                    Images = product.Images ?? new List<DashboardProductImageNode>(),
                    ImageUrl = product.ImageUrl,
                    CurrencySymbol = CurrencySymbol,
                    OriginalStockQuantity = product.StockQuantity,
                    StockQuantity = product.StockQuantity
                });
            }

            NotifyQuickRestockStateChanged();
            IsQuickRestockPanelOpen = true;
        }

        [RelayCommand]
        private void CancelQuickRestockPanel()
        {
            QuickRestockErrorMessage = string.Empty;
            IsQuickRestockPanelOpen = false;
            QuickRestockItems.Clear();
            NotifyQuickRestockStateChanged();
        }

        [RelayCommand]
        private async Task SaveQuickRestockAsync()
        {
            if (QuickRestockItems.Count == 0)
            {
                IsQuickRestockPanelOpen = false;
                return;
            }

            var invalidItem = QuickRestockItems.FirstOrDefault(x => x.StockQuantity < 0);
            if (invalidItem is not null)
            {
                QuickRestockErrorMessage = $"{invalidItem.Name} cannot have negative stock.";
                return;
            }

            var changedItems = QuickRestockItems
                .Where(x => x.StockQuantity != x.OriginalStockQuantity)
                .ToList();

            if (changedItems.Count == 0)
            {
                IsQuickRestockPanelOpen = false;
                return;
            }

            try
            {
                IsBusy = true;
                QuickRestockErrorMessage = string.Empty;

                foreach (var item in changedItems)
                {
                    await UpdateQuickRestockItemAsync(item);
                }

                IsQuickRestockPanelOpen = false;
                QuickRestockItems.Clear();
                NotifyQuickRestockStateChanged();
                await LoadLowStockAndRecentOrdersAsync();
            }
            catch (Exception ex)
            {
                QuickRestockErrorMessage = "Unable to update stock: " + ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
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

            foreach (var product in QuickRestockItems)
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

        partial void OnQuickRestockErrorMessageChanged(string value)
        {
            OnPropertyChanged(nameof(HasQuickRestockErrorMessage));
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
                        categoryId
                        stockQuantity
                        retailPrice
                        importPrice
                        images {
                            imageUrl
                            displayOrder
                            isPrimary
                        }
                        category {
                            id
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
                var imageUrl = ResolvePrimaryImageUrl(product);

                LowStockProducts.Add(new LowStockProductViewModel
                {
                    Id = product.Id,
                    Name = product.Name,
                    CategoryId = product.CategoryId,
                    CategoryName = product.Category?.Name ?? "Unknown",
                    StockQuantity = product.StockQuantity,
                    RetailPrice = product.RetailPrice,
                    ImportPrice = product.ImportPrice,
                    Images = product.Images ?? new List<DashboardProductImageNode>(),
                    ImageUrl = imageUrl,
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
            const string summaryQuery = @"query DashboardTodaySummary {
                dashboardTodaySummary {
                    orderCount
                    revenue
                }
            }";

            var payload = await _graphQLClient.ExecuteAsync<DashboardTodaySummaryNode>(
                summaryQuery,
                dataKey: "dashboardTodaySummary");

            OrdersToday = payload?.OrderCount ?? 0;
            TodayRevenue = payload?.Revenue ?? 0m;
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

            double innerHeight = RevenueChartHeight - (RevenueChartPadding * 2d);
            int count = RevenueTrendPoints.Count;
            double pointSlotWidth = chartWidth / count;

            var lineBuilder = new StringBuilder();
            var areaBuilder = new StringBuilder();
            RevenueTrendMarkers.Clear();

            for (int i = 0; i < count; i++)
            {
                var item = RevenueTrendPoints[i];
                item.LabelWidth = pointSlotWidth;

                double x = (pointSlotWidth / 2d) + (pointSlotWidth * i);
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

            double left = pointSlotWidth / 2d;
            double right = left + (pointSlotWidth * (count - 1));
            double bottom = RevenueChartPadding + innerHeight;
            areaBuilder
                .Append(" L ")
                .Append(string.Format(CultureInfo.InvariantCulture, "{0:0.##},{1:0.##}", right, bottom))
                .Append(" L ")
                .Append(string.Format(CultureInfo.InvariantCulture, "{0:0.##},{1:0.##}", left, bottom))
                .Append(" Z");

            RevenueTrendPathData = lineBuilder.ToString();
            RevenueTrendAreaData = areaBuilder.ToString();
        }

        private async Task UpdateQuickRestockItemAsync(QuickRestockProductViewModel item)
        {
            const string mutation = @"mutation QuickRestock($input: UpdateProductInput!) {
                updateProduct(input: $input) {
                    id
                    name
                    categoryId
                    retailPrice
                    importPrice
                    stockQuantity
                    imageUrl
                    category {
                        id
                        name
                    }
                }
            }";

            IEnumerable<object> images = item.Images is { Count: > 0 }
                ? item.Images.Select(i => (object)new
                {
                    imageUrl = i.ImageUrl,
                    displayOrder = i.DisplayOrder,
                    isPrimary = i.IsPrimary
                })
                : !string.IsNullOrWhiteSpace(item.ImageUrl)
                    ? new object[]
                    {
                        new
                        {
                            imageUrl = item.ImageUrl,
                            displayOrder = 0,
                            isPrimary = true
                        }
                    }
                    : Array.Empty<object>();

            var input = new
            {
                id = item.Id,
                name = item.Name,
                categoryId = item.CategoryId,
                retailPrice = item.RetailPrice,
                importPrice = item.ImportPrice,
                stockQuantity = item.StockQuantity,
                images
            };

            _ = await _graphQLClient.ExecuteAsync<DashboardProductNode>(
                mutation,
                new { input },
                dataKey: "updateProduct")
                ?? throw new InvalidOperationException($"Failed to update stock for {item.Name}.");
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

        private void NotifyQuickRestockStateChanged()
        {
            OnPropertyChanged(nameof(HasQuickRestockItems));
            OnPropertyChanged(nameof(IsQuickRestockItemsEmpty));
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

        private static string? ResolvePrimaryImageUrl(DashboardProductNode product)
        {
            if (product.Images is { Count: > 0 })
            {
                return product.Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl
                    ?? product.Images.FirstOrDefault()?.ImageUrl;
            }

            return product.ImageUrl;
        }
    }

    public sealed class LowStockProductViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public int StockQuantity { get; set; }
        public decimal RetailPrice { get; set; }
        public decimal ImportPrice { get; set; }
        public List<DashboardProductImageNode> Images { get; set; } = new();
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

    public sealed partial class RevenueTrendPointViewModel : ObservableObject
    {
        [ObservableProperty]
        private DateTime _periodStart;

        [ObservableProperty]
        private string _dayLabel = string.Empty;

        [ObservableProperty]
        private decimal _revenue;

        [ObservableProperty]
        private double _labelWidth = 40d;
    }

    public sealed class RevenueTrendMarkerViewModel
    {
        public double X { get; set; }
        public double Y { get; set; }
        public string Tooltip { get; set; } = string.Empty;
    }

    public sealed partial class QuickRestockProductViewModel : ObservableObject
    {
        [ObservableProperty]
        private int _id;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private int _categoryId;

        [ObservableProperty]
        private string _categoryName = string.Empty;

        [ObservableProperty]
        private decimal _retailPrice;

        [ObservableProperty]
        private decimal _importPrice;

        [ObservableProperty]
        private List<DashboardProductImageNode> _images = new();

        [ObservableProperty]
        private string? _imageUrl;

        [ObservableProperty]
        private string _currencySymbol = "$";

        [ObservableProperty]
        private int _originalStockQuantity;

        [ObservableProperty]
        private int _stockQuantity;

        public string PriceDisplay => FormatCurrency(RetailPrice, CurrencySymbol);

        public string OriginalStockDisplay => $"Current: {OriginalStockQuantity}";

        public string StockDeltaDisplay
        {
            get
            {
                var delta = StockQuantity - OriginalStockQuantity;
                return delta switch
                {
                    > 0 => $"+{delta}",
                    < 0 => delta.ToString(CultureInfo.InvariantCulture),
                    _ => "No change"
                };
            }
        }

        partial void OnRetailPriceChanged(decimal value)
        {
            OnPropertyChanged(nameof(PriceDisplay));
        }

        partial void OnCurrencySymbolChanged(string value)
        {
            OnPropertyChanged(nameof(PriceDisplay));
        }

        partial void OnOriginalStockQuantityChanged(int value)
        {
            OnPropertyChanged(nameof(OriginalStockDisplay));
            OnPropertyChanged(nameof(StockDeltaDisplay));
        }

        partial void OnStockQuantityChanged(int value)
        {
            OnPropertyChanged(nameof(StockDeltaDisplay));
        }

        private static string FormatCurrency(decimal amount, string currencySymbol)
        {
            var number = amount.ToString("N2", CultureInfo.GetCultureInfo("en-US"));
            var spacing = currencySymbol.Length == 1 ? string.Empty : " ";
            return string.Concat(currencySymbol, spacing, number);
        }
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

    public sealed class DashboardTodaySummaryNode
    {
        public int OrderCount { get; set; }
        public decimal Revenue { get; set; }
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
        public int CategoryId { get; set; }
        public int StockQuantity { get; set; }
        public decimal RetailPrice { get; set; }
        public decimal ImportPrice { get; set; }
        public string? ImageUrl { get; set; }
        public List<DashboardProductImageNode> Images { get; set; } = new();
        public DashboardCategoryNode? Category { get; set; }
    }

    public sealed class DashboardProductImageNode
    {
        public string ImageUrl { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public bool IsPrimary { get; set; }
    }

    public sealed class DashboardCategoryNode
    {
        public int Id { get; set; }
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
