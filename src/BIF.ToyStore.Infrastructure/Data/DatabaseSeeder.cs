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