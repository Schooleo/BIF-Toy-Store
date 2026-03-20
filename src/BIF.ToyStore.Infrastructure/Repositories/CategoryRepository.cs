using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.Infrastructure.Data;

namespace BIF.ToyStore.Infrastructure.Repositories
{
    public class CategoryRepository(AppDbContext dbContext) : BaseRepository<Category>(dbContext), ICategoryRepository
    {
    }
}
