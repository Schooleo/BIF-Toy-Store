namespace BIF.ToyStore.Core.Models
{
    public class SaleKpiRanking
    {
        public int SaleId { get; set; }
        public string SaleName { get; set; } = string.Empty;
        public int TotalOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public int Rank { get; set; }
    }
}
