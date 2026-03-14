using BIF.ToyStore.Core.Enums;
using BIF.ToyStore.Core.Models;

namespace BIF.ToyStore.Tests.Core
{
    // ============================================================
    //  User Model Tests
    // ============================================================
    public class UserModelTests
    {
        [Fact]
        public void User_DefaultUsername_IsEmpty()
        {
            var user = new User();
            Assert.Equal(string.Empty, user.Username);
        }

        [Fact]
        public void User_DefaultPasswordHash_IsEmpty()
        {
            var user = new User();
            Assert.Equal(string.Empty, user.PasswordHash);
        }

        [Fact]
        public void User_DefaultRole_IsAdmin()
        {
            // Admin = 0 is the default enum value
            var user = new User();
            Assert.Equal(UserRole.Admin, user.Role);
        }

        [Fact]
        public void User_CanSetAllProperties()
        {
            var user = new User
            {
                Id = 42,
                Username = "john",
                PasswordHash = "hashed",
                Role = UserRole.Sale
            };

            Assert.Equal(42, user.Id);
            Assert.Equal("john", user.Username);
            Assert.Equal("hashed", user.PasswordHash);
            Assert.Equal(UserRole.Sale, user.Role);
        }
    }

    // ============================================================
    //  Product Model Tests
    // ============================================================
    public class ProductModelTests
    {
        [Fact]
        public void Product_DefaultName_IsEmpty()
        {
            var product = new Product();
            Assert.Equal(string.Empty, product.Name);
        }

        [Fact]
        public void Product_DefaultRetailPrice_IsZero()
        {
            var product = new Product();
            Assert.Equal(0m, product.RetailPrice);
        }

        [Fact]
        public void Product_DefaultImportPrice_IsZero()
        {
            var product = new Product();
            Assert.Equal(0m, product.ImportPrice);
        }

        [Fact]
        public void Product_DefaultStockQuantity_IsZero()
        {
            var product = new Product();
            Assert.Equal(0, product.StockQuantity);
        }

        [Fact]
        public void Product_CanSetAllProperties()
        {
            var product = new Product
            {
                Id = 1,
                Name = "Lego City",
                CategoryId = 2,
                RetailPrice = 49.99m,
                ImportPrice = 30.00m,
                StockQuantity = 100
            };

            Assert.Equal(1, product.Id);
            Assert.Equal("Lego City", product.Name);
            Assert.Equal(2, product.CategoryId);
            Assert.Equal(49.99m, product.RetailPrice);
            Assert.Equal(30.00m, product.ImportPrice);
            Assert.Equal(100, product.StockQuantity);
        }

        [Fact]
        public void Product_Margin_IsCalculatedCorrectly()
        {
            var product = new Product { RetailPrice = 50m, ImportPrice = 30m };
            var margin = product.RetailPrice - product.ImportPrice;
            Assert.Equal(20m, margin);
        }
    }

    // ============================================================
    //  Category Model Tests
    // ============================================================
    public class CategoryModelTests
    {
        [Fact]
        public void Category_DefaultName_IsEmpty()
        {
            var category = new Category();
            Assert.Equal(string.Empty, category.Name);
        }

        [Fact]
        public void Category_DefaultProducts_IsEmptyCollection()
        {
            var category = new Category();
            Assert.NotNull(category.Products);
            Assert.Empty(category.Products);
        }

        [Fact]
        public void Category_CanAddProducts()
        {
            var category = new Category { Id = 1, Name = "Toys" };
            category.Products.Add(new Product { Id = 1, Name = "Ball" });
            category.Products.Add(new Product { Id = 2, Name = "Car" });

            Assert.Equal(2, category.Products.Count);
        }
    }

    // ============================================================
    //  Customer Model Tests
    // ============================================================
    public class CustomerModelTests
    {
        [Fact]
        public void Customer_DefaultFullName_IsEmpty()
        {
            var customer = new Customer();
            Assert.Equal(string.Empty, customer.FullName);
        }

        [Fact]
        public void Customer_DefaultPhoneNumber_IsEmpty()
        {
            var customer = new Customer();
            Assert.Equal(string.Empty, customer.PhoneNumber);
        }

        [Fact]
        public void Customer_DefaultLoyaltyPoints_IsZero()
        {
            var customer = new Customer();
            Assert.Equal(0, customer.LoyaltyPoints);
        }

        [Fact]
        public void Customer_CanSetAllProperties()
        {
            var customer = new Customer
            {
                Id = 5,
                FullName = "Alice Smith",
                PhoneNumber = "0901234567",
                LoyaltyPoints = 150
            };

            Assert.Equal(5, customer.Id);
            Assert.Equal("Alice Smith", customer.FullName);
            Assert.Equal("0901234567", customer.PhoneNumber);
            Assert.Equal(150, customer.LoyaltyPoints);
        }
    }

    // ============================================================
    //  Order Model Tests
    // ============================================================
    public class OrderModelTests
    {
        [Fact]
        public void Order_DefaultStatus_IsNew()
        {
            var order = new Order();
            Assert.Equal(OrderStatus.New, order.Status);
        }

        [Fact]
        public void Order_DefaultOrderDate_IsApproximatelyNow()
        {
            var before = DateTime.Now.AddSeconds(-1);
            var order = new Order();
            var after = DateTime.Now.AddSeconds(1);

            Assert.InRange(order.OrderDate, before, after);
        }

        [Fact]
        public void Order_DefaultOrderDetails_IsEmptyCollection()
        {
            var order = new Order();
            Assert.NotNull(order.OrderDetails);
            Assert.Empty(order.OrderDetails);
        }

        [Fact]
        public void Order_DefaultTotalAmount_IsZero()
        {
            var order = new Order();
            Assert.Equal(0m, order.TotalAmount);
        }

        [Fact]
        public void Order_CanChangeStatusToPaid()
        {
            var order = new Order { Status = OrderStatus.New };
            order.Status = OrderStatus.Paid;
            Assert.Equal(OrderStatus.Paid, order.Status);
        }

        [Fact]
        public void Order_CanChangeStatusToCancelled()
        {
            var order = new Order { Status = OrderStatus.New };
            order.Status = OrderStatus.Cancelled;
            Assert.Equal(OrderStatus.Cancelled, order.Status);
        }

        [Fact]
        public void Order_CanAddOrderDetails()
        {
            var order = new Order { Id = 1 };
            order.OrderDetails.Add(new OrderDetail { Id = 1, Quantity = 2, UnitPrice = 10m });
            order.OrderDetails.Add(new OrderDetail { Id = 2, Quantity = 1, UnitPrice = 30m });

            Assert.Equal(2, order.OrderDetails.Count);
        }

        [Fact]
        public void Order_NullableCustomerId_CanBeNull()
        {
            var order = new Order { CustomerId = null };
            Assert.Null(order.CustomerId);
        }
    }

    // ============================================================
    //  OrderDetail Model Tests
    // ============================================================
    public class OrderDetailModelTests
    {
        [Fact]
        public void OrderDetail_DefaultQuantity_IsZero()
        {
            var detail = new OrderDetail();
            Assert.Equal(0, detail.Quantity);
        }

        [Fact]
        public void OrderDetail_DefaultUnitPrice_IsZero()
        {
            var detail = new OrderDetail();
            Assert.Equal(0m, detail.UnitPrice);
        }

        [Fact]
        public void OrderDetail_DefaultUnitImportPrice_IsZero()
        {
            var detail = new OrderDetail();
            Assert.Equal(0m, detail.UnitImportPrice);
        }

        [Fact]
        public void OrderDetail_LineTotal_CalculatedCorrectly()
        {
            var detail = new OrderDetail { Quantity = 3, UnitPrice = 15m };
            var lineTotal = detail.Quantity * detail.UnitPrice;
            Assert.Equal(45m, lineTotal);
        }

        [Fact]
        public void OrderDetail_Profit_CalculatedCorrectly()
        {
            var detail = new OrderDetail { Quantity = 2, UnitPrice = 20m, UnitImportPrice = 12m };
            var profit = detail.Quantity * (detail.UnitPrice - detail.UnitImportPrice);
            Assert.Equal(16m, profit);
        }

        [Fact]
        public void OrderDetail_CanSetAllProperties()
        {
            var detail = new OrderDetail
            {
                Id = 1,
                OrderId = 10,
                ProductId = 5,
                Quantity = 4,
                UnitPrice = 25m,
                UnitImportPrice = 15m
            };

            Assert.Equal(1, detail.Id);
            Assert.Equal(10, detail.OrderId);
            Assert.Equal(5, detail.ProductId);
            Assert.Equal(4, detail.Quantity);
            Assert.Equal(25m, detail.UnitPrice);
            Assert.Equal(15m, detail.UnitImportPrice);
        }
    }
}
