using BIF.ToyStore.Core.Models;


namespace BIF.ToyStore.Core.Interfaces
{
    public interface IProductRepository : IRepository<Product>
    {
        Task<int> BulkInsertAsync(IEnumerable<Product> products);
    }
}
