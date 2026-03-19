using BIF.ToyStore.Core.Settings;

namespace BIF.ToyStore.Infrastructure.GraphQL
{
    public class InitialSetupInput
    {
        public string DisplayName { get; set; } = string.Empty;
        public string ReceiptHeader { get; set; } = string.Empty;
        public string ReceiptFooter { get; set; } = string.Empty;
        public string ThemePreference { get; set; } = "System";
        public bool EnableLoyaltyPoints { get; set; }
        public decimal TaxRate { get; set; }
    }

    public class SetupStatePayload
    {
        public bool IsInitialSetupCompleted { get; init; }
    }

    public class UpdateConfigInput
    {
        public string DisplayName { get; set; } = string.Empty;
        public decimal TaxRate { get; set; }
        public int LocalServerPort { get; set; }
        public string DatabasePath { get; set; } = string.Empty;
    }

    public class AppConfigPayload
    {
        public int Id { get; init; }
        public string DisplayName { get; init; } = string.Empty;
        public string ReceiptHeader { get; init; } = string.Empty;
        public string ReceiptFooter { get; init; } = string.Empty;
        public string ThemePreference { get; init; } = "System";
        public bool EnableLoyaltyPoints { get; init; }
        public decimal TaxRate { get; init; }
        public int LocalServerPort { get; init; }
        public string DatabasePath { get; init; } = string.Empty;
        public bool IsInitialSetupCompleted { get; init; }

        public static AppConfigPayload FromConfig(AppConfig config)
        {
            return new AppConfigPayload
            {
                Id = config.Id,
                DisplayName = config.DisplayName,
                ReceiptHeader = config.ReceiptHeader,
                ReceiptFooter = config.ReceiptFooter,
                ThemePreference = config.ThemePreference,
                EnableLoyaltyPoints = config.EnableLoyaltyPoints,
                TaxRate = config.TaxRate,
                LocalServerPort = config.LocalServerPort,
                DatabasePath = config.DatabasePath,
                IsInitialSetupCompleted = config.IsInitialSetupCompleted
            };
        }
    }
}
