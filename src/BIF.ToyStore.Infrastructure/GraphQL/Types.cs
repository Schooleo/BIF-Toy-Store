using BIF.ToyStore.Core.Enums;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.Core.Settings;

namespace BIF.ToyStore.Infrastructure.GraphQL
{
    public class InitialSetupInput
    {
        public string DisplayName { get; set; } = string.Empty;
        public string ReceiptHeader { get; set; } = string.Empty;
        public string ReceiptFooter { get; set; } = string.Empty;
        public string ThemePreference { get; set; } = "System";
        public bool EnableLoyaltyPoints { get; set; }
        public decimal TaxRate { get; set; }
    }

    public class SetupStatePayload
    {
        public bool IsInitialSetupCompleted { get; init; }
    }

    public class UpdateConfigInput
    {
        public string DisplayName { get; set; } = string.Empty;
        public decimal TaxRate { get; set; }
        public int LocalServerPort { get; set; }
        public string DatabasePath { get; set; } = string.Empty;
    }

    public class AppConfigPayload
    {
        public int Id { get; init; }
        public string DisplayName { get; init; } = string.Empty;
        public string ReceiptHeader { get; init; } = string.Empty;
        public string ReceiptFooter { get; init; } = string.Empty;
        public string ThemePreference { get; init; } = "System";
        public bool EnableLoyaltyPoints { get; init; }
        public decimal TaxRate { get; init; }
        public int LocalServerPort { get; init; }
        public string DatabasePath { get; init; } = string.Empty;
        public bool IsInitialSetupCompleted { get; init; }

        public static AppConfigPayload FromConfig(AppConfig config)
        {
            return new AppConfigPayload
            {
                Id = config.Id,
                DisplayName = config.DisplayName,
                ReceiptHeader = config.ReceiptHeader,
                ReceiptFooter = config.ReceiptFooter,
                ThemePreference = config.ThemePreference,
                EnableLoyaltyPoints = config.EnableLoyaltyPoints,
                TaxRate = config.TaxRate,
                LocalServerPort = config.LocalServerPort,
                DatabasePath = config.DatabasePath,
                IsInitialSetupCompleted = config.IsInitialSetupCompleted
            };
        }
    }
    public class CreateOrderDetailInput
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    public class CreateOrderInput
    {
        public int SaleId { get; set; }
        public int? CustomerId { get; set; }
        public List<CreateOrderDetailInput> Items { get; set; } = [];
    }

    public class UpdateOrderInput
    {
        public int Id { get; set; }
        public OrderStatus? Status { get; set; }
        public int? CustomerId { get; set; }
    }

    public class ProductPayload
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public decimal RetailPrice { get; init; }

        public static ProductPayload FromProduct(Product product)
        {
            return new ProductPayload
            {
                Id = product.Id,
                Name = product.Name,
                RetailPrice = product.RetailPrice
            };
        }
    }

    public class OrderDetailPayload
    {
        public int Id { get; init; }
        public int ProductId { get; init; }
        public int Quantity { get; init; }
        public decimal UnitPrice { get; init; }
        public decimal UnitImportPrice { get; init; }
        public ProductPayload? Product { get; init; }

        public static OrderDetailPayload FromOrderDetail(OrderDetail detail)
        {
            return new OrderDetailPayload
            {
                Id = detail.Id,
                ProductId = detail.ProductId,
                Quantity = detail.Quantity,
                UnitPrice = detail.UnitPrice,
                UnitImportPrice = detail.UnitImportPrice,
                Product = detail.Product is not null
                    ? ProductPayload.FromProduct(detail.Product)
                    : null
            };
        }
    }

    public class OrderPayload
    {
        public int Id { get; init; }
        public DateTime OrderDate { get; init; }
        public OrderStatus Status { get; init; }
        public decimal TotalAmount { get; init; }
        public int SaleId { get; init; }
        public string? SaleName { get; init; }
        public int? CustomerId { get; init; }
        public string? CustomerName { get; init; }
        public List<OrderDetailPayload> OrderDetails { get; init; } = [];

        public static OrderPayload FromOrder(Order order)
        {
            return new OrderPayload
            {
                Id = order.Id,
                OrderDate = order.OrderDate,
                Status = order.Status,
                TotalAmount = order.TotalAmount,
                SaleId = order.SaleId,
                SaleName = order.Sale?.Username,
                CustomerId = order.CustomerId,
                CustomerName = order.Customer?.FullName,
                OrderDetails = order.OrderDetails
                    .Select(OrderDetailPayload.FromOrderDetail)
                    .ToList()
            };
        }
    }

    public class OrderListPayload
    {
        public List<OrderPayload> Items { get; init; } = [];
        public int TotalCount { get; init; }
        public int Page { get; init; }
        public int PageSize { get; init; }
    }

    public class CreateProductInput
    {
        public string Name { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public decimal RetailPrice { get; set; }
        public decimal ImportPrice { get; set; }
        public int StockQuantity { get; set; }
    }

    public class UpdateProductInput : CreateProductInput
    {
        public int Id { get; set; }
    }

    public class ImportProductsPayload
    {
        public int ImportedCount { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    public class UserListItemPayload
    {
        public int Id { get; init; }
        public string Username { get; init; } = string.Empty;
        public UserRole Role { get; init; }

        public static UserListItemPayload FromUser(User user)
        {
            return new UserListItemPayload
            {
                Id = user.Id,
                Username = user.Username,
                Role = user.Role
            };
        }
    }

    public class UserPayload
    {
        public int Id { get; init; }
        public string Username { get; init; } = string.Empty;
        public string PasswordHash { get; init; } = string.Empty;
        public UserRole Role { get; init; }
    }

    public class SaleKpiRankingPayload
    {
        public int SaleId { get; init; }
        public string SaleName { get; init; } = string.Empty;
        public int TotalOrders { get; init; }
        public decimal TotalRevenue { get; init; }
        public int Rank { get; init; }

        public static SaleKpiRankingPayload FromModel(SaleKpiRanking model)
        {
            return new SaleKpiRankingPayload
            {
                SaleId = model.SaleId,
                SaleName = model.SaleName,
                TotalOrders = model.TotalOrders,
                TotalRevenue = model.TotalRevenue,
                Rank = model.Rank
            };
        }
    }

    public class RevenueTrendPointPayload
    {
        public string DayLabel { get; init; } = string.Empty;
        public decimal Revenue { get; init; }

        public static RevenueTrendPointPayload FromModel(RevenueTrendPoint model)
        {
            return new RevenueTrendPointPayload
            {
                DayLabel = $"Day {model.Date.Day}",
                Revenue = model.Revenue
            };
        }
    }

    public class BestSellingProductPayload
    {
        public int ProductId { get; init; }
        public string ProductName { get; init; } = string.Empty;
        public string CategoryName { get; init; } = string.Empty;
        public decimal RetailPrice { get; init; }
        public int UnitsSold { get; init; }
        public int Rank { get; init; }

        public static BestSellingProductPayload FromModel(BestSellingProductStat model)
        {
            return new BestSellingProductPayload
            {
                ProductId = model.ProductId,
                ProductName = model.ProductName,
                CategoryName = model.CategoryName,
                RetailPrice = model.RetailPrice,
                UnitsSold = model.UnitsSold,
                Rank = model.Rank
            };
        }
    }
}