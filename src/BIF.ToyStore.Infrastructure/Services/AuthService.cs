using Microsoft.EntityFrameworkCore;
using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.Infrastructure.Data;

namespace BIF.ToyStore.Infrastructure.Services
{
    public class AuthService(AppDbContext dbContext) : IAuthService
    {
        private readonly AppDbContext _dbContext = dbContext;

        public async Task<User?> LoginAsync(string username, string password)
        {
            // TODO: Implement password hash comparison
            return await _dbContext.Users
                .FirstOrDefaultAsync(u => u.Username == username && u.PasswordHash == password);
        }

        public Task LogoutAsync()
        {
            // Clear saved local settings/tokens
            return Task.CompletedTask;
        }
    }
}