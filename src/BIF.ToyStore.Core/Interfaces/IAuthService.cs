using BIF.ToyStore.Core.Models;

namespace BIF.ToyStore.Core.Interfaces
{
    public interface IAuthService
    {
        Task<User?> LoginAsync(string username, string password);
        Task LogoutAsync();
    }
}