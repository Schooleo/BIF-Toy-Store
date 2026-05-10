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
            await EnsureCategorySchemaAsync(dbContext);
            await EnsureProductSchemaAsync(dbContext);
            await EnsureProductImageSchemaAsync(dbContext);
            await EnsureOrderSchemaAsync(dbContext);

            bool configExists = await dbContext.AppConfigs.AnyAsync(c => c.Id == 1);
            if (!configExists)
            {
                var defaultSettings = new AppConfig
                {
                    Id = 1,
                    DisplayName = "BIF Toy Store",
                    ReceiptHeader = "Welcome to BIF Toy Store",
                    ReceiptFooter = "Thank you for your purchase!",
                    CurrencySymbol = "VND",
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
                if (string.IsNullOrWhiteSpace(config.CurrencySymbol))
                {
                    config.CurrencySymbol = "VND";
                }
                await dbContext.SaveChangesAsync();
            }

            await SeedCategoriesAndProductsAsync(dbContext);
        }

        private static async Task SeedCategoriesAndProductsAsync(AppDbContext dbContext)
        {
            // Ensure the 'Other' category always exists and cannot be deleted
            bool otherExists = await dbContext.Categories.IgnoreQueryFilters().AnyAsync(c => c.Id == AppConstants.OtherCategoryId);
            if (!otherExists)
            {
                var otherCategory = new Category { Id = AppConstants.OtherCategoryId, Name = "Other", IsDeleted = false };
                dbContext.Categories.Add(otherCategory);
                await dbContext.SaveChangesAsync();
            }

            bool categoryExists = await dbContext.Categories.AnyAsync(c => c.Id != AppConstants.OtherCategoryId);
            if (!categoryExists)
            {
                string jsonPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "seed_data.json");
                if (!File.Exists(jsonPath))
                {
                    // Fallback for different environments
                    jsonPath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Data", "seed_data.json");
                }

                if (File.Exists(jsonPath))
                {
                    var json = await File.ReadAllTextAsync(jsonPath);
                    var seedData = System.Text.Json.JsonSerializer.Deserialize<List<CategorySeedDto>>(json);

                    if (seedData != null)
                    {
                        foreach (var categoryDto in seedData)
                        {
                            var category = new Category { Name = categoryDto.CategoryName };
                            dbContext.Categories.Add(category);
                            await dbContext.SaveChangesAsync();

                            foreach (var productDto in categoryDto.Products)
                            {
                                var product = new Product
                                {
                                    Name = productDto.Name,
                                    CategoryId = category.Id,
                                    RetailPrice = productDto.RetailPrice,
                                    ImportPrice = productDto.ImportPrice,
                                    StockQuantity = productDto.StockQuantity,
                                    IsDeleted = false
                                };

                                dbContext.Products.Add(product);
                                await dbContext.SaveChangesAsync();

                                if (productDto.Images != null)
                                {
                                    for (int i = 0; i < productDto.Images.Count; i++)
                                    {
                                        var productImage = new ProductImage
                                        {
                                            ProductId = product.Id,
                                            ImageUrl = productDto.Images[i],
                                            DisplayOrder = i,
                                            IsPrimary = i == 0
                                        };
                                        dbContext.ProductImages.Add(productImage);
                                    }
                                    await dbContext.SaveChangesAsync();
                                }
                            }
                        }
                    }
                }
            }

            // Force exactly 2 products to have zero stock for testing critical stock UI states
            // This runs every time, regardless of whether seeding occurred
            var productsToSetToZeroStock = new[] { "Panda Stuffed Animal 22cm", "Ford GT RC 1-24 Blue R78200" };
            foreach (var productName in productsToSetToZeroStock)
            {
                var product = await dbContext.Products.FirstOrDefaultAsync(p => p.Name == productName);
                if (product != null && product.StockQuantity != 0)
                {
                    product.StockQuantity = 0;
                    await dbContext.SaveChangesAsync();
                }
            }

            // Force exactly 3 products to have low stock (1-2) for testing low stock warning states
            var productsToSetToLowStock = new[]
            {
                "Silly Face Duck Plush",
                "Graduation Bear Plush",
                "Busy Pig Plush 40cm"
            };
            foreach (var productName in productsToSetToLowStock)
            {
                var product = await dbContext.Products.FirstOrDefaultAsync(p => p.Name == productName);
                if (product != null && product.StockQuantity > 2)
                {
                    product.StockQuantity = 2;
                    await dbContext.SaveChangesAsync();
                }
            }

            // Ensure all remaining products have stock between 15 and 50
            var allProducts = await dbContext.Products.ToListAsync();
            var criticalAndLowStockNames = productsToSetToZeroStock.Concat(productsToSetToLowStock).ToHashSet();
            
            foreach (var product in allProducts)
            {
                // Skip products already configured for critical or low stock
                if (criticalAndLowStockNames.Contains(product.Name))
                    continue;

                // Adjust stock to be within 15-50 range
                if (product.StockQuantity < 15)
                {
                    product.StockQuantity = 15;
                }
                else if (product.StockQuantity > 50)
                {
                    product.StockQuantity = 50;
                }
            }
            await dbContext.SaveChangesAsync();
        }

        private class CategorySeedDto
        {
            public string CategoryName { get; set; } = string.Empty;
            public List<ProductSeedDto> Products { get; set; } = new();
        }

        private class ProductSeedDto
        {
            public string Name { get; set; } = string.Empty;
            public decimal RetailPrice { get; set; }
            public decimal ImportPrice { get; set; }
            public int StockQuantity { get; set; }
            public List<string> Images { get; set; } = new();
        }

        /// <summary>
        /// Returns false if any EF-tracked table is absent from the SQLite schema
        /// </summary>
        private static async Task<bool> AllTablesExistAsync(AppDbContext dbContext)
        {
            var expectedTables = new[]
            {
                "Users", "Categories", "Products", "ProductImages",
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
                await EnsureColumnAsync(connection, tableName, existingColumns, "CurrencySymbol", "TEXT NOT NULL DEFAULT 'VND'");
                await EnsureColumnAsync(connection, tableName, existingColumns, "ThemePreference", "TEXT NOT NULL DEFAULT 'System'");
                await EnsureColumnAsync(connection, tableName, existingColumns, "EnableLoyaltyPoints", "INTEGER NOT NULL DEFAULT 1");
                await EnsureColumnAsync(connection, tableName, existingColumns, "IsInitialSetupCompleted", "INTEGER NOT NULL DEFAULT 0");
            }
            finally
            {
                await connection.CloseAsync();
            }
        }

        private static async Task EnsureCategorySchemaAsync(AppDbContext dbContext)
        {
            const string tableName = "Categories";

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

                await EnsureColumnAsync(connection, tableName, existingColumns, "IsDeleted", "INTEGER NOT NULL DEFAULT 0");
            }
            finally
            {
                await connection.CloseAsync();
            }
        }

        private static async Task EnsureProductSchemaAsync(AppDbContext dbContext)
        {
            const string tableName = "Products";

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

                await EnsureColumnAsync(connection, tableName, existingColumns, "IsDeleted", "INTEGER NOT NULL DEFAULT 0");
            }
            finally
            {
                await connection.CloseAsync();
            }
        }

        private static async Task EnsureProductImageSchemaAsync(AppDbContext dbContext)
        {
            var connection = dbContext.Database.GetDbConnection();
            await connection.OpenAsync();

            try
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS ProductImages (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        ProductId INTEGER NOT NULL,
                        ImageUrl TEXT NOT NULL,
                        DisplayOrder INTEGER NOT NULL,
                        IsPrimary INTEGER NOT NULL,
                        CONSTRAINT FK_ProductImages_Products_ProductId FOREIGN KEY (ProductId) REFERENCES Products (Id) ON DELETE CASCADE
                    );
                    CREATE INDEX IF NOT EXISTS IX_ProductImages_ProductId ON ProductImages (ProductId);";
                await cmd.ExecuteNonQueryAsync();
            }
            finally
            {
                await connection.CloseAsync();
            }
        }

        private static async Task EnsureOrderSchemaAsync(AppDbContext dbContext)
        {
            const string tableName = "Orders";

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

                await EnsureColumnAsync(connection, tableName, existingColumns, "IsDeleted", "INTEGER NOT NULL DEFAULT 0");
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
