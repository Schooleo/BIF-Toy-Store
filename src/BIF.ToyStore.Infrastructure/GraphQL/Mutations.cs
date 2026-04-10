using BIF.ToyStore.Core.Enums;
using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.Infrastructure.Data;
using ExcelDataReader;
using Microsoft.EntityFrameworkCore;
using HotChocolate.Types;
using BIF.ToyStore.Core.Settings;

namespace BIF.ToyStore.Infrastructure.GraphQL
{
    public class Mutations
    {
        public async Task<LoginUser?> Login(
            string username,
            string password,
            [Service] IAuthService authService)
        {
            var user = await authService.LoginAsync(username, password);
            if (user is null)
            {
                return null;
            }

            return new LoginUser
            {
                Id = user.Id,
                Username = user.Username,
                Role = user.Role
            };
        }

        public async Task<LoginUser> CreateUser(
            string username,
            string password,
            UserRole role,
            [Service] AppDbContext dbContext)
        {
            bool userExists = await dbContext.Users.AnyAsync(u => u.Username == username);
            if (userExists)
            {
                throw new InvalidOperationException("Username already exists.");
            }

            var user = new User
            {
                Username = username,
                PasswordHash = Services.PasswordCipher.Encrypt(password),
                Role = role
            };

            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();

            return new LoginUser
            {
                Id = user.Id,
                Username = user.Username,
                Role = user.Role
            };
        }

        public async Task<bool> DeleteUser(
            int id,
            [Service] AppDbContext dbContext)
        {
            var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user is null)
            {
                return false;
            }

            dbContext.Users.Remove(user);
            await dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<LoginUser> UpdateUser(
            int id,
            string username,
            string password,
            [Service] AppDbContext dbContext)
        {
            var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == id)
                ?? throw new InvalidOperationException("User not found.");

            bool duplicatedUsername = await dbContext.Users.AnyAsync(u => u.Username == username && u.Id != id);
            if (duplicatedUsername)
            {
                throw new InvalidOperationException("Username already exists.");
            }

            user.Username = username;
            user.PasswordHash = Services.PasswordCipher.Encrypt(password);

            await dbContext.SaveChangesAsync();

            return new LoginUser
            {
                Id = user.Id,
                Username = user.Username,
                Role = user.Role
            };
        }

        public async Task<AppConfigPayload> UpdateConfig(
            UpdateConfigInput input,
            [Service] IConfigService configService)
        {
            var updatedConfig = await configService.UpdateConfigAsync(
                input.DisplayName,
                input.TaxRate,
                input.LocalServerPort,
                input.DatabasePath);

            return AppConfigPayload.FromConfig(updatedConfig);
        }

        public async Task<AppConfigPayload> UpdateStoreSettings(
            UpdateStoreSettingsInput input,
            [Service] IConfigService configService)
        {
            var updatedConfig = await configService.UpdateStoreSettingsAsync(
                input.DisplayName,
                input.TaxRate,
                input.CurrencySymbol,
                input.ReceiptHeader,
                input.ReceiptFooter,
                input.ThemePreference);

            return AppConfigPayload.FromConfig(updatedConfig);
        }

        public async Task<AppConfigPayload> CompleteInitialSetup(
            InitialSetupInput input,
            [Service] IConfigService configService)
        {
            var updatedConfig = await configService.CompleteInitialSetupAsync(new InitialSetupConfiguration
            {
                DisplayName = input.DisplayName,
                ReceiptHeader = input.ReceiptHeader,
                ReceiptFooter = input.ReceiptFooter,
                CurrencySymbol = input.CurrencySymbol,
                ThemePreference = input.ThemePreference,
                EnableLoyaltyPoints = input.EnableLoyaltyPoints,
                TaxRate = input.TaxRate
            });

            return AppConfigPayload.FromConfig(updatedConfig);
        }
        public async Task<OrderPayload> CreateOrder(
            CreateOrderInput input,
            [Service] IOrderService orderService)
        {
            var items = input.Items
                .Select(i => (i.ProductId, i.Quantity, i.UnitPrice))
                .ToList();

            var order = await orderService.CreateOrderAsync(
                input.SaleId,
                input.CustomerId,
                items);

            return OrderPayload.FromOrder(order);
        }

        public async Task<OrderPayload> UpdateOrder(
            UpdateOrderInput input,
            [Service] IOrderService orderService)
        {
            var order = await orderService.UpdateOrderAsync(
                input.Id,
                input.Status,
                input.CustomerId);

            return OrderPayload.FromOrder(order);
        }

        public async Task<bool> DeleteOrder(
            int id,
            [Service] IOrderService orderService)
        {
            return await orderService.DeleteOrderAsync(id);
        }

        public async Task<Product> CreateProduct(CreateProductInput input, [Service] IProductRepository repo)
        {
            var product = new Product
            {
                Name = input.Name,
                CategoryId = input.CategoryId,
                RetailPrice = input.RetailPrice,
                ImportPrice = input.ImportPrice,
                StockQuantity = input.StockQuantity,
                ImageUrl = input.ImageUrl
            };
            return await repo.AddAsync(product);
        }

        public async Task<Product> UpdateProduct(UpdateProductInput input, [Service] IProductRepository repo)
        {
            var product = new Product
            {
                Id = input.Id,
                Name = input.Name,
                CategoryId = input.CategoryId,
                RetailPrice = input.RetailPrice,
                ImportPrice = input.ImportPrice,
                StockQuantity = input.StockQuantity,
                ImageUrl = input.ImageUrl
            };

            return await repo.UpdateDetailsAsync(product);
        }

        public async Task<bool> DeleteProduct(int id, [Service] IProductRepository repo)
        {
            return await repo.SoftDeleteAsync(id);
        }

        public async Task<ImportProductsPayload> ImportProducts(
            IFile file,
            [Service] IProductRepository repo,
            [Service] AppDbContext dbContext)
        {
            var payload = new ImportProductsPayload();
            var products = new List<Product>();

            try
            {
                var categoryMap = await dbContext.Categories
                    .AsNoTracking()
                    .Select(c => new { c.Id, c.Name })
                    .ToListAsync();

                var categoryByName = categoryMap
                    .Where(c => !string.IsNullOrWhiteSpace(c.Name))
                    .GroupBy(c => c.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

                using var stream = file.OpenReadStream();

                using var reader = ExcelReaderFactory.CreateReader(stream);
                var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration()
                {
                    ConfigureDataTable = (_) => new ExcelDataTableConfiguration() { UseHeaderRow = true }
                });

                var table = dataSet.Tables[0];
                var rowIndex = 1;

                foreach (System.Data.DataRow row in table.Rows)
                {
                    rowIndex++;
                    var productName = row["Name"]?.ToString()?.Trim() ?? string.Empty;

                    try
                    {
                        if (string.IsNullOrWhiteSpace(productName))
                        {
                            throw new FormatException("Product name is required.");
                        }

                        var categoryName = row["CategoryName"]?.ToString()?.Trim();
                        var categoryId = AppConstants.OtherCategoryId;
                        if (!string.IsNullOrWhiteSpace(categoryName)
                            && categoryByName.TryGetValue(categoryName, out var mappedCategoryId))
                        {
                            categoryId = mappedCategoryId;
                        }

                        if (!decimal.TryParse(row["RetailPrice"]?.ToString(), out var retailPrice))
                        {
                            throw new FormatException("RetailPrice is invalid.");
                        }

                        if (!decimal.TryParse(row["ImportPrice"]?.ToString(), out var importPrice))
                        {
                            throw new FormatException("ImportPrice is invalid.");
                        }

                        if (!int.TryParse(row["StockQuantity"]?.ToString(), out var stockQuantity))
                        {
                            throw new FormatException("StockQuantity is invalid.");
                        }

                        var product = new Product
                        {
                            Name = productName,
                            CategoryId = categoryId,
                            RetailPrice = retailPrice,
                            ImportPrice = importPrice,
                            StockQuantity = stockQuantity
                        };

                        products.Add(product);
                    }
                    catch (Exception ex)
                    {
                        var safeName = string.IsNullOrWhiteSpace(productName) ? "Unknown" : productName;
                        payload.Errors.Add($"Row {rowIndex}: [{safeName}] - {ex.Message}");
                    }
                }

                if (products.Count > 0)
                {
                    payload.ImportedCount = await repo.BulkInsertAsync(products);
                }
            }
            catch (Exception ex)
            {
                payload.Errors.Add($"Error processing file: {ex.Message}");
            }
            return payload;
        }

        public async Task<Category> CreateCategory(CreateCategoryInput input, [Service] ICategoryRepository repo)
        {
            var category = new Category
            {
                Name = input.Name
            };
            return await repo.AddAsync(category);
        }

        public async Task<Category> UpdateCategory(UpdateCategoryInput input, [Service] ICategoryRepository repo)
        {
            return await repo.UpdateNameAsync(input.Id, input.Name);
        }

        public async Task<bool> DeleteCategory(int id, [Service] ICategoryRepository repo)
        {
            return await repo.SoftDeleteAsync(id);
        }

        public async Task<Category> RestoreCategory(int id, [Service] ICategoryRepository repo)
        {
            return await repo.RestoreAsync(id);
        }
    }
}
