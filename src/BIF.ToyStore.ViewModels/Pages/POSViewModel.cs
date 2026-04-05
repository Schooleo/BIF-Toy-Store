using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.ViewModels.Base;
using BIF.ToyStore.ViewModels.Messages;
using BIF.ToyStore.ViewModels.Utils;
using BIF.ToyStore.Infrastructure.GraphQL;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace BIF.ToyStore.ViewModels.Pages
{
    public partial class POSViewModel : BaseViewModel, IRecipient<LoginSucceededMessage>
    {
        private readonly IGraphQLClient _graphQLClient;
        private readonly IMessenger _messenger;
        private readonly ILocalSettingsService _localSettingsService;
        private List<ProductItemViewModel> _allProducts = new();

        // ── Bound collections ─────────────────────────────────────────────────
        [ObservableProperty]
        private ObservableCollection<ProductItemViewModel> _filteredProducts = new();

        [ObservableProperty]
        private ObservableCollection<CartItemViewModel> _cartItems = new();

        [ObservableProperty]
        private ObservableCollection<string> _categories = new() { "All" };

        [ObservableProperty]
        private string _selectedCategory = "All";

        [ObservableProperty]
        private string _selectedSort = "Default";

        // ── Totals ────────────────────────────────────────────────────────────
        [ObservableProperty]
        private decimal _subtotal;

        [ObservableProperty]
        private decimal _tax;

        [ObservableProperty]
        private decimal _totalDue;

        public string SubtotalDisplay => $"${Subtotal:F2}";
        public string TaxDisplay => $"${Tax:F2}";
        public string TotalDueDisplay => $"${TotalDue:F2}";

        // ── Sort options ──────────────────────────────────────────────────────
        public ObservableCollection<string> SortOptions { get; } = new()
        {
            "Default", "Price: Low to High", "Price: High to Low", "Name A–Z", "Stock: High to Low"
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

        private const decimal TaxRate = 0.08m;

        // ─────────────────────────────────────────────────────────────────────
        public POSViewModel(IGraphQLClient graphQLClient, ILocalSettingsService localSettingsService, IMessenger messenger)
        {
            _graphQLClient = graphQLClient;
            _localSettingsService = localSettingsService;
            _messenger = messenger;
            Title = "Point of Sale";
            _currentSaleId = _localSettingsService.GetInt(AppPreferenceKeys.CurrentUserId, 0);

            _messenger.Register(this);
        }

        public void Receive(LoginSucceededMessage message)
        {
            _currentSaleId = message.Value.Id;
            _localSettingsService.SetInt(AppPreferenceKeys.CurrentUserId, _currentSaleId);
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────
        public async Task LoadAsync()
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            try
            {
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
                Categories.Add("All");
                foreach (var c in result.Nodes)
                {
                    Categories.Add(c.Name);
                }
                
                SelectedCategory = Categories.Contains(prevCat) ? prevCat : "All";
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
                             category {
                                 id
                                 name
                             }
                             retailPrice
                             stockQuantity
                             imageUrl
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
                    _allProducts.AddRange(result.Nodes.Select(p => new ProductItemViewModel(p)));
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

        private void ApplyFilterAndSort()
        {
            IEnumerable<ProductItemViewModel> query = _allProducts;
            string currentCategory = string.IsNullOrWhiteSpace(SelectedCategory) ? "All" : SelectedCategory;

            if (!string.Equals(currentCategory, "All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(p => p.CategoryName == currentCategory);
            }

            query = SelectedSort switch
            {
                "Price: Low to High" => query.OrderBy(p => p.Price),
                "Price: High to Low" => query.OrderByDescending(p => p.Price),
                "Name A–Z" => query.OrderBy(p => p.Name),
                "Stock: High to Low" => query.OrderByDescending(p => p.StockQuantity),
                _ => query
            };

            FilteredProducts.Clear();
            foreach (var p in query)
            {
                FilteredProducts.Add(p);
            }
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
            if (CartItems.Count == 0)
            {
                ErrorMessage = "Cart is empty. Add products before processing payment.";
                return;
            }

            IsBusy = true;
            ErrorMessage = string.Empty;
            SuccessMessage = string.Empty;

            try
            {
                const string mutation = @"
                    mutation CreateOrder($input: CreateOrderInput!) {
                        createOrder(input: $input) {
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
                    new { input },
                    dataKey: "createOrder");

                if (result != null)
                {
                    SuccessMessage = $"Order #{result.Id} created — Total: ${result.TotalAmount:F2}";
                    CartItems.Clear();
                    RecalculateTotals();

                    // Refresh stock counts after order
                    await LoadProductsAsync();
                }
                else
                {
                    ErrorMessage = "Order creation failed. Please try again.";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Payment failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void HoldOrder()
        {
            // TODO: save held order to local state
        }

        [RelayCommand]
        private void AddNote()
        {
            // TODO: open note dialog
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private void RecalculateTotals()
        {
            Subtotal = CartItems.Sum(c => c.LineTotal);
            Tax = Math.Round(Subtotal * TaxRate, 2);
            TotalDue = Subtotal + Tax;
            OnPropertyChanged(nameof(SubtotalDisplay));
            OnPropertyChanged(nameof(TaxDisplay));
            OnPropertyChanged(nameof(TotalDueDisplay));
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

        public string PriceDisplay => $"${Price:F2}";
        public string StockDisplay => StockQuantity == 0 ? "Out of stock" : $"{StockQuantity} in stock";
        
        public bool IsOutOfStock => StockQuantity == 0;
        public bool IsLowStock => StockQuantity > 0 && StockQuantity <= 5;
        public string LowStockBadgeLabel => "LOW STOCK";
        public string OutOfStockBadgeLabel => "OUT OF STOCK";
        
        public bool CanAddToCart => StockQuantity > 0 && CartQuantity < StockQuantity;

        public ProductItemViewModel(Product p)
        {
            Id = p.Id;
            Name = p.Name;
            Price = p.RetailPrice;
            StockQuantity = p.StockQuantity;
            CategoryName = p.Category?.Name ?? string.Empty;
            ImageUrl = p.ImageUrl;
            CartQuantity = 0;
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
}
