using BIF.ToyStore.ViewModels.Base;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace BIF.ToyStore.ViewModels.Pages
{
    public partial class POSViewModel : BaseViewModel
    {
        private readonly List<ProductItemViewModel> _allProducts = new();

        [ObservableProperty]
        private ObservableCollection<ProductItemViewModel> _filteredProducts = new();

        [ObservableProperty]
        private ObservableCollection<CartItemViewModel> _cartItems = new();

        [ObservableProperty]
        private string _selectedCategory = "All";

        [ObservableProperty]
        private string _selectedSort = "Default";

        [ObservableProperty]
        private decimal _subtotal;

        [ObservableProperty]
        private decimal _tax;

        [ObservableProperty]
        private decimal _totalDue;

        public string SubtotalDisplay => $"${Subtotal:F2}";
        public string TaxDisplay => $"${Tax:F2}";
        public string TotalDueDisplay => $"${TotalDue:F2}";

        public ObservableCollection<string> Categories { get; } = new()
        {
            "All", "Action Figures", "Building Blocks", "Dolls", "Vehicles", "Plush"
        };

        public ObservableCollection<string> SortOptions { get; } = new()
        {
            "Default", "Price: Low to High", "Price: High to Low", "Name A–Z", "Stock: High to Low"
        };

        private const decimal TaxRate = 0.08m;

        public POSViewModel()
        {
            Title = "Active Workshop";
        }

        public Task LoadAsync()
        {
            LoadMockData();
            return Task.CompletedTask;
            // TODO: replace with real API call once Product/Category endpoints are merged:
            // var products = await _graphQLClient.ExecuteAsync<List<ProductDto>>(query, dataKey: "products");
        }

        private void LoadMockData()
        {
            _allProducts.Clear();
            _allProducts.Add(new ProductItemViewModel(1, "LEGO Fire Truck", 49.99m, 20, "Building Blocks", "POPULAR"));
            _allProducts.Add(new ProductItemViewModel(2, "Barbie Dreamhouse", 129.99m, 8, "Dolls", ""));
            _allProducts.Add(new ProductItemViewModel(3, "RC Car", 35.00m, 15, "Vehicles", ""));
            _allProducts.Add(new ProductItemViewModel(4, "Teddy Bear", 19.99m, 42, "Plush", "SALE"));
            _allProducts.Add(new ProductItemViewModel(5, "Action Hero Set", 24.99m, 11, "Action Figures", ""));
            _allProducts.Add(new ProductItemViewModel(6, "Mega Blocks City", 59.99m, 6, "Building Blocks", "POPULAR"));

            ApplyFilterAndSort();
        }

        partial void OnSelectedCategoryChanged(string value) => ApplyFilterAndSort();
        partial void OnSelectedSortChanged(string value) => ApplyFilterAndSort();

        private void ApplyFilterAndSort()
        {
            IEnumerable<ProductItemViewModel> query = _allProducts;

            if (!string.Equals(SelectedCategory, "All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(p => p.CategoryName == SelectedCategory);
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

        [RelayCommand]
        private void AddToCart(ProductItemViewModel? product)
        {
            if (product is null) return;

            var existing = CartItems.FirstOrDefault(c => c.Product.Id == product.Id);
            if (existing is not null)
            {
                existing.Quantity++;
            }
            else
            {
                CartItems.Add(new CartItemViewModel(product));
            }

            RecalculateTotals();
        }

        [RelayCommand]
        private void IncreaseQuantity(CartItemViewModel? item)
        {
            if (item is null) return;
            item.Quantity++;
            RecalculateTotals();
        }

        [RelayCommand]
        private void DecreaseQuantity(CartItemViewModel? item)
        {
            if (item is null) return;

            if (item.Quantity <= 1)
            {
                CartItems.Remove(item);
            }
            else
            {
                item.Quantity--;
            }

            RecalculateTotals();
        }

        [RelayCommand]
        private void RemoveFromCart(CartItemViewModel? item)
        {
            if (item is null) return;
            CartItems.Remove(item);
            RecalculateTotals();
        }

        [RelayCommand]
        private void ApplyCategoryFilter(string? category)
        {
            SelectedCategory = category ?? "All";
        }

        [RelayCommand]
        private void ApplySort(string? sort)
        {
            SelectedSort = sort ?? "Default";
        }

        [RelayCommand]
        private void ClearCart()
        {
            CartItems.Clear();
            RecalculateTotals();
        }

        // Placeholder commands — to be connected to real order logic later
        [RelayCommand]
        private void ProcessPayment() { /* TODO: integrate with order service */ }

        [RelayCommand]
        private void HoldOrder() { /* TODO: save held order */ }

        [RelayCommand]
        private void AddNote() { /* TODO: open note dialog */ }

        private void RecalculateTotals()
        {
            Subtotal = CartItems.Sum(c => c.LineTotal);
            Tax = Math.Round(Subtotal * TaxRate, 2);
            TotalDue = Subtotal + Tax;
            OnPropertyChanged(nameof(SubtotalDisplay));
            OnPropertyChanged(nameof(TaxDisplay));
            OnPropertyChanged(nameof(TotalDueDisplay));
        }
    }

    // ── Item ViewModels ────────────────────────────────────────────────────────

    public sealed class ProductItemViewModel
    {
        public int Id { get; }
        public string Name { get; }
        public decimal Price { get; }
        public int StockQuantity { get; }
        public string CategoryName { get; }
        public string BadgeLabel { get; }

        public string PriceDisplay => $"${Price:F2}";
        public string StockDisplay => $"{StockQuantity} in stock";
        public bool HasBadge => !string.IsNullOrEmpty(BadgeLabel);

        public ProductItemViewModel(int id, string name, decimal price, int stock, string category, string badge)
        {
            Id = id;
            Name = name;
            Price = price;
            StockQuantity = stock;
            CategoryName = category;
            BadgeLabel = badge;
        }
    }

    public partial class CartItemViewModel : ObservableObject
    {
        public ProductItemViewModel Product { get; }

        [ObservableProperty]
        private int _quantity;

        public decimal LineTotal => Product.Price * Quantity;

        public CartItemViewModel(ProductItemViewModel product)
        {
            Product = product;
            Quantity = 1;
        }

        partial void OnQuantityChanged(int value)
        {
            OnPropertyChanged(nameof(LineTotal));
        }
    }
}
