using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.Infrastructure.Services;

namespace BIF.ToyStore.Tests.Services
{
    public class CategoryServiceTests
    {
        [Fact]
        public async Task GetCategoriesAsync_ForwardsQueryToRepository()
        {
            var expectedResult = new CategoryListResult
            {
                TotalCount = 1,
                HasNextPage = false,
                HasPreviousPage = true,
                StartCursor = "s1",
                EndCursor = "e1",
                Items = [new Category { Id = 1, Name = "Lego" }]
            };

            var repository = new RecordingCategoryApiRepository(expectedResult);
            var service = new CategoryService(repository);
            var query = new CategoryListQuery
            {
                PageSize = 20,
                Direction = "next",
                AfterCursor = "cursor-next",
                SearchText = "lego"
            };

            var result = await service.GetCategoriesAsync(query);

            Assert.Same(expectedResult, result);
            Assert.NotNull(repository.LastQuery);
            Assert.Equal(20, repository.LastQuery!.PageSize);
            Assert.Equal("next", repository.LastQuery.Direction);
            Assert.Equal("cursor-next", repository.LastQuery.AfterCursor);
            Assert.Equal("lego", repository.LastQuery.SearchText);
        }

        private sealed class RecordingCategoryApiRepository : ICategoryApiRepository
        {
            private readonly CategoryListResult _result;

            public RecordingCategoryApiRepository(CategoryListResult result)
            {
                _result = result;
            }

            public CategoryListQuery? LastQuery { get; private set; }

            public Task<CategoryListResult> GetCategoriesAsync(CategoryListQuery query)
            {
                LastQuery = query;
                return Task.FromResult(_result);
            }

            public Task<Category> CreateCategoryAsync(Category category) => throw new NotSupportedException();

            public Task<Category> UpdateCategoryAsync(Category category) => throw new NotSupportedException();

            public Task<bool> DeleteCategoryAsync(int id) => throw new NotSupportedException();

            public Task<Category> RestoreCategoryAsync(int id) => throw new NotSupportedException();
        }
    }
}
