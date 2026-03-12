namespace BIF.ToyStore.Core.Models
{
    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        // Foreign Key
        public int CategoryId { get; set; }
        public Category? Category { get; set; }

        public decimal RetailPrice { get; set; }

        // For Income Report
        public decimal ImportPrice { get; set; }

        // For listing "Low Stock" products
        public int StockQuantity { get; set; }
    }
}
