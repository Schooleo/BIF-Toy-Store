using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;

namespace BIF.ToyStore.Infrastructure.GraphQL
{
    public class Mutations
    {
        public async Task<User?> Login(
            string username,
            string password,
            [Service] IAuthService authService)
        {
            return await authService.LoginAsync(username, password);
        }
    }
}
