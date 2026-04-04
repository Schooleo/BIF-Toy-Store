using BIF.ToyStore.Core.Models;

namespace BIF.ToyStore.Core.Interfaces
{
    public interface ICategoryRepository : IRepository<Category>
    {
        Task<Category> UpdateNameAsync(int id, string name);
        Task<bool> SoftDeleteAsync(int id);
        Task<Category> RestoreAsync(int id);
        IQueryable<Category> QueryForGraphQL();
    }
}
