using BIF.ToyStore.Core.Enums;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.Core.Settings;
using Microsoft.EntityFrameworkCore;

namespace BIF.ToyStore.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext() { }
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // Tables
        public DbSet<User> Users { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderDetail> OrderDetails { get; set; }
        public DbSet<AppConfig> AppConfigs { get; set; }

        // Configure SQLite connection
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlite("Data Source=ToyStore.db");
            }
        }

        // Add rules
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User: Unique Username
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();

            modelBuilder.Entity<AppConfig>()
                .ToTable(t => t.HasCheckConstraint("CK_AppConfig_SingleRow", "Id = 1"));

            modelBuilder.Entity<AppConfig>()
                .Property(c => c.Id)
                .ValueGeneratedNever();

            // Order: Soft-delete global query filter
            modelBuilder.Entity<Order>()
                .HasQueryFilter(o => !o.IsDeleted);

            // Order: Store OrderStatus enum as integer
            modelBuilder.Entity<Order>()
                .Property(o => o.Status)
                .HasConversion<int>();

            // OrderDetail: composite relationship
            modelBuilder.Entity<OrderDetail>()
                .HasOne(d => d.Order)
                .WithMany(o => o.OrderDetails)
                .HasForeignKey(d => d.OrderId);

            modelBuilder.Entity<OrderDetail>()
                .HasOne(d => d.Product)
                .WithMany()
                .HasForeignKey(d => d.ProductId);
        }
    }
}
