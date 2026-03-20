using BIF.ToyStore.Core.Enums;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.Core.Settings;
using Microsoft.EntityFrameworkCore;

namespace BIF.ToyStore.Infrastructure.Data
{
    public static class DatabaseSeeder
    {
        public static async Task SeedAsync(AppDbContext dbContext)
        {
            bool schemaIsStale = !await dbContext.Database.CanConnectAsync()
                || !await AllTablesExistAsync(dbContext);

            if (schemaIsStale)
            {
                await dbContext.Database.EnsureDeletedAsync();
            }

            await dbContext.Database.EnsureCreatedAsync();
            await EnsureAppConfigSchemaAsync(dbContext);

            bool adminExists = await dbContext.Users.AnyAsync(u => u.Role == UserRole.Admin);
            if (!adminExists)
            {
                var defaultAdmin = new User
                {
                    Username = "admin",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                    Role = UserRole.Admin
                };

                dbContext.Users.Add(defaultAdmin);
                await dbContext.SaveChangesAsync();
            }

            bool configExists = await dbContext.AppConfigs.AnyAsync(c => c.Id == 1);
            if (!configExists)
            {
                var defaultSettings = new AppConfig
                {
                    Id = 1,
                    DisplayName = "BIF Toy Store",
                    ReceiptHeader = "Welcome to BIF Toy Store",
                    ReceiptFooter = "Thank you for your purchase!",
                    ThemePreference = "System",
                    EnableLoyaltyPoints = true,
                    TaxRate = 0.10m,
                    LocalServerPort = 5000,
                    DatabasePath = "ToyStore.db",
                    IsInitialSetupCompleted = false
                };

                dbContext.AppConfigs.Add(defaultSettings);
                await dbContext.SaveChangesAsync();
            }
            else
            {
                var config = await dbContext.AppConfigs.SingleAsync(c => c.Id == 1);
                config.Id = 1;
                await dbContext.SaveChangesAsync();
            }

            await SeedCategoriesAndProductsAsync(dbContext);
        }

        private static async Task SeedCategoriesAndProductsAsync(AppDbContext dbContext)
        {
            bool categoryExists = await dbContext.Categories.AnyAsync();
            if (categoryExists)
            {
                return; 
            }
            // 1. Create Sample Categories
            var category1 = new Category { Name = "Action Figures" };
            var category2 = new Category { Name = "Lego Sets" };
            var category3 = new Category { Name = "Board Games" };
            dbContext.Categories.AddRange(category1, category2, category3);
            await dbContext.SaveChangesAsync();
            // 2. Create Sample Products
            var products = new List<Product>
            {
                // Action Figures (Id của category1)
                new Product { Name = "Ferrari 1:18 Diecast Model", CategoryId = category1.Id, RetailPrice = 45.99m, ImportPrice = 20.00m, StockQuantity = 10 },
                new Product { Name = "Gundam RX-78-2 Master Grade", CategoryId = category1.Id, RetailPrice = 65.50m, ImportPrice = 35.00m, StockQuantity = 5 },
                new Product { Name = "Transformers Optimus Prime", CategoryId = category1.Id, RetailPrice = 39.99m, ImportPrice = 18.00m, StockQuantity = 22 },
                new Product { Name = "Marvel Legends Iron Man", CategoryId = category1.Id, RetailPrice = 24.99m, ImportPrice = 10.00m, StockQuantity = 40 },
                new Product { Name = "Star Wars Darth Vader Helmet", CategoryId = category1.Id, RetailPrice = 59.90m, ImportPrice = 28.50m, StockQuantity = 8 },
                
                // Lego Sets (Id của category2)
                new Product { Name = "Lego Ninjago Master Dragon", CategoryId = category2.Id, RetailPrice = 89.99m, ImportPrice = 50.00m, StockQuantity = 12 },
                new Product { Name = "Lego City Fire Station", CategoryId = category2.Id, RetailPrice = 119.99m, ImportPrice = 75.00m, StockQuantity = 3 },
                new Product { Name = "Lego Technic McLaren F1", CategoryId = category2.Id, RetailPrice = 199.99m, ImportPrice = 110.00m, StockQuantity = 5 },
                new Product { Name = "Lego Star Wars Millennium Falcon", CategoryId = category2.Id, RetailPrice = 159.99m, ImportPrice = 90.00m, StockQuantity = 2 },
                new Product { Name = "Lego Creator 3-in-1 Dinosaur", CategoryId = category2.Id, RetailPrice = 29.99m, ImportPrice = 12.00m, StockQuantity = 30 },
                
                // Board Games (Id của category3)
                new Product { Name = "Monopoly Classic Edition", CategoryId = category3.Id, RetailPrice = 19.99m, ImportPrice = 10.50m, StockQuantity = 50 },
                new Product { Name = "Catan Base Game", CategoryId = category3.Id, RetailPrice = 44.99m, ImportPrice = 22.00m, StockQuantity = 15 },
                new Product { Name = "Ticket to Ride Classic", CategoryId = category3.Id, RetailPrice = 49.99m, ImportPrice = 25.00m, StockQuantity = 18 },
                new Product { Name = "Scrabble Original", CategoryId = category3.Id, RetailPrice = 21.50m, ImportPrice = 9.00m, StockQuantity = 25 },
                new Product { Name = "Chess Set Premium Wood", CategoryId = category3.Id, RetailPrice = 35.00m, ImportPrice = 15.00m, StockQuantity = 7 }
            };
            dbContext.Products.AddRange(products);
            await dbContext.SaveChangesAsync();
        }

        /// <summary>
        /// Returns false if any EF-tracked table is absent from the SQLite schema
        /// </summary>
        private static async Task<bool> AllTablesExistAsync(AppDbContext dbContext)
        {
            var expectedTables = new[]
            {
                "Users", "Categories", "Products",
                "Customers", "Orders", "OrderDetails", "AppConfigs"
            };

            var connection = dbContext.Database.GetDbConnection();
            await connection.OpenAsync();

            try
            {
                foreach (var table in expectedTables)
                {
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText =
                        $"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{table}'";
                    var result = await cmd.ExecuteScalarAsync();
                    if (Convert.ToInt64(result) == 0) return false;
                }
                return true;
            }
            finally
            {
                await connection.CloseAsync();
            }
        }

        private static async Task EnsureAppConfigSchemaAsync(AppDbContext dbContext)
        {
            const string tableName = "AppConfigs";

            var connection = dbContext.Database.GetDbConnection();
            await connection.OpenAsync();

            try
            {
                var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = $"PRAGMA table_info({tableName});";

                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        existingColumns.Add(reader.GetString(1));
                    }
                }

                await EnsureColumnAsync(connection, tableName, existingColumns, "TaxRate", "REAL NOT NULL DEFAULT 0.10");
                await EnsureColumnAsync(connection, tableName, existingColumns, "ReceiptHeader", "TEXT NOT NULL DEFAULT ''");
                await EnsureColumnAsync(connection, tableName, existingColumns, "ReceiptFooter", "TEXT NOT NULL DEFAULT ''");
                await EnsureColumnAsync(connection, tableName, existingColumns, "ThemePreference", "TEXT NOT NULL DEFAULT 'System'");
                await EnsureColumnAsync(connection, tableName, existingColumns, "EnableLoyaltyPoints", "INTEGER NOT NULL DEFAULT 1");
                await EnsureColumnAsync(connection, tableName, existingColumns, "IsInitialSetupCompleted", "INTEGER NOT NULL DEFAULT 0");
            }
            finally
            {
                await connection.CloseAsync();
            }
        }

        private static async Task EnsureColumnAsync(
            System.Data.Common.DbConnection connection,
            string tableName,
            HashSet<string> existingColumns,
            string columnName,
            string columnDefinition)
        {
            if (existingColumns.Contains(columnName))
            {
                return;
            }

            using var alterCmd = connection.CreateCommand();
            alterCmd.CommandText =
                $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};";
            await alterCmd.ExecuteNonQueryAsync();
            existingColumns.Add(columnName);
        }
    }
}