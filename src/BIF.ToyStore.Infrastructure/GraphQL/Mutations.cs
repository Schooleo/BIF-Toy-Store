using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Enums;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

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
    }
}

