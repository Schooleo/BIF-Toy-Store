using BIF.ToyStore.Core.Enums;
using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.ViewModels.Base;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace BIF.ToyStore.ViewModels.Pages
{
    public partial class OrderViewModel : BaseViewModel
    {
        private readonly IGraphQLClient _graphQLClient;

        private const int DefaultPageSize = 10;
        private int _currentPage = 1;

        // ── Bound collections ─────────────────────────────────────────────────
        [ObservableProperty]
        private ObservableCollection<OrderItemViewModel> _orders = new();

        [ObservableProperty]
        private ObservableCollection<EmployeeListItem> _employees = new();

        // ── Filters ───────────────────────────────────────────────────────────
        [ObservableProperty]
        private EmployeeListItem? _selectedEmployee;

        [ObservableProperty]
        private DateTimeOffset? _fromDate;

        [ObservableProperty]
        private DateTimeOffset? _toDate;

        // ── Pagination ────────────────────────────────────────────────────────
        [ObservableProperty]
        private int _totalCount;

        [ObservableProperty]
        private int _pageCount = 1;

        [ObservableProperty]
        private bool _hasNextPage;

        [ObservableProperty]
        private bool _hasPreviousPage;

        public string PaginationLabel => $"Showing {(TotalCount == 0 ? 0 : (_currentPage - 1) * DefaultPageSize + 1)} to {Math.Min(_currentPage * DefaultPageSize, TotalCount)} of {TotalCount} ORDERS";
        public int CurrentPage => _currentPage;

        // ── Details sidebar ───────────────────────────────────────────────────
        [ObservableProperty]
        private OrderDetailsViewModel? _selectedOrder;

        [ObservableProperty]
        private bool _isDetailsPanelOpen;

        // ── State ─────────────────────────────────────────────────────────────
        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
        public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

        public ObservableCollection<string> OrderStatusOptions { get; } = new()
        {
            "New", "Paid", "Cancelled"
        };

        public OrderViewModel(IGraphQLClient graphQLClient)
        {
            _graphQLClient = graphQLClient;
            Title = "Order Management";
        }

        // ── Load / Lifecycle ──────────────────────────────────────────────────
        public async Task LoadAsync()
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            try
            {
                await Task.WhenAll(LoadEmployeesAsync(), LoadOrdersInternalAsync());
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to load orders: " + ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public async Task RefreshAsync()
        {
            _currentPage = 1;
            await LoadOrdersInternalAsync();
        }

        // ── Employees ─────────────────────────────────────────────────────────
        private async Task LoadEmployeesAsync()
        {
            const string query = @"
                query GetUserList {
                    getUserList: userList {
                        id
                        username
                    }
                }";

            var result = await _graphQLClient.ExecuteAsync<OrderUserListResponse>(query);
            if (result?.GetUserList != null)
            {
                Employees.Clear();
                Employees.Add(new EmployeeListItem { Id = null, Username = "All Employees" });
                foreach (var u in result.GetUserList)
                {
                    Employees.Add(u);
                }
                SelectedEmployee = Employees.FirstOrDefault();
            }
        }

        // ── Orders ────────────────────────────────────────────────────────────
        [RelayCommand]
        public async Task ApplyFilterAsync()
        {
            _currentPage = 1;
            IsDetailsPanelOpen = false;
            SelectedOrder = null;
            await LoadOrdersInternalAsync();
        }

        [RelayCommand]
        public async Task ClearFilterAsync()
        {
            SelectedEmployee = Employees.FirstOrDefault();
            FromDate = null;
            ToDate = null;
            _currentPage = 1;
            await LoadOrdersInternalAsync();
        }

        [RelayCommand(CanExecute = nameof(HasNextPage))]
        public async Task NextPageAsync()
        {
            _currentPage++;
            await LoadOrdersInternalAsync();
        }

        [RelayCommand(CanExecute = nameof(HasPreviousPage))]
        public async Task PreviousPageAsync()
        {
            if (_currentPage > 1) _currentPage--;
            await LoadOrdersInternalAsync();
        }

        [RelayCommand]
        public async Task GoToPageAsync(int page)
        {
            if (page < 1 || page > PageCount) return;
            _currentPage = page;
            await LoadOrdersInternalAsync();
        }

        private async Task LoadOrdersInternalAsync()
        {
            IsBusy = true;
            try
            {
                const string query = @"
                    query GetOrdersPage($page: Int!, $pageSize: Int!, $fromDate: DateTime, $toDate: DateTime, $employeeId: Int) {
                        orders(page: $page, pageSize: $pageSize, fromDate: $fromDate, toDate: $toDate, employeeId: $employeeId) {
                            totalCount
                            page
                            pageSize
                            items {
                                id
                                orderDate
                                status
                                totalAmount
                                customerName
                                saleName
                                orderDetails {
                                    id
                                    quantity
                                    unitPrice
                                }
                            }
                        }
                    }";

                var variables = new
                {
                    page = _currentPage,
                    pageSize = DefaultPageSize,
                    fromDate = FromDate?.LocalDateTime.Date,
                    toDate = ToDate?.LocalDateTime.Date.AddDays(1).AddTicks(-1),
                    employeeId = SelectedEmployee?.Id
                };

                var result = await _graphQLClient.ExecuteAsync<OrdersPageResponse>(query, variables);
                var payload = result?.Orders ?? new OrderListResponse();

                Orders.Clear();
                foreach (var item in payload.Items)
                {
                    Orders.Add(OrderItemViewModel.FromPayload(item));
                }

                TotalCount = payload.TotalCount;
                PageCount = TotalCount == 0 ? 1 : (int)Math.Ceiling((double)TotalCount / DefaultPageSize);
                HasNextPage = _currentPage < PageCount;
                HasPreviousPage = _currentPage > 1;

                OnPropertyChanged(nameof(PaginationLabel));
                OnPropertyChanged(nameof(CurrentPage));
                NextPageCommand.NotifyCanExecuteChanged();
                PreviousPageCommand.NotifyCanExecuteChanged();
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to load orders: " + ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── Details ───────────────────────────────────────────────────────────
        [RelayCommand]
        public async Task OpenOrderDetailsAsync(OrderItemViewModel order)
        {
            if (order is null) return;
            IsBusy = true;
            ErrorMessage = string.Empty;
            try
            {
                const string query = @"
                    query GetOrderById($id: Int!) {
                        getOrderById: orderById(id: $id) {
                            id
                            orderDate
                            status
                            totalAmount
                            customerName
                            saleName
                            orderDetails {
                                id
                                quantity
                                unitPrice
                                product {
                                    id
                                    name
                                }
                            }
                        }
                    }";

                var result = await _graphQLClient.ExecuteAsync<GetOrderByIdResponse>(query, new { id = order.Id });
                if (result?.GetOrderById is { } detail)
                {
                    SelectedOrder = OrderDetailsViewModel.FromPayload(detail);
                    IsDetailsPanelOpen = true;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to load order details: " + ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public void CloseDetailsPanel()
        {
            IsDetailsPanelOpen = false;
            SelectedOrder = null;
        }

        // ── Mutate ────────────────────────────────────────────────────────────
        [RelayCommand]
        public async Task UpdateOrderStatusAsync(string statusString)
        {
            if (SelectedOrder is null) return;
            if (!Enum.TryParse<OrderStatus>(statusString, out var status)) return;

            IsBusy = true;
            StatusMessage = string.Empty;
            ErrorMessage = string.Empty;
            try
            {
                const string mutation = @"
                    mutation UpdateOrder($input: UpdateOrderInput!) {
                        updateOrder(input: $input) {
                            id
                            status
                        }
                    }";

                var input = new { id = SelectedOrder.Id, status = status.ToString().ToUpperInvariant(), customerId = (int?)null };
                await _graphQLClient.ExecuteAsync<object>(mutation, new { input }, dataKey: "updateOrder");

                StatusMessage = $"Order #{SelectedOrder.Id} status updated to {statusString}.";
                SelectedOrder.Status = statusString;
                SelectedOrder.StatusBadgeClass = statusString;

                // Refresh list row
                var row = Orders.FirstOrDefault(o => o.Id == SelectedOrder.Id);
                if (row is not null)
                {
                    row.Status = statusString;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to update status: " + ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public async Task DeleteOrderAsync(int orderId)
        {
            IsBusy = true;
            StatusMessage = string.Empty;
            ErrorMessage = string.Empty;
            try
            {
                const string mutation = @"
                    mutation DeleteOrder($id: Int!) {
                        deleteOrder(id: $id)
                    }";

                await _graphQLClient.ExecuteAsync<bool>(mutation, new { id = orderId }, dataKey: "deleteOrder");

                StatusMessage = $"Order #{orderId} has been deleted.";
                IsDetailsPanelOpen = false;
                SelectedOrder = null;

                // Remove from current view
                var row = Orders.FirstOrDefault(o => o.Id == orderId);
                if (row is not null)
                {
                    Orders.Remove(row);
                    TotalCount = Math.Max(0, TotalCount - 1);
                    OnPropertyChanged(nameof(PaginationLabel));
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to delete order: " + ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));
        partial void OnStatusMessageChanged(string value) => OnPropertyChanged(nameof(HasStatusMessage));
    }

    // ── Item ViewModels ────────────────────────────────────────────────────────

    public partial class OrderItemViewModel : ObservableObject
    {
        public int Id { get; set; }
        public DateTime OrderDate { get; set; }

        [ObservableProperty]
        private string _status = string.Empty;
        public decimal TotalAmount { get; set; }
        public string? EmployeeName { get; set; }
        public string? CustomerName { get; set; }

        public string IdDisplay => $"#TY-{Id:D4}";
        public string DateDisplay => OrderDate.ToString("MMM dd, yyyy HH:mm", CultureInfo.InvariantCulture);
        public string TotalDisplay => $"${TotalAmount:N2}";
        public string EmployeeDisplay => string.IsNullOrWhiteSpace(EmployeeName) ? "Unknown" : EmployeeName;
        public string CustomerDisplay => string.IsNullOrWhiteSpace(CustomerName) ? "Walk-in" : CustomerName;

        public static OrderItemViewModel FromPayload(OrderItemNode node) => new()
        {
            Id = node.Id,
            OrderDate = node.OrderDate,
            Status = node.Status,
            TotalAmount = node.TotalAmount,
            EmployeeName = node.SaleName,
            CustomerName = node.CustomerName
        };
    }

    public partial class OrderDetailsViewModel : ObservableObject
    {
        public int Id { get; set; }
        public DateTime OrderDate { get; set; }

        [ObservableProperty]
        private string _status = string.Empty;

        [ObservableProperty]
        private string _statusBadgeClass = string.Empty;

        public decimal TotalAmount { get; set; }
        public string? EmployeeName { get; set; }
        public string? CustomerName { get; set; }
        public List<OrderDetailLineViewModel> Lines { get; set; } = new();

        public string IdDisplay => $"#TY-{Id:D4}";
        public string DateDisplay => OrderDate.ToString("MMM dd, yyyy HH:mm", CultureInfo.InvariantCulture);
        public string TotalDisplay => $"${TotalAmount:N2}";
        public string EmployeeDisplay => string.IsNullOrWhiteSpace(EmployeeName) ? "Unknown" : EmployeeName;
        public string CustomerDisplay => string.IsNullOrWhiteSpace(CustomerName) ? "Walk-in Customer" : CustomerName;
        public int ItemCount => Lines.Sum(l => l.Quantity);

        public static OrderDetailsViewModel FromPayload(OrderDetailPayload p)
        {
            var vm = new OrderDetailsViewModel
            {
                Id = p.Id,
                OrderDate = p.OrderDate,
                Status = p.Status,
                StatusBadgeClass = p.Status,
                TotalAmount = p.TotalAmount,
                EmployeeName = p.SaleName,
                CustomerName = p.CustomerName
            };

            if (p.OrderDetails != null)
            {
                foreach (var d in p.OrderDetails)
                {
                    vm.Lines.Add(new OrderDetailLineViewModel
                    {
                        ProductName = d.Product?.Name ?? "Unknown Product",
                        Quantity = d.Quantity,
                        UnitPrice = d.UnitPrice
                    });
                }
            }
            return vm;
        }
    }

    public sealed class OrderDetailLineViewModel
    {
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal => Quantity * UnitPrice;
        public string QtyDisplay => $"x{Quantity}";
        public string UnitPriceDisplay => $"${UnitPrice:N2}";
        public string LineTotalDisplay => $"${LineTotal:N2}";
    }

    public sealed class EmployeeListItem
    {
        public int? Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public override string ToString() => Username;
    }

    // ── GraphQL Response Wrappers ──────────────────────────────────────────────

    public sealed class OrdersPageResponse
    {
        public OrderListResponse Orders { get; set; } = new();
    }

    public sealed class OrderListResponse
    {
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public List<OrderItemNode> Items { get; set; } = new();
    }

    public sealed class OrderItemNode
    {
        public int Id { get; set; }
        public DateTime OrderDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string? CustomerName { get; set; }
        public string? SaleName { get; set; }
        public List<OrderDetailNodeSimple> OrderDetails { get; set; } = new();
    }

    public sealed class OrderDetailNodeSimple
    {
        public int Id { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    public sealed class GetOrderByIdResponse
    {
        public OrderDetailPayload? GetOrderById { get; set; }
    }

    public sealed class OrderDetailPayload
    {
        public int Id { get; set; }
        public DateTime OrderDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string? CustomerName { get; set; }
        public string? SaleName { get; set; }
        public List<OrderDetailNodeFull> OrderDetails { get; set; } = new();
    }

    public sealed class OrderDetailNodeFull
    {
        public int Id { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public OrderProductNode? Product { get; set; }
    }

    public sealed class OrderProductNode
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public sealed class OrderUserListResponse
    {
        public List<EmployeeListItem> GetUserList { get; set; } = new();
    }
}
