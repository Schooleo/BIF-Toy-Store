using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.Core.Enums;
using BIF.ToyStore.ViewModels.Base;
using BIF.ToyStore.ViewModels.Messages;
using BIF.ToyStore.ViewModels.Utils;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace BIF.ToyStore.ViewModels.Pages
{
    public partial class POSViewModel : BaseViewModel, IRecipient<LoginSucceededMessage>
    {
        private readonly IGraphQLClient _graphQLClient;
        private readonly IMessenger _messenger;
        private readonly ILocalSettingsService _localSettingsService;
        private List<ProductItemViewModel> _allProducts = new();


        [RelayCommand]
        private void ClearFilter()
        {
            SearchText = string.Empty;
            SelectedCategory = "All Categories";
            SelectedSort = "Newest";
            ErrorMessage = string.Empty;
            SuccessMessage = string.Empty;
        }

        // ── Bound collections ─────────────────────────────────────────────────
        [ObservableProperty]
        private ObservableCollection<ProductItemViewModel> _filteredProducts = new();

        [ObservableProperty]
        private ObservableCollection<CartItemViewModel> _cartItems = new();

        [ObservableProperty]
        private ObservableCollection<string> _categories = new() { "All Categories" };

        [ObservableProperty]
        private string _selectedCategory = "All Categories";

        [ObservableProperty]
        private string _selectedSort = "Newest";

        [ObservableProperty]
        private string _searchText = string.Empty;

        // ── Totals ────────────────────────────────────────────────────────────
        [ObservableProperty]
        private decimal _subtotal;

        [ObservableProperty]
        private decimal _tax;

        [ObservableProperty]
        private decimal _totalDue;

        [ObservableProperty]
        private decimal _configuredTaxRate = 0.08m;

        [ObservableProperty]
        private string _currencySymbol = "VND";

        public string SubtotalDisplay => FormatCurrency(Subtotal);
        public string TaxDisplay => FormatCurrency(Tax);
        public string TotalDueDisplay => FormatCurrency(TotalDue);
        public string TaxLabel => $"Tax ({ConfiguredTaxRate * 100m:0.##}%)";
        public bool HasFilteredProducts => FilteredProducts.Count > 0;
        public bool IsFilteredProductsEmpty => !HasFilteredProducts;

        // ── Sort options ──────────────────────────────────────────────────────
        public ObservableCollection<string> SortOptions { get; } = new()
        {
            "Newest", "Price: Low to High", "Price: High to Low", "Stock: Low to High", "Name: A-Z"
        };

        // ── State ─────────────────────────────────────────────────────────────
        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private string _successMessage = string.Empty;

        public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
        public bool HasSuccess => !string.IsNullOrWhiteSpace(SuccessMessage);

        // ── Current sale's user id (set on login) ─────────────────────────────
        private int _currentSaleId;
        private string _currentUserRole;

        // ─────────────────────────────────────────────────────────────────────
        public POSViewModel(IGraphQLClient graphQLClient, ILocalSettingsService localSettingsService, IMessenger messenger)
        {
            _graphQLClient = graphQLClient;
            _localSettingsService = localSettingsService;
            _messenger = messenger;
            Title = "Point of Sale";
            _currentSaleId = _localSettingsService.GetInt(AppPreferenceKeys.CurrentUserId, 0);
            _currentUserRole = _localSettingsService.GetString(AppPreferenceKeys.CurrentUserRole, UserRole.Admin.ToString());

            _messenger.Register(this);
        }

        public void Receive(LoginSucceededMessage message)
        {
            _currentSaleId = message.Value.Id;
            _currentUserRole = message.Value.Role.ToString();
            _localSettingsService.SetInt(AppPreferenceKeys.CurrentUserId, _currentSaleId);
            _localSettingsService.SetString(AppPreferenceKeys.CurrentUserRole, _currentUserRole);
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────
        public async Task LoadAsync()
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            try
            {
                await LoadStoreConfigAsync();
                await Task.WhenAll(LoadCategoriesAsync(), LoadProductsAsync());
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load data: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadStoreConfigAsync()
        {
            const string query = @"
                query GetPosConfig {
                    appConfig {
                        currencySymbol
                        taxRate
                    }
                }";

            var config = await _graphQLClient.ExecuteAsync<PosAppConfigNode>(query, dataKey: "appConfig");
            if (config is null)
            {
                return;
            }

            CurrencySymbol = string.IsNullOrWhiteSpace(config.CurrencySymbol) ? "VND" : config.CurrencySymbol;
            ConfiguredTaxRate = config.TaxRate >= 0 ? config.TaxRate : 0m;
        }

        // ── Data loading ──────────────────────────────────────────────────────
        private async Task LoadCategoriesAsync()
        {
            const string query = @"
                query GetCategories {
                    categories(first: 50) {
                        nodes {
                            id
                            name
                        }
                    }
                }";

            var result = await _graphQLClient.ExecuteAsync<CategoryConnection>(query, dataKey: "categories");
            if (result?.Nodes != null)
            {
                var prevCat = SelectedCategory;
                Categories.Clear();
                Categories.Add("All Categories");
                foreach (var c in result.Nodes)
                {
                    Categories.Add(c.Name);
                }
                
                SelectedCategory = Categories.Contains(prevCat) ? prevCat : "All Categories";
            }
        }

        private async Task LoadProductsAsync()
        {
            const string query = @"
                query GetProductsForPOS($after: String) {
                    products(first: 50, after: $after, where: { isDeleted: { eq: false } }, order: [{ name: ASC }]) {
                        pageInfo {
                            hasNextPage
                            endCursor
                        }
                        nodes {
                            id
                            name
                            categoryId
                             categoryName
                             retailPrice
                             stockQuantity
                             images {
                                 imageUrl
                                 isPrimary
                                 displayOrder
                             }
                         }
                     }
                 }";

            _allProducts.Clear();
            bool hasNext = true;
            string? cursor = null;

            while (hasNext)
            {
                var result = await _graphQLClient.ExecuteAsync<ProductConnectionSimple>(
                    query, 
                    new { after = cursor }, 
                    dataKey: "products");

                if (result?.Nodes != null)
                {
                    _allProducts.AddRange(result.Nodes.Select(p => new ProductItemViewModel(p, CurrencySymbol)));
                }

                if (result?.PageInfo != null && result.PageInfo.HasNextPage && !string.IsNullOrEmpty(result.PageInfo.EndCursor))
                {
                    cursor = result.PageInfo.EndCursor;
                }
                else
                {
                    hasNext = false;
                }
            }

            ApplyFilterAndSort();
        }

        // ── Filter / Sort ─────────────────────────────────────────────────────
        partial void OnSelectedCategoryChanged(string value) => ApplyFilterAndSort();
        partial void OnSelectedSortChanged(string value) => ApplyFilterAndSort();
        partial void OnSearchTextChanged(string value) => ApplyFilterAndSort();

        private void ApplyFilterAndSort()
        {
            IEnumerable<ProductItemViewModel> query = _allProducts;
            string currentCategory = string.IsNullOrWhiteSpace(SelectedCategory) ? "All Categories" : SelectedCategory;
            string currentSearch = SearchText?.Trim() ?? string.Empty;

            if (!string.Equals(currentCategory, "All Categories", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(p => p.CategoryName == currentCategory);
            }

            if (!string.IsNullOrWhiteSpace(currentSearch))
            {
                query = query.Where(p => p.Name.Contains(currentSearch, StringComparison.OrdinalIgnoreCase));
            }

            query = SelectedSort switch
            {
                "Newest" => query.OrderByDescending(p => p.Id),
                "Price: Low to High" => query.OrderBy(p => p.Price),
                "Price: High to Low" => query.OrderByDescending(p => p.Price),
                "Stock: Low to High" => query.OrderBy(p => p.StockQuantity),
                "Name: A-Z" => query.OrderBy(p => p.Name),
                _ => query.OrderByDescending(p => p.Id)
            };

            FilteredProducts.Clear();
            foreach (var p in query)
            {
                FilteredProducts.Add(p);
            }

            OnPropertyChanged(nameof(HasFilteredProducts));
            OnPropertyChanged(nameof(IsFilteredProductsEmpty));
        }

        // ── Cart commands ─────────────────────────────────────────────────────
        [RelayCommand]
        private void AddToCart(ProductItemViewModel? product)
        {
            if (product is null) return;
            if (product.CartQuantity >= product.StockQuantity) return;

            var existing = CartItems.FirstOrDefault(c => c.Product.Id == product.Id);
            if (existing is not null)
            {
                existing.Quantity++;
                product.CartQuantity++;
            }
            else
            {
                CartItems.Add(new CartItemViewModel(product));
                product.CartQuantity++;
            }

            RecalculateTotals();
        }

        [RelayCommand]
        private void IncreaseQuantity(CartItemViewModel? item)
        {
            if (item is null) return;
            if (item.Quantity >= item.Product.StockQuantity) return;

            item.Quantity++;
            item.Product.CartQuantity++;
            RecalculateTotals();
        }

        [RelayCommand]
        private void DecreaseQuantity(CartItemViewModel? item)
        {
            if (item is null) return;

            if (item.Quantity <= 1)
            {
                CartItems.Remove(item);
                item.Product.CartQuantity = 0;
            }
            else
            {
                item.Quantity--;
                item.Product.CartQuantity--;
            }

            RecalculateTotals();
        }

        [RelayCommand]
        private void RemoveFromCart(CartItemViewModel? item)
        {
            if (item is null) return;
            CartItems.Remove(item);
            item.Product.CartQuantity = 0;
            RecalculateTotals();
        }

        [RelayCommand]
        private void ClearCart()
        {
            foreach (var cartItem in CartItems)
            {
                cartItem.Product.CartQuantity = 0;
            }
            CartItems.Clear();
            RecalculateTotals();
            ErrorMessage = string.Empty;
            SuccessMessage = string.Empty;
        }

        // ── Order creation ────────────────────────────────────────────────────
        [RelayCommand]
        private async Task ProcessPayment()
        {
            await CreateOrderAsync(markAsPaid: true);
        }

        [RelayCommand]
        private async Task HoldOrder()
        {
            await CreateOrderAsync(markAsPaid: false);
        }

        private async Task CreateOrderAsync(bool markAsPaid)
        {
            if (CartItems.Count == 0)
            {
                ErrorMessage = "Cart is empty. Add products before creating an order.";
                return;
            }

            IsBusy = true;
            ErrorMessage = string.Empty;
            SuccessMessage = string.Empty;

            try
            {
                const string mutation = @"
                    mutation CreateOrder($input: CreateOrderInput!, $currentUserId: Int, $currentUserRole: String) {
                        createOrder(input: $input, currentUserId: $currentUserId, currentUserRole: $currentUserRole) {
                            id
                            totalAmount
                            status
                            orderDate
                        }
                    }";

                var saleId = _currentSaleId > 0
                    ? _currentSaleId
                    : _localSettingsService.GetInt(AppPreferenceKeys.CurrentUserId, 0);

                if (saleId <= 0)
                {
                    ErrorMessage = "Could not identify the current employee. Please log in again.";
                    return;
                }

                _currentSaleId = saleId;

                var input = new
                {
                    saleId,
                    customerId = (int?)null,
                    items = CartItems.Select(c => new
                    {
                        productId = c.Product.Id,
                        quantity = c.Quantity,
                        unitPrice = c.Product.Price
                    }).ToList()
                };

                var result = await _graphQLClient.ExecuteAsync<OrderResult>(
                    mutation,
                    new
                    {
                        input,
                        currentUserId = _currentSaleId,
                        currentUserRole = _currentUserRole
                    },
                    dataKey: "createOrder");

                if (result != null)
                {
                    string finalizedStatus = result.Status;

                    if (markAsPaid)
                    {
                        const string updateOrderMutation = @"
                            mutation MarkOrderPaid($input: UpdateOrderInput!, $currentUserId: Int, $currentUserRole: String) {
                                updateOrder(input: $input, currentUserId: $currentUserId, currentUserRole: $currentUserRole) {
                                    id
                                    status
                                }
                            }";

                        var updateResult = await _graphQLClient.ExecuteAsync<OrderStatusUpdateResponse>(
                            updateOrderMutation,
                            new
                            {
                                input = new
                                {
                                    id = result.Id,
                                    status = OrderStatus.Paid.ToString().ToUpperInvariant(),
                                    customerId = (int?)null
                                },
                                currentUserId = _currentSaleId,
                                currentUserRole = _currentUserRole
                            },
                            dataKey: "updateOrder");

                        finalizedStatus = updateResult?.Status ?? result.Status;
                    }
                    else if (string.IsNullOrWhiteSpace(finalizedStatus))
                    {
                        finalizedStatus = OrderStatus.New.ToString();
                    }

                    string actionLabel = markAsPaid ? "recorded" : "held";
                    SuccessMessage = $"Order #{result.Id} {actionLabel} as {finalizedStatus} — Total: {FormatCurrency(result.TotalAmount)}";
                    CartItems.Clear();
                    RecalculateTotals();

                    // Refresh stock counts after creating the order.
                    await LoadProductsAsync();
                }
                else
                {
                    ErrorMessage = "Order creation failed. Please try again.";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = markAsPaid
                    ? $"Payment failed: {ex.Message}"
                    : $"Hold order failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private void RecalculateTotals()
        {
            Subtotal = CartItems.Sum(c => c.LineTotal);
            Tax = Math.Round(Subtotal * ConfiguredTaxRate, 2);
            TotalDue = Subtotal + Tax;
            OnPropertyChanged(nameof(SubtotalDisplay));
            OnPropertyChanged(nameof(TaxDisplay));
            OnPropertyChanged(nameof(TotalDueDisplay));
        }

        partial void OnConfiguredTaxRateChanged(decimal value)
        {
            OnPropertyChanged(nameof(TaxLabel));
            RecalculateTotals();
        }

        partial void OnCurrencySymbolChanged(string value)
        {
            foreach (var product in _allProducts)
            {
                product.CurrencySymbol = value;
            }

            OnPropertyChanged(nameof(SubtotalDisplay));
            OnPropertyChanged(nameof(TaxDisplay));
            OnPropertyChanged(nameof(TotalDueDisplay));
        }

        private string FormatCurrency(decimal amount)
        {
            var number = amount.ToString("N2", CultureInfo.InvariantCulture);
            var spacing = CurrencySymbol.Length == 1 ? string.Empty : " ";
            return string.Concat(CurrencySymbol, spacing, number);
        }

        partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));
        partial void OnSuccessMessageChanged(string value) => OnPropertyChanged(nameof(HasSuccess));
    }

    // ── Item ViewModels ────────────────────────────────────────────────────────

    public partial class ProductItemViewModel : ObservableObject
    {
        public int Id { get; }
        public string Name { get; }
        public decimal Price { get; }
        public int StockQuantity { get; }
        public string CategoryName { get; }
        public string? ImageUrl { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanAddToCart))]
        private int _cartQuantity;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PriceDisplay))]
        private string _currencySymbol = "VND";

        public string PriceDisplay => FormatCurrency(Price, CurrencySymbol);
        public string StockDisplay => StockQuantity == 0 ? "Out of stock" : $"{StockQuantity} in stock";
        
        public bool IsOutOfStock => StockQuantity == 0;
        public bool IsLowStock => StockQuantity > 0 && StockQuantity <= 5;
        public string LowStockBadgeLabel => "LOW STOCK";
        public string OutOfStockBadgeLabel => "OUT OF STOCK";
        
        public bool CanAddToCart => StockQuantity > 0 && CartQuantity < StockQuantity;

        public ProductItemViewModel(Product p, string currencySymbol)
        {
            Id = p.Id;
            Name = p.Name;
            Price = p.RetailPrice;
            StockQuantity = p.StockQuantity;
            CategoryName = string.IsNullOrWhiteSpace(p.CategoryName)
                ? p.Category?.Name ?? string.Empty
                : p.CategoryName;
            ImageUrl = p.Images?.FirstOrDefault(i => i.IsPrimary)?.ImageUrl ?? p.Images?.FirstOrDefault()?.ImageUrl ?? p.ImageUrl;
            CurrencySymbol = currencySymbol;
            CartQuantity = 0;
        }

        private static string FormatCurrency(decimal amount, string currencySymbol)
        {
            var number = amount.ToString("N2", CultureInfo.InvariantCulture);
            var spacing = currencySymbol.Length == 1 ? string.Empty : " ";
            return string.Concat(currencySymbol, spacing, number);
        }
    }

    public partial class CartItemViewModel : ObservableObject
    {
        public ProductItemViewModel Product { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(LineTotal))]
        [NotifyPropertyChangedFor(nameof(CanIncreaseQuantity))]
        private int _quantity;

        public decimal LineTotal => Product.Price * Quantity;
        public bool CanIncreaseQuantity => Quantity < Product.StockQuantity;

        public CartItemViewModel(ProductItemViewModel product)
        {
            Product = product;
            Quantity = 1;
        }
    }

    // ── GraphQL response wrappers ─────────────────────────────────────────────

    public class CategoryConnection { public List<Category>? Nodes { get; set; } }

    public class ProductConnectionSimple 
    { 
        public List<Product>? Nodes { get; set; } 
        public SimplePageInfo? PageInfo { get; set; }
    }

    public class SimplePageInfo
    {
        public bool HasNextPage { get; set; }
        public string? EndCursor { get; set; }
    }

    public class OrderResult
    {
        public int Id { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
    }

    public sealed class PosAppConfigNode
    {
        public string CurrencySymbol { get; set; } = "VND";
        public decimal TaxRate { get; set; } = 0.08m;
    }
}



