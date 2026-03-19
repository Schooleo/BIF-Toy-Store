using Microsoft.EntityFrameworkCore;
using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.Infrastructure.Data;
using BCrypt.Net;

namespace BIF.ToyStore.Infrastructure.Services
{
    public class AuthService(AppDbContext dbContext) : IAuthService
    {
        private readonly AppDbContext _dbContext = dbContext;

        public async Task<User?> LoginAsync(string username, string password)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user is null)
            {
                return null;
            }

            try
            {
                return BCrypt.Net.BCrypt.Verify(password, user.PasswordHash) ? user : null;
            }
            catch (SaltParseException)
            {
                // Backward compatibility for legacy records that still store plain text.
                if (!string.Equals(user.PasswordHash, password, StringComparison.Ordinal))
                {
                    return null;
                }

                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
                await _dbContext.SaveChangesAsync();
                return user;
            }
        }

        public Task LogoutAsync()
        {
            // Clear saved local settings/tokens
            return Task.CompletedTask;
        }
    }
}