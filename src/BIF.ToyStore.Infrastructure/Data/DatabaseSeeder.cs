using BIF.ToyStore.Core.Enums;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.Core.Settings;
using BIF.ToyStore.Infrastructure.Services;
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
            await EnsureCustomerSchemaAsync(dbContext);
            await EnsureProductSchemaAsync(dbContext);
            await EnsureProductImageSchemaAsync(dbContext);
            await EnsureOrderSchemaAsync(dbContext);
            await EnsureOrderDetailSchemaAsync(dbContext);

            await SeedUsersAsync(dbContext);

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

            await SeedCustomersAsync(dbContext);

            await EnsureOrdersSeededAsync(dbContext);
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

        private static async Task SeedUsersAsync(AppDbContext dbContext)
        {
            // Only seed users if the database is empty
            bool usersExist = await dbContext.Users.AnyAsync();
            if (usersExist)
            {
                return;
            }

            var staffUsers = new List<User>
            {
                new User
                {
                    Username = "sales_1",
                    PasswordHash = PasswordCipher.Encrypt("sales123"),
                    Role = UserRole.Sale
                },
                new User
                {
                    Username = "sales_2",
                    PasswordHash = PasswordCipher.Encrypt("sales123"),
                    Role = UserRole.Sale
                }
            };

            dbContext.Users.AddRange(staffUsers);
            await dbContext.SaveChangesAsync();
        }

        private static async Task SeedCustomersAsync(AppDbContext dbContext)
        {
            // Only seed customers if the customer table is empty
            bool customersExist = await dbContext.Customers.AnyAsync();
            if (customersExist)
            {
                return;
            }

            var customers = new List<Customer>
            {
                new Customer { FullName = "Nguyễn Thị Hương", PhoneNumber = "0912345678", LoyaltyPoints = 150 },
                new Customer { FullName = "Trần Văn Minh", PhoneNumber = "0913456789", LoyaltyPoints = 320 },
                new Customer { FullName = "Phạm Đức Anh", PhoneNumber = "0914567890", LoyaltyPoints = 85 },
                new Customer { FullName = "Lê Thị Mai", PhoneNumber = "0915678901", LoyaltyPoints = 510 },
                new Customer { FullName = "Vũ Tuấn Hùng", PhoneNumber = "0916789012", LoyaltyPoints = 200 },
                new Customer { FullName = "Đặng Thị Linh", PhoneNumber = "0917890123", LoyaltyPoints = 420 },
                new Customer { FullName = "Hoàng Văn Khoa", PhoneNumber = "0918901234", LoyaltyPoints = 175 },
                new Customer { FullName = "Bùi Thị Hoa", PhoneNumber = "0919012345", LoyaltyPoints = 350 },
                new Customer { FullName = "Dương Văn Sơn", PhoneNumber = "0920123456", LoyaltyPoints = 95 },
                new Customer { FullName = "Tô Thị Xuân", PhoneNumber = "0921234567", LoyaltyPoints = 280 },
                new Customer { FullName = "Mạc Văn Hải", PhoneNumber = "0922345678", LoyaltyPoints = 450 },
                new Customer { FullName = "Nhan Thị Yến", PhoneNumber = "0923456789", LoyaltyPoints = 125 },
                new Customer { FullName = "Cao Văn Trường", PhoneNumber = "0924567890", LoyaltyPoints = 365 },
                new Customer { FullName = "Tạ Thị Hương", PhoneNumber = "0925678901", LoyaltyPoints = 210 },
                new Customer { FullName = "Chu Văn Đạt", PhoneNumber = "0926789012", LoyaltyPoints = 540 }
            };

            dbContext.Customers.AddRange(customers);
            await dbContext.SaveChangesAsync();
        }

        private static async Task EnsureOrdersSeededAsync(AppDbContext dbContext)
        {
            bool ordersExist = await dbContext.Orders.AnyAsync();
            bool orderDetailsExist = await dbContext.OrderDetails.AnyAsync();
            if (ordersExist && orderDetailsExist)
            {
                return;
            }

            var products = await dbContext.Products.AsNoTracking().ToListAsync();
            var users = await dbContext.Users.AsNoTracking().ToListAsync();
            var customers = await dbContext.Customers.AsNoTracking().ToListAsync();

            var random = new Random();
            var endDate = DateTime.UtcNow.Date;
            var startDate = endDate.AddDays(-45);

            if (users.Count == 0 || products.Count == 0)
            {
                return;
            }

            var saleUsers = users.Where(u => u.Role == UserRole.Sale).ToList();
            if (saleUsers.Count == 0)
            {
                saleUsers = users;
            }

            var frequentlyPurchasedProductNames = new HashSet<string>(StringComparer.Ordinal)
            {
                "Chiikawa Usagi Cute Plush",
                "Duka Remote Control Supercar 24G"
            };

            int GetProductWeight(Product product)
            {
                return frequentlyPurchasedProductNames.Contains(product.Name) ? 5 : 1;
            }

            int PickWeightedProductIndex(HashSet<int> excludedIndexes)
            {
                int totalWeight = 0;
                for (int i = 0; i < products.Count; i++)
                {
                    if (excludedIndexes.Contains(i))
                    {
                        continue;
                    }

                    totalWeight += GetProductWeight(products[i]);
                }

                if (totalWeight == 0)
                {
                    return -1;
                }

                int roll = random.Next(1, totalWeight + 1);
                int cumulative = 0;
                for (int i = 0; i < products.Count; i++)
                {
                    if (excludedIndexes.Contains(i))
                    {
                        continue;
                    }

                    cumulative += GetProductWeight(products[i]);
                    if (roll <= cumulative)
                    {
                        return i;
                    }
                }

                return -1;
            }

            void PopulateOrderDetails(Order order)
            {
                int lineCount = random.Next(1, Math.Min(4, products.Count) + 1);
                var selectedProductIndexes = new HashSet<int>();
                while (selectedProductIndexes.Count < lineCount)
                {
                    int selectedIndex = PickWeightedProductIndex(selectedProductIndexes);
                    if (selectedIndex < 0)
                    {
                        break;
                    }

                    selectedProductIndexes.Add(selectedIndex);
                }

                var orderDetails = new List<OrderDetail>(lineCount);
                decimal totalAmount = 0m;
                foreach (var productIndex in selectedProductIndexes)
                {
                    var product = products[productIndex];
                    int quantity = random.Next(1, 4);
                    totalAmount += product.RetailPrice * quantity;

                    orderDetails.Add(new OrderDetail
                    {
                        OrderId = order.Id,
                        ProductId = product.Id,
                        Quantity = quantity,
                        UnitPrice = product.RetailPrice,
                        UnitImportPrice = product.ImportPrice
                    });
                }

                order.TotalAmount = totalAmount;
                order.OrderDetails = orderDetails;
            }

            if (ordersExist)
            {
                var existingOrders = await dbContext.Orders.ToListAsync();
                int pendingCount = 0;

                foreach (var order in existingOrders)
                {
                    if (order.OrderDetails.Count > 0)
                    {
                        continue;
                    }

                    PopulateOrderDetails(order);
                    dbContext.OrderDetails.AddRange(order.OrderDetails);
                    pendingCount++;

                    if (pendingCount % 200 == 0)
                    {
                        await dbContext.SaveChangesAsync();
                    }
                }

                if (pendingCount % 200 != 0)
                {
                    await dbContext.SaveChangesAsync();
                }

                return;
            }

            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                bool isWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
                int orderCount = isWeekend
                    ? random.Next(15, 26)
                    : random.Next(5, 13);

                for (int i = 0; i < orderCount; i++)
                {
                    int minutesFromOpen = random.Next(0, 12 * 60 + 1);
                    var orderDate = date.AddHours(8).AddMinutes(minutesFromOpen);

                    var statusRoll = random.Next(100);
                    var status = statusRoll < 85
                        ? OrderStatus.Paid
                        : statusRoll < 95
                            ? OrderStatus.New
                            : OrderStatus.Cancelled;

                    var saleUser = saleUsers[random.Next(saleUsers.Count)];
                    int? customerId = null;
                    bool assignCustomer = customers.Count > 0 && random.Next(100) < 70;
                    if (assignCustomer)
                    {
                        customerId = customers[random.Next(customers.Count)].Id;
                    }

                    var order = new Order
                    {
                        OrderDate = orderDate,
                        Status = status,
                        SaleId = saleUser.Id,
                        CustomerId = customerId,
                        IsDeleted = false
                    };

                    PopulateOrderDetails(order);
                    dbContext.Orders.Add(order);
                }

                await dbContext.SaveChangesAsync();
            }
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

        private static async Task EnsureCustomerSchemaAsync(AppDbContext dbContext)
        {
            const string tableName = "Customers";

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

                await EnsureColumnAsync(connection, tableName, existingColumns, "PhoneNumber", "TEXT NOT NULL DEFAULT ''");
                await EnsureColumnAsync(connection, tableName, existingColumns, "LoyaltyPoints", "INTEGER NOT NULL DEFAULT 0");
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

                await EnsureColumnAsync(connection, tableName, existingColumns, "OrderDate", "TEXT NOT NULL DEFAULT '1970-01-01T00:00:00Z'");
                await EnsureColumnAsync(connection, tableName, existingColumns, "Status", "INTEGER NOT NULL DEFAULT 0");
                await EnsureColumnAsync(connection, tableName, existingColumns, "SaleId", "INTEGER NOT NULL DEFAULT 0");
                await EnsureColumnAsync(connection, tableName, existingColumns, "CustomerId", "INTEGER NULL");
                await EnsureColumnAsync(connection, tableName, existingColumns, "TotalAmount", "REAL NOT NULL DEFAULT 0");
                await EnsureColumnAsync(connection, tableName, existingColumns, "IsDeleted", "INTEGER NOT NULL DEFAULT 0");
            }
            finally
            {
                await connection.CloseAsync();
            }
        }

        private static async Task EnsureOrderDetailSchemaAsync(AppDbContext dbContext)
        {
            const string tableName = "OrderDetails";

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

                await EnsureColumnAsync(connection, tableName, existingColumns, "OrderId", "INTEGER NOT NULL DEFAULT 0");
                await EnsureColumnAsync(connection, tableName, existingColumns, "ProductId", "INTEGER NOT NULL DEFAULT 0");
                await EnsureColumnAsync(connection, tableName, existingColumns, "UnitPrice", "REAL NOT NULL DEFAULT 0");
                await EnsureColumnAsync(connection, tableName, existingColumns, "UnitImportPrice", "REAL NOT NULL DEFAULT 0");
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
