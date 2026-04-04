using BIF.ToyStore.Core.Enums;
using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.ViewModels.Base;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace BIF.ToyStore.ViewModels.Pages
{
    public partial class ReportsViewModel : BaseViewModel
    {
        private const double TrendChartWidth = 980;
        private const double TrendChartHeight = 260;
        private const double TrendChartPadding = 12;
        private const double RevenueProfitBarMaxHeight = 140;
        private const int TopProductsTake = 8;

        private readonly IGraphQLClient _graphQLClient;

        [ObservableProperty]
        private ObservableCollection<ReportTimePointViewModel> _timeSeriesPoints = new();

        [ObservableProperty]
        private ObservableCollection<ReportTopProductViewModel> _topProducts = new();

        [ObservableProperty]
        private DateTimeOffset? _fromDate = DateTimeOffset.Now.AddDays(-30);

        [ObservableProperty]
        private DateTimeOffset? _toDate = DateTimeOffset.Now;

        [ObservableProperty]
        private ReportGroupByOption? _selectedGroupBy;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private string _trendPathData = "M 0,0";

        [ObservableProperty]
        private string _trendAreaData = "M 0,0 Z";

        [ObservableProperty]
        private int _trendAxisMax;

        [ObservableProperty]
        private string _currencySymbol = "$";

        [ObservableProperty]
        private ReportGroupBy _effectiveGroupBy = ReportGroupBy.Day;

        public ObservableCollection<ReportGroupByOption> GroupByOptions { get; } =
        [
            new ReportGroupByOption("Day", ReportGroupBy.Day),
            new ReportGroupByOption("Week", ReportGroupBy.Week),
            new ReportGroupByOption("Month", ReportGroupBy.Month),
            new ReportGroupByOption("Year", ReportGroupBy.Year)
        ];

        public bool HasTimeSeriesData => TimeSeriesPoints.Count > 0;

        public bool IsTimeSeriesEmpty => !HasTimeSeriesData;

        public bool HasTopProducts => TopProducts.Count > 0;

        public bool IsTopProductsEmpty => !HasTopProducts;

        public string TrendAxisMaxDisplay => TrendAxisMax.ToString(CultureInfo.InvariantCulture);

        public string TrendAxisMidDisplay => (TrendAxisMax / 2).ToString(CultureInfo.InvariantCulture);

        public string EffectiveGroupByDisplay => EffectiveGroupBy.ToString();

        public int TotalItemsSold => TimeSeriesPoints.Sum(x => x.Quantity);

        public decimal TotalRevenue => TimeSeriesPoints.Sum(x => x.Revenue);

        public decimal TotalProfit => TimeSeriesPoints.Sum(x => x.Profit);

        public string TotalItemsSoldDisplay => TotalItemsSold.ToString("N0", CultureInfo.InvariantCulture);

        public string TotalRevenueDisplay => FormatCurrency(TotalRevenue, CurrencySymbol);

        public string TotalProfitDisplay => FormatCurrency(TotalProfit, CurrencySymbol);

        public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

        public ReportsViewModel(IGraphQLClient graphQLClient)
        {
            _graphQLClient = graphQLClient;
            Title = "Reports";
            SelectedGroupBy = GroupByOptions.FirstOrDefault(x => x.Value == ReportGroupBy.Day);
            EffectiveGroupBy = ReportGroupBy.Day;
        }

        public async Task LoadAsync()
        {
            await ApplyFiltersAsync();
        }

        [RelayCommand]
        private async Task ApplyFiltersAsync()
        {
            try
            {
                IsBusy = true;
                ErrorMessage = string.Empty;
                StatusMessage = string.Empty;

                var from = (FromDate ?? DateTimeOffset.Now.AddDays(-30)).Date;
                var to = (ToDate ?? DateTimeOffset.Now).Date;
                if (to < from)
                {
                    (from, to) = (to, from);
                    FromDate = from;
                    ToDate = to;
                }

                var selectedGroupBy = SelectedGroupBy?.Value ?? ReportGroupBy.Day;
                var effectiveGroupBy = GetEffectiveGroupBy(selectedGroupBy, from, to);
                EffectiveGroupBy = effectiveGroupBy;

                if (effectiveGroupBy != selectedGroupBy)
                {
                    StatusMessage = $"Grouping automatically adjusted to {effectiveGroupBy} for better chart performance.";
                }

                const string query = @"query ReportsData($startDate: DateTime!, $endDate: DateTime!, $groupBy: ReportGroupBy!, $take: Int!) {
                    getReportTimeSeries: reportTimeSeries(startDate: $startDate, endDate: $endDate, groupBy: $groupBy) {
                        periodStart
                        periodLabel
                        totalQuantity
                        totalRevenue
                        totalProfit
                    }
                    getReportTopProducts: reportTopProducts(startDate: $startDate, endDate: $endDate, take: $take) {
                        productId
                        productName
                        categoryName
                        totalQuantity
                        totalRevenue
                        totalProfit
                        rank
                    }
                    appConfig {
                        currencySymbol
                    }
                }";

                var variables = new
                {
                    startDate = new DateTimeOffset(DateTime.SpecifyKind(from, DateTimeKind.Utc)),
                    endDate = new DateTimeOffset(DateTime.SpecifyKind(to.AddDays(1).AddTicks(-1), DateTimeKind.Utc)),
                    groupBy = ToGraphQlGroupByValue(effectiveGroupBy),
                    take = TopProductsTake
                };

                var payload = await _graphQLClient.ExecuteAsync<ReportsQueryData>(query, variables)
                    ?? new ReportsQueryData();

                CurrencySymbol = string.IsNullOrWhiteSpace(payload.AppConfig?.CurrencySymbol)
                    ? "$"
                    : payload.AppConfig.CurrencySymbol;

                TimeSeriesPoints.Clear();
                decimal maxSeriesRevenue = payload.GetReportTimeSeries.Count == 0
                    ? 0m
                    : payload.GetReportTimeSeries.Max(x => x.TotalRevenue);
                decimal maxSeriesProfit = payload.GetReportTimeSeries.Count == 0
                    ? 0m
                    : payload.GetReportTimeSeries.Max(x => x.TotalProfit);

                foreach (var point in payload.GetReportTimeSeries)
                {
                    var revenueBarHeight = maxSeriesRevenue <= 0m
                        ? 0d
                        : Math.Clamp((double)(point.TotalRevenue / maxSeriesRevenue) * RevenueProfitBarMaxHeight, 0d, RevenueProfitBarMaxHeight);

                    var profitBarHeight = maxSeriesProfit <= 0m
                        ? 0d
                        : Math.Clamp((double)(point.TotalProfit / maxSeriesProfit) * RevenueProfitBarMaxHeight, 0d, RevenueProfitBarMaxHeight);

                    TimeSeriesPoints.Add(new ReportTimePointViewModel
                    {
                        PeriodStart = point.PeriodStart,
                        Label = point.PeriodLabel,
                        Quantity = point.TotalQuantity,
                        Revenue = point.TotalRevenue,
                        Profit = point.TotalProfit,
                        RevenueBarHeight = revenueBarHeight,
                        ProfitBarHeight = profitBarHeight,
                        CurrencySymbol = CurrencySymbol
                    });
                }

                RebuildTrendGeometry();
                OnPropertyChanged(nameof(HasTimeSeriesData));
                OnPropertyChanged(nameof(IsTimeSeriesEmpty));

                TopProducts.Clear();
                decimal maxRevenue = payload.GetReportTopProducts.Count == 0
                    ? 0m
                    : payload.GetReportTopProducts.Max(x => x.TotalRevenue);
                decimal maxProfit = payload.GetReportTopProducts.Count == 0
                    ? 0m
                    : payload.GetReportTopProducts.Max(x => x.TotalProfit);

                foreach (var product in payload.GetReportTopProducts)
                {
                    var revenueFill = maxRevenue <= 0m
                        ? 0d
                        : Math.Clamp((double)(product.TotalRevenue / maxRevenue) * 100d, 0d, 100d);

                    var profitFill = maxProfit <= 0m
                        ? 0d
                        : Math.Clamp((double)(product.TotalProfit / maxProfit) * 100d, 0d, 100d);

                    TopProducts.Add(new ReportTopProductViewModel
                    {
                        ProductId = product.ProductId,
                        ProductName = product.ProductName,
                        CategoryName = product.CategoryName,
                        TotalQuantity = product.TotalQuantity,
                        TotalRevenue = product.TotalRevenue,
                        TotalProfit = product.TotalProfit,
                        Rank = product.Rank,
                        BarFillPercent = revenueFill,
                        ProfitBarPercent = profitFill,
                        CurrencySymbol = CurrencySymbol
                    });
                }

                OnPropertyChanged(nameof(HasTopProducts));
                OnPropertyChanged(nameof(IsTopProductsEmpty));
                OnPropertyChanged(nameof(TotalItemsSold));
                OnPropertyChanged(nameof(TotalRevenue));
                OnPropertyChanged(nameof(TotalProfit));
                OnPropertyChanged(nameof(TotalItemsSoldDisplay));
                OnPropertyChanged(nameof(TotalRevenueDisplay));
                OnPropertyChanged(nameof(TotalProfitDisplay));
            }
            catch (Exception ex)
            {
                ErrorMessage = BuildFriendlyErrorMessage(ex);
                TimeSeriesPoints.Clear();
                TopProducts.Clear();
                TrendPathData = "M 0,0";
                TrendAreaData = "M 0,0 Z";
                TrendAxisMax = 0;
                OnPropertyChanged(nameof(HasTimeSeriesData));
                OnPropertyChanged(nameof(IsTimeSeriesEmpty));
                OnPropertyChanged(nameof(HasTopProducts));
                OnPropertyChanged(nameof(IsTopProductsEmpty));
                OnPropertyChanged(nameof(TotalItemsSold));
                OnPropertyChanged(nameof(TotalRevenue));
                OnPropertyChanged(nameof(TotalProfit));
                OnPropertyChanged(nameof(TotalItemsSoldDisplay));
                OnPropertyChanged(nameof(TotalRevenueDisplay));
                OnPropertyChanged(nameof(TotalProfitDisplay));
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void RebuildTrendGeometry()
        {
            if (TimeSeriesPoints.Count == 0)
            {
                TrendPathData = "M 0,0";
                TrendAreaData = "M 0,0 Z";
                TrendAxisMax = 0;
                return;
            }

            int maxQuantityRaw = TimeSeriesPoints.Max(x => x.Quantity);
            int axisMax = Math.Max(10, (int)Math.Ceiling(maxQuantityRaw / 10d) * 10);
            TrendAxisMax = axisMax;

            double innerWidth = TrendChartWidth - (TrendChartPadding * 2d);
            double innerHeight = TrendChartHeight - (TrendChartPadding * 2d);
            int count = TimeSeriesPoints.Count;
            double xStep = count > 1 ? innerWidth / (count - 1) : 0;

            var lineBuilder = new StringBuilder();
            var areaBuilder = new StringBuilder();

            for (int i = 0; i < count; i++)
            {
                var item = TimeSeriesPoints[i];
                double x = TrendChartPadding + (xStep * i);
                double normalized = axisMax == 0 ? 0d : (double)item.Quantity / axisMax;
                normalized = Math.Clamp(normalized, 0d, 1d);
                double y = TrendChartPadding + ((1d - normalized) * innerHeight);

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

            double right = TrendChartPadding + innerWidth;
            double bottom = TrendChartPadding + innerHeight;
            areaBuilder
                .Append(" L ")
                .Append(string.Format(CultureInfo.InvariantCulture, "{0:0.##},{1:0.##}", right, bottom))
                .Append(" L ")
                .Append(string.Format(CultureInfo.InvariantCulture, "{0:0.##},{1:0.##}", TrendChartPadding, bottom))
                .Append(" Z");

            TrendPathData = lineBuilder.ToString();
            TrendAreaData = areaBuilder.ToString();
        }

        private static ReportGroupBy GetEffectiveGroupBy(ReportGroupBy requested, DateTime from, DateTime to)
        {
            int days = Math.Max(1, (to.Date - from.Date).Days + 1);

            if (requested == ReportGroupBy.Day && days > 120)
            {
                return ReportGroupBy.Month;
            }

            if (requested == ReportGroupBy.Week && days > 730)
            {
                return ReportGroupBy.Month;
            }

            if (requested == ReportGroupBy.Month && days > 3650)
            {
                return ReportGroupBy.Year;
            }

            return requested;
        }

        private static string ToGraphQlGroupByValue(ReportGroupBy groupBy)
        {
            return groupBy switch
            {
                ReportGroupBy.Day => "DAY",
                ReportGroupBy.Week => "WEEK",
                ReportGroupBy.Month => "MONTH",
                ReportGroupBy.Year => "YEAR",
                _ => "DAY"
            };
        }

        partial void OnTrendAxisMaxChanged(int value)
        {
            OnPropertyChanged(nameof(TrendAxisMaxDisplay));
            OnPropertyChanged(nameof(TrendAxisMidDisplay));
        }

        partial void OnEffectiveGroupByChanged(ReportGroupBy value)
        {
            OnPropertyChanged(nameof(EffectiveGroupByDisplay));
        }

        partial void OnErrorMessageChanged(string value)
        {
            OnPropertyChanged(nameof(HasError));
        }

        partial void OnCurrencySymbolChanged(string value)
        {
            OnPropertyChanged(nameof(TotalRevenueDisplay));
            OnPropertyChanged(nameof(TotalProfitDisplay));
        }

        private static string BuildFriendlyErrorMessage(Exception ex)
        {
            if (ex is HttpRequestException httpEx)
            {
                const string prefix = "HTTP 400 Error:";
                if (httpEx.Message.Contains(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    var detail = httpEx.Message[(httpEx.Message.IndexOf(prefix, StringComparison.OrdinalIgnoreCase) + prefix.Length)..].Trim();
                    if (!string.IsNullOrWhiteSpace(detail))
                    {
                        return $"Unable to load reports: {detail}";
                    }

                    return "Unable to load reports: The server rejected the report query.";
                }

                return "Unable to load reports: Could not reach the server.";
            }

            return "Unable to load reports: " + ex.Message;
        }

        private static string FormatCurrency(decimal amount, string currencySymbol)
        {
            var number = amount.ToString("N2", CultureInfo.GetCultureInfo("en-US"));
            var spacing = currencySymbol.Length == 1 ? string.Empty : " ";
            return string.Concat(currencySymbol, spacing, number);
        }
    }

    public sealed class ReportGroupByOption
    {
        public string Label { get; }
        public ReportGroupBy Value { get; }

        public ReportGroupByOption(string label, ReportGroupBy value)
        {
            Label = label;
            Value = value;
        }
    }

    public sealed class ReportTimePointViewModel
    {
        public DateTime PeriodStart { get; set; }
        public string Label { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Revenue { get; set; }
        public decimal Profit { get; set; }
        public double RevenueBarHeight { get; set; }
        public double ProfitBarHeight { get; set; }
        public string CurrencySymbol { get; set; } = "$";

        public string QuantityDisplay => Quantity.ToString(CultureInfo.InvariantCulture);

        public string RevenueDisplay => FormatCurrency(Revenue, CurrencySymbol);

        public string ProfitDisplay => FormatCurrency(Profit, CurrencySymbol);

        public string TooltipText => $"{Label}: {QuantityDisplay} units\nRevenue: {RevenueDisplay}\nProfit: {ProfitDisplay}";

        private static string FormatCurrency(decimal amount, string currencySymbol)
        {
            var number = amount.ToString("N2", CultureInfo.GetCultureInfo("en-US"));
            var spacing = currencySymbol.Length == 1 ? string.Empty : " ";
            return string.Concat(currencySymbol, spacing, number);
        }
    }

    public sealed class ReportTopProductViewModel
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public int TotalQuantity { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalProfit { get; set; }
        public int Rank { get; set; }
        public double BarFillPercent { get; set; }
        public double ProfitBarPercent { get; set; }
        public string CurrencySymbol { get; set; } = "$";

        public string RankDisplay => Rank.ToString(CultureInfo.InvariantCulture);
        public string QuantityDisplay => $"{TotalQuantity} units";
        public string RevenueDisplay => FormatCurrency(TotalRevenue, CurrencySymbol);
        public string ProfitDisplay => FormatCurrency(TotalProfit, CurrencySymbol);
        public string TooltipText => $"{ProductName}\n{QuantityDisplay}\nRevenue: {RevenueDisplay}\nProfit: {ProfitDisplay}";

        private static string FormatCurrency(decimal amount, string currencySymbol)
        {
            var number = amount.ToString("N2", CultureInfo.GetCultureInfo("en-US"));
            var spacing = currencySymbol.Length == 1 ? string.Empty : " ";
            return string.Concat(currencySymbol, spacing, number);
        }
    }

    public sealed class ReportsQueryData
    {
        public List<ReportTimeSeriesNode> GetReportTimeSeries { get; set; } = [];
        public List<ReportTopProductNode> GetReportTopProducts { get; set; } = [];
        public ReportsAppConfigNode? AppConfig { get; set; }
    }

    public sealed class ReportsAppConfigNode
    {
        public string CurrencySymbol { get; set; } = "$";
    }

    public sealed class ReportTimeSeriesNode
    {
        public DateTime PeriodStart { get; set; }
        public string PeriodLabel { get; set; } = string.Empty;
        public int TotalQuantity { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalProfit { get; set; }
    }

    public sealed class ReportTopProductNode
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public int TotalQuantity { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalProfit { get; set; }
        public int Rank { get; set; }
    }
}
