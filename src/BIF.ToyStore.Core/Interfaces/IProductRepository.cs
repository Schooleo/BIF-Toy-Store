using BIF.ToyStore.Core.Models;


namespace BIF.ToyStore.Core.Interfaces
{
    public interface IProductRepository : IRepository<Product>
    {
        /// <summary>
        /// Upserts products by Name and returns the number of distinct records that were changed.
        /// </summary>
        Task<int> BulkInsertAsync(IEnumerable<Product> products);
        Task<Product> UpdateDetailsAsync(Product product);
        Task<bool> SoftDeleteAsync(int id);
        IQueryable<Product> QueryForGraphQL();
        IQueryable<Product> QueryByCategoryForGraphQL(int categoryId);
        Task<Category?> ResolveEffectiveCategoryAsync(Product product);
    }
}
