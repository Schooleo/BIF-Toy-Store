using BIF.ToyStore.Infrastructure.Data;
using BIF.ToyStore.Infrastructure.Services;
using BIF.ToyStore.Core.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BIF.ToyStore.Tests.Services
{
    public class DatabaseSeederTests
    {
        [Fact]
        public async Task SeedAsync_LegacyAppConfigSchema_AddsMissingSetupColumns()
        {
            const string connectionString = "Data Source=SeederDb1;Mode=Memory;Cache=Shared";
            await using var keeperConnection = new SqliteConnection(connectionString);
            await keeperConnection.OpenAsync();

            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            await using var context = new AppDbContext(options);
            await CreateLegacySchemaAsync(context);

            await DatabaseSeeder.SeedAsync(context);

            var columns = await GetTableColumnsAsync(context, "AppConfigs");
            Assert.Contains("TaxRate", columns);
            Assert.Contains("ReceiptHeader", columns);
            Assert.Contains("ReceiptFooter", columns);
            Assert.Contains("CurrencySymbol", columns);
            Assert.Contains("ThemePreference", columns);
            Assert.Contains("EnableLoyaltyPoints", columns);
            Assert.Contains("IsInitialSetupCompleted", columns);

            var productColumns = await GetTableColumnsAsync(context, "Products");
            Assert.DoesNotContain("ImageUrl", productColumns);

            // Verify ProductImages table exists
            var productImagesColumns = await GetTableColumnsAsync(context, "ProductImages");
            Assert.Contains("ImageUrl", productImagesColumns);
            Assert.Contains("ProductId", productImagesColumns);

            var config = await context.AppConfigs.SingleAsync(c => c.Id == 1);
            Assert.False(config.IsInitialSetupCompleted);
            Assert.Equal("Legacy", config.DisplayName);
            Assert.Equal(0.10m, config.TaxRate);
            Assert.Equal("VND", config.CurrencySymbol);
        }

        [Fact]
        public async Task SeedAsync_NoUsers_CreatesDefaultAccounts()
        {
            const string connectionString = "Data Source=SeederDb2;Mode=Memory;Cache=Shared";
            await using var keeperConnection = new SqliteConnection(connectionString);
            await keeperConnection.OpenAsync();

            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            await using var context = new AppDbContext(options);
            await context.Database.EnsureCreatedAsync();

            await DatabaseSeeder.SeedAsync(context);

            Assert.Equal(3, await context.Users.CountAsync());
            Assert.Contains(context.Users, u => u.Username == "admin");
            Assert.Contains(context.Users, u => u.Username == "cashier_a");
            Assert.Contains(context.Users, u => u.Username == "cashier_b");
        }

        [Fact]
        public async Task SeedAsync_NoSaleUser_DoesNotCreateDefaultSaleUser()
        {
            const string connectionString = "Data Source=SeederDbSale;Mode=Memory;Cache=Shared";
            await using var keeperConnection = new SqliteConnection(connectionString);
            await keeperConnection.OpenAsync();

            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            await using var context = new AppDbContext(options);
            await context.Database.EnsureCreatedAsync();

            await DatabaseSeeder.SeedAsync(context);

            Assert.DoesNotContain(context.Users, u => u.Username == "sale1");
        }

        [Fact]
        public async Task SeedAsync_ExistingSaleUserWithWrongRole_DoesNotChangeRole()
        {
            const string connectionString = "Data Source=SeederDbSaleRoleFix;Mode=Memory;Cache=Shared";
            await using var keeperConnection = new SqliteConnection(connectionString);
            await keeperConnection.OpenAsync();

            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            await using var context = new AppDbContext(options);
            await context.Database.EnsureCreatedAsync();

            context.Users.Add(new BIF.ToyStore.Core.Models.User
            {
                Username = "sale1",
                PasswordHash = PasswordCipher.Encrypt("123456"),
                Role = UserRole.Admin
            });
            await context.SaveChangesAsync();

            await DatabaseSeeder.SeedAsync(context);

            var saleUser = await context.Users.SingleAsync(u => u.Username == "sale1");
            Assert.Equal(UserRole.Admin, saleUser.Role);
        }

        [Fact]
        public async Task SeedAsync_ExistingSaleUserWithWrongPassword_DoesNotMutatePassword()
        {
            const string connectionString = "Data Source=SeederDbSalePassFix;Mode=Memory;Cache=Shared";
            await using var keeperConnection = new SqliteConnection(connectionString);
            await keeperConnection.OpenAsync();

            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            await using var context = new AppDbContext(options);
            await context.Database.EnsureCreatedAsync();

            const string wrongPassword = "not-the-demo-password";

            context.Users.Add(new BIF.ToyStore.Core.Models.User
            {
                Username = "sale1",
                PasswordHash = wrongPassword,
                Role = UserRole.Sale
            });
            await context.SaveChangesAsync();

            await DatabaseSeeder.SeedAsync(context);

            var saleUser = await context.Users.SingleAsync(u => u.Username == "sale1");
            Assert.Equal(wrongPassword, saleUser.PasswordHash);
        }

        [Fact]
        public async Task SeedAsync_ExistingSaleUserLegacyPlainPassword_DoesNotMutatePassword()
        {
            const string connectionString = "Data Source=SeederDbSaleLegacy;Mode=Memory;Cache=Shared";
            await using var keeperConnection = new SqliteConnection(connectionString);
            await keeperConnection.OpenAsync();

            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            await using var context = new AppDbContext(options);
            await context.Database.EnsureCreatedAsync();

            context.Users.Add(new BIF.ToyStore.Core.Models.User
            {
                Username = "sale1",
                PasswordHash = "123456",
                Role = UserRole.Sale
            });
            await context.SaveChangesAsync();

            await DatabaseSeeder.SeedAsync(context);

            var saleUser = await context.Users.SingleAsync(u => u.Username == "sale1");
            Assert.Equal("123456", saleUser.PasswordHash);
        }

        [Fact]
        public async Task SeedAsync_EmptyDatabase_CreatesDefaultCategoriesAndProducts()
        {
            // Use a unique Data Source for this specific test
            const string connectionString = "Data Source=SeederDb3;Mode=Memory;Cache=Shared";
            await using var keeperConnection = new SqliteConnection(connectionString);
            await keeperConnection.OpenAsync();

            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            await using var context = new AppDbContext(options);
            await context.Database.EnsureCreatedAsync();

            // Run Seeder
            await DatabaseSeeder.SeedAsync(context);

            // Assert: 3 default categories + protected "Other" category, and 66 products
            var categoriesCount = await context.Categories.CountAsync();
            var productsCount = await context.Products.CountAsync();

            Assert.Equal(4, categoriesCount);
            Assert.Equal(66, productsCount);

            // Validate mapping by checking a category from seed_data.json
            var stuffedAnimalsCategory = await context.Categories
                .Include(c => c.Products)
                .SingleAsync(c => c.Name == "Stuffed Animals");

            Assert.Equal(22, stuffedAnimalsCategory.Products.Count);

            // Verify images are seeded
            var productWithImages = await context.Products
                .Include(p => p.Images)
                .FirstAsync(p => p.CategoryId == stuffedAnimalsCategory.Id);
            Assert.NotEmpty(productWithImages.Images);
        }

        private static async Task CreateLegacySchemaAsync(AppDbContext context)
        {
            var sqlCommands = new[]
            {
                "CREATE TABLE IF NOT EXISTS Users (Id INTEGER PRIMARY KEY AUTOINCREMENT, Username TEXT NOT NULL, PasswordHash TEXT NOT NULL, Role INTEGER NOT NULL);",
                "CREATE TABLE IF NOT EXISTS Categories (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NULL);",
                "CREATE TABLE IF NOT EXISTS Products (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NULL, CategoryId INTEGER NOT NULL DEFAULT 0, RetailPrice REAL NOT NULL DEFAULT 0, ImportPrice REAL NOT NULL DEFAULT 0, StockQuantity INTEGER NOT NULL DEFAULT 0);",
                "CREATE TABLE IF NOT EXISTS Customers (Id INTEGER PRIMARY KEY AUTOINCREMENT, FullName TEXT NULL);",
                "CREATE TABLE IF NOT EXISTS Orders (Id INTEGER PRIMARY KEY AUTOINCREMENT, TotalAmount REAL NOT NULL DEFAULT 0);",
                "CREATE TABLE IF NOT EXISTS OrderDetails (Id INTEGER PRIMARY KEY AUTOINCREMENT, Quantity INTEGER NOT NULL DEFAULT 0);",
                "CREATE TABLE IF NOT EXISTS AppConfigs (Id INTEGER PRIMARY KEY, DisplayName TEXT NOT NULL, LocalServerPort INTEGER NOT NULL, DatabasePath TEXT NOT NULL);",
                "INSERT INTO AppConfigs (Id, DisplayName, LocalServerPort, DatabasePath) VALUES (1, 'Legacy', 5000, 'ToyStore.db');"
            };

            foreach (var command in sqlCommands)
            {
                await context.Database.ExecuteSqlRawAsync(command);
            }
        }

        private static async Task<HashSet<string>> GetTableColumnsAsync(AppDbContext context, string tableName)
        {
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var connection = context.Database.GetDbConnection();
            await connection.OpenAsync();
            try
            {
                await using var cmd = connection.CreateCommand();
                cmd.CommandText = $"PRAGMA table_info({tableName});";

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    columns.Add(reader.GetString(1));
                }
            }
            finally
            {
                await connection.CloseAsync();
            }

            return columns;
        }
    }
}
