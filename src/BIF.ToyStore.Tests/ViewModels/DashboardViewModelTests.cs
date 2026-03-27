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
                            new DashboardProductNode { Id = 1, Name = "A", StockQuantity = 5, RetailPrice = 10m, Category = new DashboardCategoryNode { Name = "CatA" } },
                            new DashboardProductNode { Id = 2, Name = "B", StockQuantity = 0, RetailPrice = 11m, Category = null },
                            new DashboardProductNode { Id = 3, Name = "C", StockQuantity = 2, RetailPrice = 12m, Category = new DashboardCategoryNode { Name = "CatC" } }
                        ]
                    },
                    GetOrders = new DashboardOrderList
                    {
                        Items =
                        [
                            new DashboardOrderNode
                            {
                                Id = 10,
                                Status = "Paid",
                                CustomerName = null,
                                TotalAmount = 100m,
                                OrderDate = DateTime.Now.AddMinutes(-5),
                                OrderDetails = [new DashboardOrderDetailNode { Quantity = 2 }, new DashboardOrderDetailNode { Quantity = 1 }]
                            },
                            new DashboardOrderNode
                            {
                                Id = 11,
                                Status = "New",
                                CustomerName = "Alice",
                                TotalAmount = 50m,
                                OrderDate = DateTime.Now.AddMinutes(-20),
                                OrderDetails = [new DashboardOrderDetailNode { Quantity = 1 }]
                            }
                        ]
                    },
                    GetTopBestSellingProducts =
                    [
                        new DashboardBestSellingProductNode { ProductId = 1, ProductName = "A", CategoryName = "CatA", RetailPrice = 10m, UnitsSold = 90, Rank = 1 }
                    ],
                    GetRevenueTrend =
                    [
                        new DashboardRevenuePointNode { DayLabel = "Mon", Revenue = 120m },
                        new DashboardRevenuePointNode { DayLabel = "Tue", Revenue = 80m }
                    ]
                });

            graphQlClient
                .Setup(x => x.ExecuteAsync<DashboardTodayQueryData>(
                    It.Is<string>(q => q.Contains("DashboardToday")),
                    It.IsAny<object?>(),
                    It.IsAny<string>()))
                .ReturnsAsync(new DashboardTodayQueryData
                {
                    GetOrders = new DashboardOrderList
                    {
                        Items =
                        [
                            new DashboardOrderNode { TotalAmount = 100m },
                            new DashboardOrderNode { TotalAmount = 50m }
                        ]
                    }
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
            Assert.Equal(2, vm.RevenueTrendPoints.Count);
            Assert.NotEqual("M 0,0", vm.RevenueTrendPathData);
            Assert.NotEqual("M 0,0 Z", vm.RevenueTrendAreaData);
            Assert.True(vm.RevenueAxisMax >= 100);

            Assert.Equal(2, vm.OrdersToday);
            Assert.Equal(150m, vm.TodayRevenue);
            Assert.Equal("VND 150.00", vm.TodayRevenueDisplay);
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
                    GetOrders = new DashboardOrderList
                    {
                        Items = []
                    },
                    GetTopBestSellingProducts = [],
                    GetRevenueTrend = []
                });

            graphQlClient
                .Setup(x => x.ExecuteAsync<DashboardTodayQueryData>(
                    It.Is<string>(q => q.Contains("DashboardToday")),
                    It.IsAny<object?>(),
                    It.IsAny<string>()))
                .ReturnsAsync(new DashboardTodayQueryData
                {
                    GetOrders = new DashboardOrderList
                    {
                        Items = []
                    }
                });

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
    }
}
