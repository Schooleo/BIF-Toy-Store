using BIF.ToyStore.Core.Enums;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.Infrastructure.Data;
using BIF.ToyStore.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace BIF.ToyStore.Tests.Services
{
    public class AuthServiceTests : IDisposable
    {
        private readonly AppDbContext _dbContext;
        private readonly AuthService _authService;

        public AuthServiceTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _dbContext = new AppDbContext(options);
            _authService = new AuthService(_dbContext);
        }

        public void Dispose() => _dbContext.Dispose();

        // ─── LoginAsync ──────────────────────────────────────────────────────────

        [Fact]
        public async Task LoginAsync_ValidCredentials_ReturnsUser()
        {
            // Arrange
            _dbContext.Users.Add(new User
            {
                Id = 1,
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("pass123"),
                Role = UserRole.Admin
            });
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _authService.LoginAsync("admin", "pass123");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("admin", result.Username);
        }

        [Fact]
        public async Task LoginAsync_WrongPassword_ReturnsNull()
        {
            // Arrange
            _dbContext.Users.Add(new User
            {
                Id = 2,
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("correct"),
                Role = UserRole.Admin
            });
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _authService.LoginAsync("admin", "wrong");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task LoginAsync_WrongUsername_ReturnsNull()
        {
            // Arrange
            _dbContext.Users.Add(new User
            {
                Id = 3,
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("pass123"),
                Role = UserRole.Admin
            });
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _authService.LoginAsync("notExist", "pass123");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task LoginAsync_EmptyDatabase_ReturnsNull()
        {
            // Act
            var result = await _authService.LoginAsync("admin", "pass123");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task LoginAsync_CorrectRole_IsReturnedWithUser()
        {
            // Arrange
            _dbContext.Users.Add(new User
            {
                Id = 4,
                Username = "seller",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("pass"),
                Role = UserRole.Sale
            });
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _authService.LoginAsync("seller", "pass");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(UserRole.Sale, result.Role);
        }

        [Fact]
        public async Task LoginAsync_CaseSensitiveUsername_ReturnsNull()
        {
            // Arrange
            _dbContext.Users.Add(new User
            {
                Id = 5,
                Username = "Admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("pass"),
                Role = UserRole.Admin
            });
            await _dbContext.SaveChangesAsync();

            // Act – lowercase "admin" should NOT match "Admin"
            var result = await _authService.LoginAsync("admin", "pass");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task LoginAsync_LegacyPlainTextPassword_MigratesToBcryptAndReturnsUser()
        {
            // Arrange
            _dbContext.Users.Add(new User
            {
                Id = 6,
                Username = "legacy",
                PasswordHash = "legacy123",
                Role = UserRole.Admin
            });
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _authService.LoginAsync("legacy", "legacy123");

            // Assert
            Assert.NotNull(result);

            var updatedUser = await _dbContext.Users.SingleAsync(u => u.Username == "legacy");
            Assert.NotEqual("legacy123", updatedUser.PasswordHash);
            Assert.True(BCrypt.Net.BCrypt.Verify("legacy123", updatedUser.PasswordHash));
        }

        [Fact]
        public async Task LoginAsync_InvalidLegacySaltAndWrongPassword_ReturnsNull()
        {
            // Arrange
            _dbContext.Users.Add(new User
            {
                Id = 7,
                Username = "broken",
                PasswordHash = "not-a-bcrypt-hash",
                Role = UserRole.Admin
            });
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _authService.LoginAsync("broken", "wrong-password");

            // Assert
            Assert.Null(result);
        }

        // ─── LogoutAsync ─────────────────────────────────────────────────────────

        [Fact]
        public async Task LogoutAsync_CompleteWithoutException()
        {
            // Act & Assert – should not throw
            var exception = await Record.ExceptionAsync(() => _authService.LogoutAsync());
            Assert.Null(exception);
        }

        [Fact]
        public async Task LogoutAsync_ReturnsCompletedTask()
        {
            // Act
            var task = _authService.LogoutAsync();
            await task;

            // Assert
            Assert.True(task.IsCompletedSuccessfully);
        }
    }
}
