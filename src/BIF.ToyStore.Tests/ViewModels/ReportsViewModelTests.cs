using BIF.ToyStore.Core.Enums;
using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.ViewModels.Pages;
using Moq;
using System.Net.Http;
using System.Text.Json;

namespace BIF.ToyStore.Tests.ViewModels.Pages
{
    public class ReportsViewModelTests
    {
        [Fact]
        public async Task LoadAsync_ValidPayload_MapsCollectionsAndChartState()
        {
            var client = new Mock<IGraphQLClient>();
            string sentGroupBy = string.Empty;
            client.Setup(x => x.ExecuteAsync<ReportsQueryData>(
                    It.Is<string>(q => q.Contains("ReportsData")),
                    It.IsAny<object?>(),
                    It.IsAny<string>()))
                .Callback<string, object?, string>((_, variables, _) =>
                {
                    if (variables is null)
                    {
                        sentGroupBy = string.Empty;
                        return;
                    }

                    using var variablesJson = JsonDocument.Parse(JsonSerializer.Serialize(variables));
                    sentGroupBy = variablesJson.RootElement.GetProperty("groupBy").GetString() ?? string.Empty;
                })
                .ReturnsAsync(new ReportsQueryData
                {
                    AppConfig = new ReportsAppConfigNode { CurrencySymbol = "VND" },
                    GetReportTimeSeries =
                    [
                        new ReportTimeSeriesNode
                        {
                            PeriodStart = new DateTime(2026, 3, 1),
                            PeriodLabel = "01 Mar",
                            TotalQuantity = 10,
                            TotalRevenue = 100m,
                            TotalProfit = 40m
                        },
                        new ReportTimeSeriesNode
                        {
                            PeriodStart = new DateTime(2026, 3, 2),
                            PeriodLabel = "02 Mar",
                            TotalQuantity = 5,
                            TotalRevenue = 50m,
                            TotalProfit = 18m
                        }
                    ],
                    GetReportTopProducts =
                    [
                        new ReportTopProductNode
                        {
                            ProductId = 1,
                            ProductName = "Puzzle A",
                            CategoryName = "Puzzles",
                            TotalQuantity = 10,
                            TotalRevenue = 100m,
                            TotalProfit = 40m,
                            Rank = 1
                        },
                        new ReportTopProductNode
                        {
                            ProductId = 2,
                            ProductName = "Figure B",
                            CategoryName = "Figures",
                            TotalQuantity = 5,
                            TotalRevenue = 50m,
                            TotalProfit = 18m,
                            Rank = 2
                        }
                    ]
                });

            var vm = new ReportsViewModel(client.Object)
            {
                SelectedGroupBy = new ReportGroupByOption("Day", ReportGroupBy.Day)
            };

            await vm.LoadAsync();

            Assert.Equal(2, vm.TimeSeriesPoints.Count);
            Assert.Equal(2, vm.TopProducts.Count);
            Assert.True(vm.HasTimeSeriesData);
            Assert.True(vm.HasTopProducts);
            Assert.Equal("VND", vm.CurrencySymbol);
            Assert.All(vm.TimeSeriesPoints, p => Assert.True(p.RevenueBarHeight > 0));
            Assert.All(vm.TimeSeriesPoints, p => Assert.True(p.ProfitBarHeight > 0));
            Assert.All(vm.TimeSeriesPoints, p => Assert.Equal(380d, p.LabelWidth));
            Assert.NotEqual("M 0,0", vm.TrendPathData);
            Assert.NotEqual("M 0,0 Z", vm.TrendAreaData);
            Assert.True(vm.TrendAxisMax >= 10);
            Assert.Equal(string.Empty, vm.ErrorMessage);
            Assert.False(vm.IsBusy);
            Assert.Equal("DAY", sentGroupBy);
        }

        [Fact]
        public async Task ApplyFiltersAsync_LargeDayRange_AutoAdjustsGrouping()
        {
            var client = new Mock<IGraphQLClient>();
            client.Setup(x => x.ExecuteAsync<ReportsQueryData>(
                    It.IsAny<string>(),
                    It.IsAny<object?>(),
                    It.IsAny<string>()))
                .ReturnsAsync(new ReportsQueryData());

            var vm = new ReportsViewModel(client.Object)
            {
                FromDate = new DateTimeOffset(new DateTime(2020, 1, 1)),
                ToDate = new DateTimeOffset(new DateTime(2026, 1, 1)),
                SelectedGroupBy = new ReportGroupByOption("Day", ReportGroupBy.Day)
            };

            await vm.ApplyFiltersCommand.ExecuteAsync(null);

            Assert.Equal(ReportGroupBy.Month, vm.EffectiveGroupBy);
            Assert.Contains("adjusted", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ApplyFiltersAsync_WhenQueryFails_SetsErrorAndResetsCollections()
        {
            var client = new Mock<IGraphQLClient>();
            client.Setup(x => x.ExecuteAsync<ReportsQueryData>(
                    It.IsAny<string>(),
                    It.IsAny<object?>(),
                    It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("boom"));

            var vm = new ReportsViewModel(client.Object);
            vm.TimeSeriesPoints.Add(new ReportTimePointViewModel { Label = "x", Quantity = 1 });
            vm.TopProducts.Add(new ReportTopProductViewModel { ProductName = "p" });

            await vm.ApplyFiltersCommand.ExecuteAsync(null);

            Assert.StartsWith("Unable to load reports:", vm.ErrorMessage);
            Assert.Empty(vm.TimeSeriesPoints);
            Assert.Empty(vm.TopProducts);
            Assert.Equal("M 0,0", vm.TrendPathData);
            Assert.Equal("M 0,0 Z", vm.TrendAreaData);
            Assert.False(vm.IsBusy);
        }

        [Fact]
        public async Task ApplyFiltersAsync_WhenDatesAreReversed_SwapsDateRange()
        {
            var client = new Mock<IGraphQLClient>();
            client.Setup(x => x.ExecuteAsync<ReportsQueryData>(
                    It.IsAny<string>(),
                    It.IsAny<object?>(),
                    It.IsAny<string>()))
                .ReturnsAsync(new ReportsQueryData());

            var vm = new ReportsViewModel(client.Object)
            {
                FromDate = new DateTimeOffset(new DateTime(2026, 4, 10)),
                ToDate = new DateTimeOffset(new DateTime(2026, 4, 1))
            };

            await vm.ApplyFiltersCommand.ExecuteAsync(null);

            Assert.Equal(new DateTimeOffset(new DateTime(2026, 4, 1)), vm.FromDate);
            Assert.Equal(new DateTimeOffset(new DateTime(2026, 4, 10)), vm.ToDate);
        }

        [Fact]
        public async Task ApplyFiltersAsync_NullPayload_UsesDefaultCurrencyAndEmptyChart()
        {
            var client = new Mock<IGraphQLClient>();
            client.Setup(x => x.ExecuteAsync<ReportsQueryData>(
                    It.IsAny<string>(),
                    It.IsAny<object?>(),
                    It.IsAny<string>()))
                .ReturnsAsync((ReportsQueryData?)null);

            var vm = new ReportsViewModel(client.Object);

            await vm.ApplyFiltersCommand.ExecuteAsync(null);

            Assert.Equal("$", vm.CurrencySymbol);
            Assert.Empty(vm.TimeSeriesPoints);
            Assert.Empty(vm.TopProducts);
            Assert.Equal("M 0,0", vm.TrendPathData);
            Assert.Equal("M 0,0 Z", vm.TrendAreaData);
            Assert.Equal(0, vm.TrendAxisMax);
        }

        [Fact]
        public async Task ApplyFiltersAsync_WeekGroupingOverLargeRange_AdjustsToMonth()
        {
            var client = new Mock<IGraphQLClient>();
            client.Setup(x => x.ExecuteAsync<ReportsQueryData>(
                    It.IsAny<string>(),
                    It.IsAny<object?>(),
                    It.IsAny<string>()))
                .ReturnsAsync(new ReportsQueryData());

            var vm = new ReportsViewModel(client.Object)
            {
                FromDate = new DateTimeOffset(new DateTime(2020, 1, 1)),
                ToDate = new DateTimeOffset(new DateTime(2024, 1, 1)),
                SelectedGroupBy = new ReportGroupByOption("Week", ReportGroupBy.Week)
            };

            await vm.ApplyFiltersCommand.ExecuteAsync(null);

            Assert.Equal(ReportGroupBy.Month, vm.EffectiveGroupBy);
            Assert.Contains("adjusted", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ApplyFiltersAsync_MonthGroupingOverLargeRange_AdjustsToYear()
        {
            var client = new Mock<IGraphQLClient>();
            client.Setup(x => x.ExecuteAsync<ReportsQueryData>(
                    It.IsAny<string>(),
                    It.IsAny<object?>(),
                    It.IsAny<string>()))
                .ReturnsAsync(new ReportsQueryData());

            var vm = new ReportsViewModel(client.Object)
            {
                FromDate = new DateTimeOffset(new DateTime(2010, 1, 1)),
                ToDate = new DateTimeOffset(new DateTime(2025, 1, 1)),
                SelectedGroupBy = new ReportGroupByOption("Month", ReportGroupBy.Month)
            };

            await vm.ApplyFiltersCommand.ExecuteAsync(null);

            Assert.Equal(ReportGroupBy.Year, vm.EffectiveGroupBy);
            Assert.Contains("adjusted", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ApplyFiltersAsync_WeekGroupingSmallRange_SendsWeekGraphQlValue()
        {
            var client = new Mock<IGraphQLClient>();
            string sentGroupBy = string.Empty;

            client.Setup(x => x.ExecuteAsync<ReportsQueryData>(
                    It.IsAny<string>(),
                    It.IsAny<object?>(),
                    It.IsAny<string>()))
                .Callback<string, object?, string>((_, variables, _) =>
                {
                    if (variables is null)
                    {
                        return;
                    }

                    using var variablesJson = JsonDocument.Parse(JsonSerializer.Serialize(variables));
                    sentGroupBy = variablesJson.RootElement.GetProperty("groupBy").GetString() ?? string.Empty;
                })
                .ReturnsAsync(new ReportsQueryData());

            var vm = new ReportsViewModel(client.Object)
            {
                FromDate = new DateTimeOffset(new DateTime(2026, 3, 1)),
                ToDate = new DateTimeOffset(new DateTime(2026, 3, 7)),
                SelectedGroupBy = new ReportGroupByOption("Week", ReportGroupBy.Week)
            };

            await vm.ApplyFiltersCommand.ExecuteAsync(null);

            Assert.Equal("WEEK", sentGroupBy);
            Assert.Equal(ReportGroupBy.Week, vm.EffectiveGroupBy);
            Assert.Equal(string.Empty, vm.StatusMessage);
        }

        [Fact]
        public async Task ApplyFiltersAsync_Http400WithDetail_ReturnsFriendlyDetailMessage()
        {
            var client = new Mock<IGraphQLClient>();
            client.Setup(x => x.ExecuteAsync<ReportsQueryData>(
                    It.IsAny<string>(),
                    It.IsAny<object?>(),
                    It.IsAny<string>()))
                .ThrowsAsync(new HttpRequestException("HTTP 400 Error: Invalid groupBy argument."));

            var vm = new ReportsViewModel(client.Object);

            await vm.ApplyFiltersCommand.ExecuteAsync(null);

            Assert.Equal("Unable to load reports: Invalid groupBy argument.", vm.ErrorMessage);
        }

        [Fact]
        public async Task ApplyFiltersAsync_HttpWithout400Prefix_ReturnsServerUnreachableMessage()
        {
            var client = new Mock<IGraphQLClient>();
            client.Setup(x => x.ExecuteAsync<ReportsQueryData>(
                    It.IsAny<string>(),
                    It.IsAny<object?>(),
                    It.IsAny<string>()))
                .ThrowsAsync(new HttpRequestException("timeout"));

            var vm = new ReportsViewModel(client.Object);

            await vm.ApplyFiltersCommand.ExecuteAsync(null);

            Assert.Equal("Unable to load reports: Could not reach the server.", vm.ErrorMessage);
        }

        [Fact]
        public void ReportTimePointViewModel_DisplayProperties_FormatAsExpected()
        {
            var point = new ReportTimePointViewModel
            {
                Label = "Mar W1",
                Quantity = 15,
                Revenue = 1250.5m,
                Profit = 300m,
                CurrencySymbol = "VND"
            };

            Assert.Equal("15", point.QuantityDisplay);
            Assert.Equal("VND 1,250.50", point.RevenueDisplay);
            Assert.Equal("VND 300.00", point.ProfitDisplay);
            Assert.Contains("Mar W1", point.TooltipText, StringComparison.Ordinal);
            Assert.Contains("VND 1,250.50", point.TooltipText, StringComparison.Ordinal);
        }

        [Fact]
        public void ReportTopProductViewModel_DisplayProperties_FormatAsExpected()
        {
            var item = new ReportTopProductViewModel
            {
                ProductName = "Robot",
                TotalQuantity = 8,
                TotalRevenue = 999.99m,
                TotalProfit = 321.45m,
                Rank = 2,
                CurrencySymbol = "$"
            };

            Assert.Equal("2", item.RankDisplay);
            Assert.Equal("8 units", item.QuantityDisplay);
            Assert.Equal("$999.99", item.RevenueDisplay);
            Assert.Equal("$321.45", item.ProfitDisplay);
            Assert.Contains("Robot", item.TooltipText, StringComparison.Ordinal);
            Assert.Contains("Revenue: $999.99", item.TooltipText, StringComparison.Ordinal);
        }
    }
}
