namespace BIF.ToyStore.Core.Models
{
    public class InitialSetupConfiguration
    {
        public string DisplayName { get; set; } = string.Empty;
        public string ReceiptHeader { get; set; } = string.Empty;
        public string ReceiptFooter { get; set; } = string.Empty;
        public string ThemePreference { get; set; } = "System";
        public bool EnableLoyaltyPoints { get; set; }
        public decimal TaxRate { get; set; }
    }
}
