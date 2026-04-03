using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.ViewModels.Pages;
using BIF.ToyStore.ViewModels.Utils;
using Moq;

namespace BIF.ToyStore.Tests.ViewModels.Pages
{
    public class OrderViewModelTests
    {
        // ─── Factories ────────────────────────────────────────────────────────

        private static OrderConnectionResponse MakeOrdersResponse(
            int total,
            bool hasNextPage,
            bool hasPreviousPage,
            params (int id, string status, string? saleName)[] orders)
        {
            var nodes = orders.Select(o => new OrderItemNode
            {
                Id = o.id,
                OrderDate = new DateTime(2026, 1, 10, 12, 0, 0),
                Status = o.status,
                TotalAmount = 100m,
                Sale = new OrderSaleNode { Username = o.saleName },
                Customer = new OrderCustomerNode { FullName = "Walk-in" }
            }).ToList();

            return new OrderConnectionResponse
            {
                TotalCount = total,
                PageInfo = new OrderPageInfo
                {
                    HasNextPage = hasNextPage,
                    HasPreviousPage = hasPreviousPage,
                    StartCursor = hasPreviousPage ? "start-cursor" : null,
                    EndCursor = hasNextPage ? "end-cursor" : null
                },
                Nodes = nodes
            };
        }

        private static OrderViewModel CreateViewModel(Mock<IGraphQLClient> client)
        {
            var settings = new Mock<ILocalSettingsService>();
            settings
                .Setup(s => s.GetInt(AppPreferenceKeys.ProductsItemsPerPage, 20))
                .Returns(20);

            return new OrderViewModel(client.Object, settings.Object);
        }

        private static OrderUserListResponse MakeUserListResponse(params (int id, string username)[] users)
        {
            return new OrderUserListResponse
            {
                GetUserList = users.Select(u => new EmployeeListItem { Id = u.id, Username = u.username }).ToList()
            };
        }

        private static GetOrderByIdResponse MakeOrderByIdResponse(int orderId, string status)
        {
            return new GetOrderByIdResponse
            {
                GetOrderById = new OrderDetailPayload
                {
                    Id = orderId,
                    OrderDate = new DateTime(2026, 1, 10, 12, 0, 0),
                    Status = status,
                    TotalAmount = 100m,
                    SaleName = "alice",
                    CustomerName = null,
                    OrderDetails =
                    [
                        new OrderDetailNodeFull
                        {
                            Id = 1,
                            Quantity = 2,
                            UnitPrice = 50m,
                            Product = new OrderProductNode { Id = 1, Name = "Widget" }
                        }
                    ]
                }
            };
        }

        // ─── LoadAsync ────────────────────────────────────────────────────────

        [Fact]
        public async Task LoadAsync_PopulatesOrdersAndEmployees()
        {
            var client = new Mock<IGraphQLClient>();

                        client.Setup(x => x.ExecuteAsync<OrderConnectionResponse>(
                                        It.IsAny<string>(), It.IsAny<object?>(), "orders"))
                                    .ReturnsAsync(MakeOrdersResponse(2, false, false, (1, "New", "alice"), (2, "Paid", "bob")));

            client.Setup(x => x.ExecuteAsync<OrderUserListResponse>(
                    It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<string>()))
                  .ReturnsAsync(MakeUserListResponse((1, "alice"), (2, "bob")));

            var vm = CreateViewModel(client);
            await vm.LoadAsync();

            Assert.Equal(2, vm.Orders.Count);
            // "All Employees" is prepended
            Assert.True(vm.Employees.Count >= 3);
            Assert.Equal("All Employees", vm.Employees.First().Username);
            Assert.False(vm.IsBusy);
        }

        [Fact]
        public async Task LoadAsync_EmptyOrders_SetsEmptyCollection()
        {
            var client = new Mock<IGraphQLClient>();

                        client.Setup(x => x.ExecuteAsync<OrderConnectionResponse>(
                                        It.IsAny<string>(), It.IsAny<object?>(), "orders"))
                                    .ReturnsAsync(MakeOrdersResponse(0, false, false));

            client.Setup(x => x.ExecuteAsync<OrderUserListResponse>(
                    It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<string>()))
                  .ReturnsAsync(MakeUserListResponse());

            var vm = CreateViewModel(client);
            await vm.LoadAsync();

            Assert.Empty(vm.Orders);
            Assert.Equal(0, vm.TotalCount);
        }

        [Fact]
        public async Task LoadAsync_GraphQLFailure_SetsErrorMessage()
        {
            var client = new Mock<IGraphQLClient>();

                client.Setup(x => x.ExecuteAsync<OrderConnectionResponse>(
                    It.IsAny<string>(), It.IsAny<object?>(), "orders"))
                  .ThrowsAsync(new InvalidOperationException("network error"));

            client.Setup(x => x.ExecuteAsync<OrderUserListResponse>(
                    It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<string>()))
                  .ReturnsAsync(MakeUserListResponse());

            var vm = CreateViewModel(client);
            await vm.LoadAsync();

            Assert.True(vm.HasError);
            Assert.Contains("Failed to load orders", vm.ErrorMessage);
            Assert.False(vm.IsBusy);
        }

        // ─── Pagination ───────────────────────────────────────────────────────

        [Fact]
        public async Task LoadAsync_MoreThanOnePage_SetsHasNextPageTrue()
        {
            var client = new Mock<IGraphQLClient>();

                        client.Setup(x => x.ExecuteAsync<OrderConnectionResponse>(
                                        It.IsAny<string>(), It.IsAny<object?>(), "orders"))
                                    .ReturnsAsync(MakeOrdersResponse(25, true, false,
                      (1, "New", null), (2, "Paid", null)));

            client.Setup(x => x.ExecuteAsync<OrderUserListResponse>(
                    It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<string>()))
                  .ReturnsAsync(MakeUserListResponse());

            var vm = CreateViewModel(client);
            await vm.LoadAsync();

            Assert.True(vm.HasNextPage);
            Assert.False(vm.HasPreviousPage);
            Assert.Equal("end-cursor", vm.AfterCursor);
        }

        [Fact]
        public async Task LoadAsync_SinglePage_HasNextPageFalse()
        {
            var client = new Mock<IGraphQLClient>();

                        client.Setup(x => x.ExecuteAsync<OrderConnectionResponse>(
                                        It.IsAny<string>(), It.IsAny<object?>(), "orders"))
                                    .ReturnsAsync(MakeOrdersResponse(3, false, false, (1, "New", null)));

            client.Setup(x => x.ExecuteAsync<OrderUserListResponse>(
                    It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<string>()))
                  .ReturnsAsync(MakeUserListResponse());

            var vm = CreateViewModel(client);
            await vm.LoadAsync();

            Assert.False(vm.HasNextPage);
            Assert.False(vm.HasPreviousPage);
            Assert.Null(vm.AfterCursor);
        }

        // ─── PaginationLabel ──────────────────────────────────────────────────

        [Fact]
        public async Task PaginationLabel_ShowsCorrectRange()
        {
            var client = new Mock<IGraphQLClient>();

                        client.Setup(x => x.ExecuteAsync<OrderConnectionResponse>(
                                        It.IsAny<string>(), It.IsAny<object?>(), "orders"))
                                    .ReturnsAsync(MakeOrdersResponse(25, false, false, (1, "New", null)));

            client.Setup(x => x.ExecuteAsync<OrderUserListResponse>(
                    It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<string>()))
                  .ReturnsAsync(MakeUserListResponse());

            var vm = CreateViewModel(client);
            await vm.LoadAsync();

            Assert.Contains("1 of 25", vm.PaginationLabel);
        }

        // ─── ApplyFilterCommand ───────────────────────────────────────────────

        [Fact]
        public async Task ApplyFilterCommand_ClosesDetailsPanelAndResets()
        {
            var client = new Mock<IGraphQLClient>();

                        client.Setup(x => x.ExecuteAsync<OrderConnectionResponse>(
                                        It.IsAny<string>(), It.IsAny<object?>(), "orders"))
                                    .ReturnsAsync(MakeOrdersResponse(0, false, false));

            client.Setup(x => x.ExecuteAsync<OrderUserListResponse>(
                    It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<string>()))
                  .ReturnsAsync(MakeUserListResponse());

            var vm = CreateViewModel(client)
            {
                IsDetailsPanelOpen = true,
                SelectedOrder = new OrderDetailsViewModel { Id = 99 }
            };

            await vm.ApplyFilterCommand.ExecuteAsync(null);

            Assert.False(vm.IsDetailsPanelOpen);
            Assert.Null(vm.SelectedOrder);
        }

        // ─── OpenOrderDetailsCommand ──────────────────────────────────────────

        [Fact]
        public async Task OpenOrderDetailsCommand_PopulatesSidebarAndOpensPanel()
        {
            var client = new Mock<IGraphQLClient>();

            client.Setup(x => x.ExecuteAsync<GetOrderByIdResponse>(
                    It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<string>()))
                  .ReturnsAsync(MakeOrderByIdResponse(orderId: 5, status: "New"));

            var vm = CreateViewModel(client);
            var order = new OrderItemViewModel { Id = 5, Status = "New" };

            await vm.OpenOrderDetailsCommand.ExecuteAsync(order);

            Assert.True(vm.IsDetailsPanelOpen);
            Assert.NotNull(vm.SelectedOrder);
            Assert.Equal(5, vm.SelectedOrder!.Id);
            Assert.Single(vm.SelectedOrder.Lines);
            Assert.Equal("Widget", vm.SelectedOrder.Lines[0].ProductName);
            Assert.Equal(2, vm.SelectedOrder.Lines[0].Quantity);
        }

        [Fact]
        public async Task OpenOrderDetailsCommand_NullInput_DoesNotCrash()
        {
            var client = new Mock<IGraphQLClient>();

            var vm = CreateViewModel(client);

            // Should silently return without throwing
            await vm.OpenOrderDetailsCommand.ExecuteAsync(null!);

            Assert.False(vm.IsDetailsPanelOpen);
            Assert.Null(vm.SelectedOrder);
        }

        // ─── CloseDetailsPanelCommand ─────────────────────────────────────────

        [Fact]
        public void CloseDetailsPanelCommand_ClearsSelectedOrderAndClosesPanel()
        {
            var client = new Mock<IGraphQLClient>();
            var vm = CreateViewModel(client)
            {
                IsDetailsPanelOpen = true,
                SelectedOrder = new OrderDetailsViewModel { Id = 1 }
            };

            vm.CloseDetailsPanelCommand.Execute(null);

            Assert.False(vm.IsDetailsPanelOpen);
            Assert.Null(vm.SelectedOrder);
        }

        // ─── DeleteOrderCommand ───────────────────────────────────────────────

        [Fact]
        public async Task DeleteOrderCommand_RemovesOrderFromCollection()
        {
            var client = new Mock<IGraphQLClient>();
            client.Setup(x => x.ExecuteAsync<bool>(
                    It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<string>()))
                  .ReturnsAsync(true);

            var vm = CreateViewModel(client);
            vm.Orders.Add(new OrderItemViewModel { Id = 42, Status = "New" });
            vm.TotalCount = 1;

            await vm.DeleteOrderCommand.ExecuteAsync(42);

            Assert.Empty(vm.Orders);
            Assert.Equal(0, vm.TotalCount);
            Assert.True(vm.HasStatusMessage);
            Assert.Contains("42", vm.StatusMessage);
        }

        [Fact]
        public async Task DeleteOrderCommand_OnSuccess_ClosesSidebar()
        {
            var client = new Mock<IGraphQLClient>();
            client.Setup(x => x.ExecuteAsync<bool>(
                    It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<string>()))
                  .ReturnsAsync(true);

            var vm = CreateViewModel(client)
            {
                IsDetailsPanelOpen = true,
                SelectedOrder = new OrderDetailsViewModel { Id = 42 }
            };

            await vm.DeleteOrderCommand.ExecuteAsync(42);

            Assert.False(vm.IsDetailsPanelOpen);
            Assert.Null(vm.SelectedOrder);
        }

        [Fact]
        public async Task DeleteOrderCommand_GraphQLFailure_SetsErrorMessage()
        {
            var client = new Mock<IGraphQLClient>();
            client.Setup(x => x.ExecuteAsync<bool>(
                    It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<string>()))
                  .ThrowsAsync(new InvalidOperationException("server error"));

            var vm = CreateViewModel(client);
            vm.Orders.Add(new OrderItemViewModel { Id = 1, Status = "New" });

            await vm.DeleteOrderCommand.ExecuteAsync(1);

            Assert.True(vm.HasError);
            Assert.Contains("Failed to delete order", vm.ErrorMessage);
            // Order should remain in the collection since server failed
            Assert.Single(vm.Orders);
        }

        // ─── UpdateOrderStatusCommand ─────────────────────────────────────────

        [Fact]
        public async Task UpdateOrderStatusCommand_ValidStatus_UpdatesLocalState()
        {
            var client = new Mock<IGraphQLClient>();
            client.Setup(x => x.ExecuteAsync<object>(
                    It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<string>()))
                  .ReturnsAsync((object?)null);

            var vm = CreateViewModel(client);
            vm.SelectedOrder = new OrderDetailsViewModel { Id = 7, Status = "New" };
            vm.Orders.Add(new OrderItemViewModel { Id = 7, Status = "New" });

            await vm.UpdateOrderStatusCommand.ExecuteAsync("Paid");

            Assert.Equal("Paid", vm.SelectedOrder.Status);
            Assert.Equal("Paid", vm.Orders.First().Status);
            Assert.True(vm.HasStatusMessage);
        }

        [Fact]
        public async Task UpdateOrderStatusCommand_NullSelectedOrder_DoesNothing()
        {
            var client = new Mock<IGraphQLClient>();
            var vm = CreateViewModel(client) { SelectedOrder = null };

            // Should silently return
            await vm.UpdateOrderStatusCommand.ExecuteAsync("Paid");

            client.Verify(
                x => x.ExecuteAsync<object>(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task UpdateOrderStatusCommand_InvalidStatus_DoesNotCallApi()
        {
            var client = new Mock<IGraphQLClient>();
            var vm = CreateViewModel(client)
            {
                SelectedOrder = new OrderDetailsViewModel { Id = 1, Status = "New" }
            };

            await vm.UpdateOrderStatusCommand.ExecuteAsync("InvalidStatus");

            client.Verify(
                x => x.ExecuteAsync<object>(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<string>()),
                Times.Never);
        }

        // ─── OrderItemViewModel display helpers ───────────────────────────────

        [Fact]
        public void OrderItemViewModel_IdDisplay_FormatsCorrectly()
        {
            var item = new OrderItemViewModel { Id = 42 };
            Assert.Equal("#TY-0042", item.IdDisplay);
        }

        [Fact]
        public void OrderItemViewModel_EmptyEmployeeName_ShowsUnknown()
        {
            var item = new OrderItemViewModel { EmployeeName = null };
            Assert.Equal("Unknown", item.EmployeeDisplay);
        }

        [Fact]
        public void OrderItemViewModel_EmptyCustomerName_ShowsWalkIn()
        {
            var item = new OrderItemViewModel { CustomerName = string.Empty };
            Assert.Equal("Walk-in", item.CustomerDisplay);
        }

        [Fact]
        public void OrderDetailLineViewModel_LineTotal_CalculatesCorrectly()
        {
            var line = new OrderDetailLineViewModel { Quantity = 3, UnitPrice = 25.50m };
            Assert.Equal(76.50m, line.LineTotal);
        }
    }
}
