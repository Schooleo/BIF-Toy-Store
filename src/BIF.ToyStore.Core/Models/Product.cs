using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;

namespace BIF.ToyStore.Core.Models
{
    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        // Foreign Key
        public int CategoryId { get; set; }
        public Category? Category { get; set; }

        [NotMapped]
        public string CategoryName { get; set; } = string.Empty;

        [NotMapped]
        public string ImageUrl { get; set; } = string.Empty;

        public decimal RetailPrice { get; set; }

        // For Income Report
        public decimal ImportPrice { get; set; }

        // For listing "Low Stock" products
        public int StockQuantity { get; set; }

        public ObservableCollection<ProductImage> Images { get; set; } = new ObservableCollection<ProductImage>();

        public bool IsDeleted { get; set; } = false;

        [NotMapped]
        public string CurrencySymbol { get; set; } = "USD";

        [NotMapped]
        public string RetailPriceDisplay => FormatCurrency(RetailPrice, CurrencySymbol);

        private static string FormatCurrency(decimal amount, string currencySymbol)
        {
            var number = amount.ToString("N2", CultureInfo.GetCultureInfo("en-US"));
            var spacing = currencySymbol.Length == 1 ? string.Empty : " ";
            return string.Concat(currencySymbol, spacing, number);
        }
    }
}
