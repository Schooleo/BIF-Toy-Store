using BIF.ToyStore.Core.Enums;
using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.Infrastructure.Data;
using ExcelDataReader;
using Microsoft.EntityFrameworkCore;
using HotChocolate.Types;

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
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
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

        public async Task<AppConfigPayload> CompleteInitialSetup(
            InitialSetupInput input,
            [Service] IConfigService configService)
        {
            var updatedConfig = await configService.CompleteInitialSetupAsync(new InitialSetupConfiguration
            {
                DisplayName = input.DisplayName,
                ReceiptHeader = input.ReceiptHeader,
                ReceiptFooter = input.ReceiptFooter,
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
                StockQuantity = input.StockQuantity
            };
            return await repo.AddAsync(product);
        }

        public async Task<Product> UpdateProduct(UpdateProductInput input, [Service] IProductRepository repo)
        {
            var product = await repo.GetByIdAsync(input.Id);
            if (product is null)
            {
                throw new InvalidOperationException("Product not found.");
            }
            product.Name = input.Name;
            product.CategoryId = input.CategoryId;
            product.RetailPrice = input.RetailPrice;
            product.ImportPrice = input.ImportPrice;
            product.StockQuantity = input.StockQuantity;
            
            await repo.UpdateAsync(product);
            return product;
        }

        public async Task<bool> DeleteProduct(int id, [Service] IProductRepository repo)
        {
            await repo.DeleteAsync(id);
            return true;
        }

        public async Task<ImportProductsPayload> ImportProducts(IFile file, [Service] IProductRepository repo)
        {
            var payload = new ImportProductsPayload();
            var products = new List<Product>();
            try
            {
                System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
                using var stream = file.OpenReadStream();

                using var reader = ExcelReaderFactory.CreateReader(stream);
                var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration()
                {
                    ConfigureDataTable = (_) => new ExcelDataTableConfiguration() { UseHeaderRow = true }
                });
                var table = dataSet.Tables[0];
                foreach (System.Data.DataRow row in table.Rows)
                {
                    try
                    {
                        var product = new Product
                        {
                            Name = row["Name"]?.ToString() ?? "Unknown",
                            CategoryId = Convert.ToInt32(row["CategoryId"]),
                            RetailPrice = Convert.ToDecimal(row["RetailPrice"]),
                            ImportPrice = Convert.ToDecimal(row["ImportPrice"]),
                            StockQuantity = Convert.ToInt32(row["StockQuantity"])
                        };
                        products.Add(product);
                    }
                    catch (Exception ex)
                    {
                        payload.Errors.Add($"Error parsing row (skipped): {ex.Message}");
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
    }
}

