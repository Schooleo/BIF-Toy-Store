using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.ViewModels.Pages;
using Moq;

namespace BIF.ToyStore.Tests.ViewModels.Pages
{
    public class DashboardViewModelTests
    {
        [Fact]
        public async Task LoadAsync_ValidGraphQlPayload_MapsDashboardData()
        {
            var graphQlClient = new Mock<IGraphQLClient>();
            var today = DateTime.Today;
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var reportSeries = new List<DashboardReportTimeSeriesNode>
            {
                new DashboardReportTimeSeriesNode
                {
                    PeriodStart = monthStart,
                    PeriodLabel = monthStart.ToString("dd MMM"),
                    TotalRevenue = 120m
                }
            };

            if (today.Day > 1)
            {
                var secondDay = monthStart.AddDays(1);
                reportSeries.Add(new DashboardReportTimeSeriesNode
                {
                    PeriodStart = secondDay,
                    PeriodLabel = secondDay.ToString("dd MMM"),
                    TotalRevenue = 80m
                });
            }

            graphQlClient
                .Setup(x => x.ExecuteAsync<DashboardMainQueryData>(
                    It.Is<string>(q => q.Contains("DashboardMain")),
                    It.IsAny<object?>(),
                    It.IsAny<string>()))
                .ReturnsAsync(new DashboardMainQueryData
                {
                    AppConfig = new DashboardAppConfigNode { CurrencySymbol = "VND" },
                    Products = new DashboardProductConnection
                    {
                        TotalCount = 7,
                        Nodes =
                        [
                            new DashboardProductNode { Id = 1, Name = "A", StockQuantity = 5, RetailPrice = 10m, ImageUrl = "https://example.com/a.png", Category = new DashboardCategoryNode { Name = "CatA" } },
                            new DashboardProductNode { Id = 2, Name = "B", StockQuantity = 0, RetailPrice = 11m, ImageUrl = "https://example.com/b.png", Category = null },
                            new DashboardProductNode { Id = 3, Name = "C", StockQuantity = 2, RetailPrice = 12m, ImageUrl = null, Category = new DashboardCategoryNode { Name = "CatC" } }
                        ]
                    },
                    GetOrders = new DashboardOrderConnection
                    {
                        TotalCount = 2,
                        Nodes =
                        [
                            new DashboardOrderNode
                            {
                                Id = 10,
                                Status = "Paid",
                                Customer = null,
                                TotalAmount = 100m,
                                OrderDate = DateTime.Now.AddMinutes(-5),
                                OrderDetails = [new DashboardOrderDetailNode { Quantity = 2 }, new DashboardOrderDetailNode { Quantity = 1 }]
                            },
                            new DashboardOrderNode
                            {
                                Id = 11,
                                Status = "New",
                                Customer = new DashboardOrderCustomerNode { FullName = "Alice" },
                                TotalAmount = 50m,
                                OrderDate = DateTime.Now.AddMinutes(-20),
                                OrderDetails = [new DashboardOrderDetailNode { Quantity = 1 }]
                            }
                        ]
                    },
                    GetTopBestSellingProducts =
                    [
                        new DashboardBestSellingProductNode { ProductId = 1, ProductName = "A", CategoryName = "CatA", RetailPrice = 10m, UnitsSold = 90, Rank = 1, ImageUrl = "https://example.com/a.png" }
                    ],
                    GetReportTimeSeries = reportSeries
                });

            graphQlClient
                .Setup(x => x.ExecuteAsync<DashboardTodaySummaryNode>(
                    It.Is<string>(q => q.Contains("DashboardTodaySummary")),
                    It.IsAny<object?>(),
                    "dashboardTodaySummary"))
                .ReturnsAsync(new DashboardTodaySummaryNode
                {
                    OrderCount = 2,
                    Revenue = 100m
                });

            var vm = new DashboardViewModel(graphQlClient.Object);

            await vm.LoadAsync();

            Assert.Equal("Workshop Overview", vm.Title);
            Assert.Equal(3, vm.LowStockProducts.Count);
            Assert.Equal(2, vm.LowStockAlertCount);
            Assert.Equal("Unknown", vm.LowStockProducts.First().CategoryName);
            Assert.Equal(7, vm.TotalProducts);
            Assert.True(vm.HasLowStockProducts);
            Assert.False(vm.IsLowStockProductsEmpty);

            Assert.Equal(2, vm.RecentOrders.Count);
            Assert.Equal("Walk-in Customer", vm.RecentOrders[0].CustomerName);
            Assert.Equal(3, vm.RecentOrders[0].ItemCount);
            Assert.True(vm.HasRecentOrders);
            Assert.False(vm.IsRecentOrdersEmpty);

            Assert.Single(vm.BestSellingProducts);
            Assert.True(vm.HasBestSellingProducts);
            Assert.False(vm.IsBestSellingProductsEmpty);
            Assert.Equal("https://example.com/a.png", vm.BestSellingProducts[0].ImageUrl);
            Assert.Equal(today.Day, vm.RevenueTrendPoints.Count);
            Assert.Equal(monthStart, vm.RevenueTrendPoints[0].PeriodStart.Date);
            Assert.Equal(today, vm.RevenueTrendPoints[^1].PeriodStart.Date);
            Assert.Equal(120m, vm.RevenueTrendPoints[0].Revenue);
            if (today.Day > 1)
            {
                Assert.Equal(80m, vm.RevenueTrendPoints[1].Revenue);
            }

            if (today.Day > 2)
            {
                Assert.All(vm.RevenueTrendPoints.Skip(2), point => Assert.Equal(0m, point.Revenue));
            }

            Assert.NotEqual("M 0,0", vm.RevenueTrendPathData);
            Assert.NotEqual("M 0,0 Z", vm.RevenueTrendAreaData);
            Assert.True(vm.RevenueAxisMax >= 100);
            Assert.NotEmpty(vm.RevenuePeriodLabel);
            Assert.True(vm.RevenueTrendScrollableWidth > 0);
            Assert.Equal(vm.RevenueTrendPoints.Count, vm.RevenueTrendMarkers.Count);
            Assert.All(vm.RevenueTrendPoints, point =>
                Assert.Equal(vm.RevenueTrendScrollableWidth / vm.RevenueTrendPoints.Count, point.LabelWidth, precision: 2));

            Assert.Equal(2, vm.OrdersToday);
            Assert.Equal(100m, vm.TodayRevenue);
            Assert.Equal("VND 100.00", vm.TodayRevenueDisplay);
            Assert.Equal("VND 11.00", vm.LowStockProducts.First().PriceDisplay);
            Assert.Equal("VND 100.00", vm.RecentOrders[0].TotalAmountDisplay);
            Assert.Equal("VND 10.00", vm.BestSellingProducts[0].PriceDisplay);
            Assert.Equal(string.Empty, vm.ErrorMessage);
            Assert.False(vm.IsBusy);
        }

        [Fact]
        public async Task LoadAsync_GraphQlFailure_ResetsDashboardStateAndSetsError()
        {
            var graphQlClient = new Mock<IGraphQLClient>();
            graphQlClient
                .Setup(x => x.ExecuteAsync<DashboardMainQueryData>(
                    It.IsAny<string>(),
                    It.IsAny<object?>(),
                    It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("boom"));

            var vm = new DashboardViewModel(graphQlClient.Object)
            {
                TotalProducts = 10,
                OrdersToday = 3,
                TodayRevenue = 99m,
                RevenueAxisMax = 200,
                RevenueTrendPathData = "M 1,1",
                RevenueTrendAreaData = "M 1,1 Z"
            };
            vm.LowStockProducts.Add(new LowStockProductViewModel());
            vm.RecentOrders.Add(new RecentOrderViewModel());
            vm.BestSellingProducts.Add(new BestSellingProductViewModel());
            vm.RevenueTrendPoints.Add(new RevenueTrendPointViewModel());

            await vm.LoadAsync();

            Assert.StartsWith("Unable to load dashboard data:", vm.ErrorMessage);
            Assert.Empty(vm.LowStockProducts);
            Assert.Empty(vm.RecentOrders);
            Assert.Empty(vm.BestSellingProducts);
            Assert.Empty(vm.RevenueTrendPoints);
            Assert.False(vm.HasLowStockProducts);
            Assert.True(vm.IsLowStockProductsEmpty);
            Assert.False(vm.HasRecentOrders);
            Assert.True(vm.IsRecentOrdersEmpty);
            Assert.False(vm.HasBestSellingProducts);
            Assert.True(vm.IsBestSellingProductsEmpty);
            Assert.Equal(0, vm.TotalProducts);
            Assert.Equal(0, vm.OrdersToday);
            Assert.Equal(0m, vm.TodayRevenue);
            Assert.Equal(0, vm.RevenueAxisMax);
            Assert.Equal("M 0,0", vm.RevenueTrendPathData);
            Assert.Equal("M 0,0 Z", vm.RevenueTrendAreaData);
            Assert.False(vm.IsBusy);
        }

        [Fact]
        public async Task LoadAsync_EmptyLists_SetsEmptyStateFlags()
        {
            var graphQlClient = new Mock<IGraphQLClient>();

            graphQlClient
                .Setup(x => x.ExecuteAsync<DashboardMainQueryData>(
                    It.Is<string>(q => q.Contains("DashboardMain")),
                    It.IsAny<object?>(),
                    It.IsAny<string>()))
                .ReturnsAsync(new DashboardMainQueryData
                {
                    Products = new DashboardProductConnection
                    {
                        TotalCount = 0,
                        Nodes = []
                    },
                    GetOrders = new DashboardOrderConnection
                    {
                        TotalCount = 0,
                        Nodes = []
                    },
                    GetTopBestSellingProducts = [],
                    GetReportTimeSeries = []
                });

            graphQlClient
                .Setup(x => x.ExecuteAsync<DashboardTodaySummaryNode>(
                    It.Is<string>(q => q.Contains("DashboardTodaySummary")),
                    It.IsAny<object?>(),
                    "dashboardTodaySummary"))
                .ReturnsAsync(new DashboardTodaySummaryNode());

            var vm = new DashboardViewModel(graphQlClient.Object);

            await vm.LoadAsync();

            Assert.Empty(vm.LowStockProducts);
            Assert.Empty(vm.RecentOrders);
            Assert.Empty(vm.BestSellingProducts);
            Assert.False(vm.HasLowStockProducts);
            Assert.True(vm.IsLowStockProductsEmpty);
            Assert.False(vm.HasRecentOrders);
            Assert.True(vm.IsRecentOrdersEmpty);
            Assert.False(vm.HasBestSellingProducts);
            Assert.True(vm.IsBestSellingProductsEmpty);
            Assert.Equal(string.Empty, vm.ErrorMessage);
        }

        [Fact]
        public async Task QuickRestock_SaveChangedStock_UpdatesProductAndClosesPanel()
        {
            var graphQlClient = new Mock<IGraphQLClient>();

            graphQlClient
                .Setup(x => x.ExecuteAsync<DashboardMainQueryData>(
                    It.Is<string>(q => q.Contains("DashboardMain")),
                    It.IsAny<object?>(),
                    It.IsAny<string>()))
                .ReturnsAsync(new DashboardMainQueryData
                {
                    AppConfig = new DashboardAppConfigNode { CurrencySymbol = "$" },
                    Products = new DashboardProductConnection
                    {
                        TotalCount = 1,
                        Nodes =
                        [
                            new DashboardProductNode
                            {
                                Id = 7,
                                Name = "Dragon Plush",
                                CategoryId = 3,
                                Category = new DashboardCategoryNode { Id = 3, Name = "Plushies" },
                                StockQuantity = 1,
                                RetailPrice = 15m,
                                ImportPrice = 8m,
                                ImageUrl = "https://example.com/dragon.png"
                            }
                        ]
                    },
                    GetOrders = new DashboardOrderConnection(),
                    GetTopBestSellingProducts = [],
                    GetReportTimeSeries = []
                });

            graphQlClient
                .Setup(x => x.ExecuteAsync<DashboardTodaySummaryNode>(
                    It.Is<string>(q => q.Contains("DashboardTodaySummary")),
                    It.IsAny<object?>(),
                    "dashboardTodaySummary"))
                .ReturnsAsync(new DashboardTodaySummaryNode());

            graphQlClient
                .Setup(x => x.ExecuteAsync<DashboardProductNode>(
                    It.Is<string>(q => q.Contains("QuickRestock")),
                    It.IsAny<object?>(),
                    "updateProduct"))
                .ReturnsAsync(new DashboardProductNode { Id = 7, Name = "Dragon Plush", StockQuantity = 12 });

            var vm = new DashboardViewModel(graphQlClient.Object);
            await vm.LoadAsync();

            vm.OpenQuickRestockPanelCommand.Execute(null);
            Assert.True(vm.IsQuickRestockPanelOpen);
            Assert.Single(vm.QuickRestockItems);

            vm.QuickRestockItems[0].StockQuantity = 12;
            await vm.SaveQuickRestockCommand.ExecuteAsync(null);

            graphQlClient.Verify(x => x.ExecuteAsync<DashboardProductNode>(
                It.Is<string>(q => q.Contains("QuickRestock")),
                It.IsAny<object?>(),
                "updateProduct"), Times.Once);
            Assert.False(vm.IsQuickRestockPanelOpen);
            Assert.Empty(vm.QuickRestockItems);
            Assert.Equal(string.Empty, vm.QuickRestockErrorMessage);
        }
    }
}
