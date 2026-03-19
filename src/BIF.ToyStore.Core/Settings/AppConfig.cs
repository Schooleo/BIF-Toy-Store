namespace BIF.ToyStore.Core.Settings
{
    public class AppConfig
    {
        public int Id { get; set; } = 1;
        public string DisplayName { get; set; } = "BIF Toy Store";
        public string ReceiptHeader { get; set; } = "Welcome to BIF Toy Store";
        public string ReceiptFooter { get; set; } = "Thank you for your purchase!";
        public string ThemePreference { get; set; } = "System";
        public bool EnableLoyaltyPoints { get; set; } = true;
        public decimal TaxRate { get; set; } = 0.10m;
        public int LocalServerPort { get; set; } = 5000;
        public string DatabasePath { get; set; } = "ToyStore.db";
        public bool IsInitialSetupCompleted { get; set; } = false;
    }
}
