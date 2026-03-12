namespace BIF.ToyStore.Core.Models
{
    public class Customer
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public int LoyaltyPoints { get; set; }
    }
}
