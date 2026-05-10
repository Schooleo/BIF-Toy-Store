using BIF.ToyStore.Core.Models;

namespace BIF.ToyStore.Core.Interfaces
{
    public interface IAuthRepository
    {
        Task<User?> FindByUsernameAsync(string username);

        Task SaveChangesAsync();
    }
}
