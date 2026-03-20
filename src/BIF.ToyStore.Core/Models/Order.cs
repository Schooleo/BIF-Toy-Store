using BIF.ToyStore.Core.Enums;

namespace BIF.ToyStore.Core.Models
{
    public class Order
    {
        public int Id { get; set; }
        public DateTime OrderDate { get; set; } = DateTime.Now;
        public OrderStatus Status { get; set; } = OrderStatus.New;
        public decimal TotalAmount { get; set; }

        // For Staff's KPI points
        public int SaleId { get; set; }
        public User? Sale { get; set; }

        public int? CustomerId { get; set; }
        public Customer? Customer { get; set; }

        public bool IsDeleted { get; set; }

        public ICollection<OrderDetail> OrderDetails { get; set; } = [];
    }
}
