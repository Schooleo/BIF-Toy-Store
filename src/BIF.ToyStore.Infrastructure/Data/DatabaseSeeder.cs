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

            bool adminExists = await dbContext.Users.AnyAsync(u => u.Role == UserRole.Admin);
            if (!adminExists)
            {
                var defaultAdmin = new User
                {
                    Username = "admin",
                    PasswordHash = "admin123",
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
                    ItemsPerPage = 10,
                    ShouldSaveLastPage = true,
                    LastOpenedPage = "Dashboard",
                    RememberMe = false,
                    SavedUsername = string.Empty,
                    LocalServerPort = 5000,
                    DatabasePath = "ToyStore.db"
                };

                dbContext.AppConfigs.Add(defaultSettings);
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
    }
}