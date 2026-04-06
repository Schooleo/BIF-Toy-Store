using BIF.ToyStore.Core.Enums;
using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.ViewModels.Base;
using BIF.ToyStore.ViewModels.Utils;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Globalization;

namespace BIF.ToyStore.ViewModels.Pages
{
    public partial class OrderViewModel : PaginatedViewModel
    {
        private readonly IGraphQLClient _graphQLClient;
        private readonly ILocalSettingsService _localSettingsService;
        private readonly bool _isAdminUser;
        private readonly string _currentUsername;
        private readonly int _currentUserId;
        private readonly string _currentUserRole;
        private int? _currentEmployeeId;

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

        [ObservableProperty]
        private bool _isEmployeeFilterVisible = true;

        public string PaginationLabel => $"Showing {Orders.Count} of {TotalCount} ORDERS";
        public string SelectedEmployeeDisplay => SelectedEmployee?.Username ?? "All Employees";

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

        public OrderViewModel(IGraphQLClient graphQLClient, ILocalSettingsService localSettingsService)
        {
            _graphQLClient = graphQLClient;
            _localSettingsService = localSettingsService;
            Title = "Order Management";
            PageSize = _localSettingsService.GetInt(AppPreferenceKeys.ProductsItemsPerPage, 20);

            _currentUserId = _localSettingsService.GetInt(AppPreferenceKeys.CurrentUserId, 0);
            _currentUserRole = _localSettingsService.GetString(AppPreferenceKeys.CurrentUserRole, UserRole.Admin.ToString());
            _currentUsername = _localSettingsService.GetString("LastUsername", string.Empty);
            _isAdminUser = Enum.TryParse<UserRole>(_currentUserRole, true, out var role) && role == UserRole.Admin;
            IsEmployeeFilterVisible = _isAdminUser;
        }

        partial void OnSelectedEmployeeChanged(EmployeeListItem? value)
        {
            OnPropertyChanged(nameof(SelectedEmployeeDisplay));
        }

        // ── Load / Lifecycle ──────────────────────────────────────────────────
        public async Task LoadAsync()
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            try
            {
                await LoadEmployeesAsync();
                await LoadPageAsync(null);
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
            BeforeCursor = null;
            AfterCursor = null;
            await LoadPageAsync(null);
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

                if (_isAdminUser)
                {
                    Employees.Add(new EmployeeListItem { Id = null, Username = "All Employees" });
                    foreach (var u in result.GetUserList)
                    {
                        Employees.Add(u);
                    }

                    SelectedEmployee = Employees.FirstOrDefault();
                    _currentEmployeeId = null;
                    return;
                }

                var currentEmployee = result.GetUserList.FirstOrDefault(x =>
                    string.Equals(x.Username, _currentUsername, StringComparison.OrdinalIgnoreCase));

                if (currentEmployee != null)
                {
                    Employees.Add(currentEmployee);
                    SelectedEmployee = currentEmployee;
                    _currentEmployeeId = currentEmployee.Id;
                }
                else
                {
                    // Avoid exposing all orders when current sale user cannot be resolved.
                    _currentEmployeeId = -1;
                }
            }
        }

        // ── Orders ────────────────────────────────────────────────────────────
        [RelayCommand]
        public async Task ApplyFilterAsync()
        {
            BeforeCursor = null;
            AfterCursor = null;
            IsDetailsPanelOpen = false;
            SelectedOrder = null;
            await LoadPageAsync(null);
        }

        [RelayCommand]
        public async Task ClearFilterAsync()
        {
            if (_isAdminUser)
            {
                SelectedEmployee = Employees.FirstOrDefault();
            }

            FromDate = null;
            ToDate = null;
            BeforeCursor = null;
            AfterCursor = null;
            await LoadPageAsync(null);
        }

        protected override async Task LoadPageAsync(string? direction)
        {
            IsBusy = true;
            try
            {
                const string query = @"
                    query GetOrdersPage($first: Int, $last: Int, $after: String, $before: String, $fromDate: DateTime, $toDate: DateTime, $employeeId: Int, $currentUserId: Int, $currentUserRole: String) {
                        orders(first: $first, last: $last, after: $after, before: $before, fromDate: $fromDate, toDate: $toDate, employeeId: $employeeId, currentUserId: $currentUserId, currentUserRole: $currentUserRole) {
                            totalCount
                            pageInfo {
                                hasNextPage
                                hasPreviousPage
                                startCursor
                                endCursor
                            }
                            nodes {
                                id
                                orderDate
                                status
                                totalAmount
                                customer {
                                    fullName
                                }
                                sale {
                                    username
                                }
                                orderDetails {
                                    id
                                    quantity
                                    unitPrice
                                }
                            }
                        }
                    }";

                int? firstVar = null;
                int? lastVar = null;
                string? afterVar = null;
                string? beforeVar = null;

                if (direction == "next" && !string.IsNullOrEmpty(AfterCursor))
                {
                    firstVar = PageSize;
                    afterVar = AfterCursor;
                }
                else if (direction == "prev" && !string.IsNullOrEmpty(BeforeCursor))
                {
                    lastVar = PageSize;
                    beforeVar = BeforeCursor;
                }
                else if (direction == "last")
                {
                    lastVar = PageSize;
                }
                else
                {
                    firstVar = PageSize;
                }

                var variables = new
                {
                    first = firstVar,
                    last = lastVar,
                    after = afterVar,
                    before = beforeVar,
                    fromDate = FromDate?.LocalDateTime.Date,
                    toDate = ToDate?.LocalDateTime.Date.AddDays(1).AddTicks(-1),
                    employeeId = _isAdminUser ? SelectedEmployee?.Id : _currentEmployeeId,
                    currentUserId = _localSettingsService.GetInt(AppPreferenceKeys.CurrentUserId, _currentUserId),
                    currentUserRole = _localSettingsService.GetString(AppPreferenceKeys.CurrentUserRole, _currentUserRole)
                };

                var payload = await _graphQLClient.ExecuteAsync<OrderConnectionResponse>(query, variables, dataKey: "orders")
                    ?? new OrderConnectionResponse();

                Orders.Clear();
                foreach (var item in payload.Nodes)
                {
                    Orders.Add(OrderItemViewModel.FromPayload(item));
                }

                ApplyPageInfo(
                    payload.TotalCount,
                    payload.PageInfo?.HasNextPage ?? false,
                    payload.PageInfo?.HasPreviousPage ?? false,
                    payload.PageInfo?.StartCursor,
                    payload.PageInfo?.EndCursor);

                OnPropertyChanged(nameof(PaginationLabel));
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
                    query GetOrderById($id: Int!, $currentUserId: Int, $currentUserRole: String) {
                        getOrderById: orderById(id: $id, currentUserId: $currentUserId, currentUserRole: $currentUserRole) {
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

                var result = await _graphQLClient.ExecuteAsync<GetOrderByIdResponse>(
                    query,
                    new
                    {
                        id = order.Id,
                        currentUserId = _localSettingsService.GetInt(AppPreferenceKeys.CurrentUserId, _currentUserId),
                        currentUserRole = _localSettingsService.GetString(AppPreferenceKeys.CurrentUserRole, _currentUserRole)
                    });
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
                    mutation UpdateOrder($input: UpdateOrderInput!, $currentUserId: Int, $currentUserRole: String) {
                        updateOrder(input: $input, currentUserId: $currentUserId, currentUserRole: $currentUserRole) {
                            id
                            status
                        }
                    }";

                var input = new { id = SelectedOrder.Id, status = status.ToString().ToUpperInvariant(), customerId = (int?)null };
                var updated = await _graphQLClient.ExecuteAsync<OrderStatusUpdateResponse>(
                    mutation,
                    new
                    {
                        input,
                        currentUserId = _localSettingsService.GetInt(AppPreferenceKeys.CurrentUserId, _currentUserId),
                        currentUserRole = _localSettingsService.GetString(AppPreferenceKeys.CurrentUserRole, _currentUserRole)
                    },
                    dataKey: "updateOrder");
                var updatedStatus = updated?.Status ?? status.ToString();

                StatusMessage = $"Order #{SelectedOrder.Id} status updated to {updatedStatus}.";
                SelectedOrder.Status = updatedStatus;
                SelectedOrder.StatusBadgeClass = updatedStatus;

                // Refresh list row
                var row = Orders.FirstOrDefault(o => o.Id == SelectedOrder.Id);
                if (row is not null)
                {
                    row.Status = updatedStatus;
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
                    mutation DeleteOrder($id: Int!, $currentUserId: Int, $currentUserRole: String) {
                        deleteOrder(id: $id, currentUserId: $currentUserId, currentUserRole: $currentUserRole)
                    }";

                await _graphQLClient.ExecuteAsync<bool>(
                    mutation,
                    new
                    {
                        id = orderId,
                        currentUserId = _localSettingsService.GetInt(AppPreferenceKeys.CurrentUserId, _currentUserId),
                        currentUserRole = _localSettingsService.GetString(AppPreferenceKeys.CurrentUserRole, _currentUserRole)
                    },
                    dataKey: "deleteOrder");

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
            EmployeeName = node.Sale?.Username,
            CustomerName = node.Customer?.FullName
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
        public OrderConnectionResponse Orders { get; set; } = new();
    }

    public sealed class OrderConnectionResponse
    {
        public int TotalCount { get; set; }
        public OrderPageInfo? PageInfo { get; set; }
        public List<OrderItemNode> Nodes { get; set; } = new();
    }

    public sealed class OrderPageInfo
    {
        public bool HasNextPage { get; set; }
        public bool HasPreviousPage { get; set; }
        public string? StartCursor { get; set; }
        public string? EndCursor { get; set; }
    }

    public sealed class OrderItemNode
    {
        public int Id { get; set; }
        public DateTime OrderDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public OrderCustomerNode? Customer { get; set; }
        public OrderSaleNode? Sale { get; set; }
        public List<OrderDetailNodeSimple> OrderDetails { get; set; } = new();
    }

    public sealed class OrderCustomerNode
    {
        public string? FullName { get; set; }
    }

    public sealed class OrderSaleNode
    {
        public string? Username { get; set; }
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

    public sealed class OrderStatusUpdateResponse
    {
        public int Id { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
