using BIF.ToyStore.Infrastructure.Data;
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
            Assert.Contains("ThemePreference", columns);
            Assert.Contains("EnableLoyaltyPoints", columns);
            Assert.Contains("IsInitialSetupCompleted", columns);

            var config = await context.AppConfigs.SingleAsync(c => c.Id == 1);
            Assert.False(config.IsInitialSetupCompleted);
            Assert.Equal("Legacy", config.DisplayName);
            Assert.Equal(0.10m, config.TaxRate);
        }

        [Fact]
        public async Task SeedAsync_NoAdmin_CreatesDefaultAdmin()
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

            var admin = await context.Users.SingleAsync(u => u.Username == "admin");
            Assert.NotEqual("admin123", admin.PasswordHash);
            Assert.True(BCrypt.Net.BCrypt.Verify("admin123", admin.PasswordHash));
        }

        private static async Task CreateLegacySchemaAsync(AppDbContext context)
        {
            var sqlCommands = new[]
            {
                "CREATE TABLE IF NOT EXISTS Users (Id INTEGER PRIMARY KEY AUTOINCREMENT, Username TEXT NOT NULL, PasswordHash TEXT NOT NULL, Role INTEGER NOT NULL);",
                "CREATE TABLE IF NOT EXISTS Categories (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NULL);",
                "CREATE TABLE IF NOT EXISTS Products (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NULL);",
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
