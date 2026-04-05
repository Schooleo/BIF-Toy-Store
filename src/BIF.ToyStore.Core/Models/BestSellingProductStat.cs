namespace BIF.ToyStore.Core.Models
{
    public class BestSellingProductStat
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public decimal RetailPrice { get; set; }
        public int UnitsSold { get; set; }
        public int Rank { get; set; }
    }
}