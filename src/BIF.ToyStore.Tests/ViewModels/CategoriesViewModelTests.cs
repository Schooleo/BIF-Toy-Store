using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.Core.Settings;
using BIF.ToyStore.Infrastructure.GraphQL;
using BIF.ToyStore.ViewModels.Pages;
using Moq;
using Xunit;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BIF.ToyStore.Tests.ViewModels.Pages
{
    public class CategoriesViewModelTests
    {
        private readonly Mock<IGraphQLClient> _graphQLClientMock;
        private readonly CategoriesViewModel _viewModel;

        public CategoriesViewModelTests()
        {
            _graphQLClientMock = new Mock<IGraphQLClient>();
            _viewModel = new CategoriesViewModel(_graphQLClientMock.Object);
        }

        // ─── Constructor defaults ────────────────────────────────────────────────

        [Fact]
        public void Constructor_SetsCorrectTitle()
        {
            Assert.Equal("Category Management", _viewModel.Title);
        }

        // ─── LoadCategoriesAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task LoadCategoriesAsync_ValidResponse_UpdatesCollectionAndPagingInfo()
        {
            // Arrange
            var fakeResponse = new CategoriesViewModel.CategoryConnection
            {
                TotalCount = 10,
                PageInfo = new CategoriesViewModel.PageInfo
                {
                    HasNextPage = false,
                    HasPreviousPage = false,
                    StartCursor = "c1",
                    EndCursor = "c10"
                },
                Nodes = new List<Category>
                {
                    new Category { Id = 1, Name = "Other" },
                    new Category { Id = 2, Name = "Lego" },
                    new Category { Id = 3, Name = "Board Games" }
                }
            };

            _graphQLClientMock.Setup(x => x.ExecuteAsync<CategoriesViewModel.CategoryConnection>(
                It.IsAny<string>(),
                It.IsAny<object>(),
                "categories"
            )).ReturnsAsync(fakeResponse);

            // Act
            await _viewModel.LoadCategoriesAsync();

            // Assert
            Assert.Equal(3, _viewModel.Categories.Count);
            Assert.Equal("Other", _viewModel.Categories[0].Name);
            Assert.Equal(10, _viewModel.TotalCount);
            Assert.False(_viewModel.HasNextPage);
            Assert.False(_viewModel.HasPreviousPage);
            Assert.Equal("c10", _viewModel.AfterCursor);
        }

        [Fact]
        public async Task LoadCategoriesAsync_WhenCompleted_IsBusyResetToFalse()
        {
            // Arrange
            _graphQLClientMock.Setup(x => x.ExecuteAsync<CategoriesViewModel.CategoryConnection>(
                It.IsAny<string>(),
                It.IsAny<object>(),
                "categories"
            )).ReturnsAsync(new CategoriesViewModel.CategoryConnection { Nodes = new List<Category>() });

            // Act
            await _viewModel.LoadCategoriesAsync();

            // Assert
            Assert.False(_viewModel.IsBusy);
        }

        // ─── Filter ──────────────────────────────────────────────────────────────

        [Fact]
        public async Task ApplyFilterAsync_WhenCalled_ResetsCursorsAndReloads()
        {
            // Arrange
            _viewModel.AfterCursor = "some_cursor";
            _viewModel.SearchText = "Lego";

            _graphQLClientMock.Setup(x => x.ExecuteAsync<CategoriesViewModel.CategoryConnection>(
                It.Is<string>(query => query.Contains("contains: \"Lego\"")),
                It.IsAny<object>(),
                "categories"
            )).ReturnsAsync(new CategoriesViewModel.CategoryConnection { Nodes = new List<Category>() });

            // Act
            await _viewModel.ApplyFilterCommand.ExecuteAsync(null);

            // Assert
            Assert.Null(_viewModel.AfterCursor);
            _graphQLClientMock.Verify(x => x.ExecuteAsync<CategoriesViewModel.CategoryConnection>(
                It.Is<string>(query => query.Contains("contains: \"Lego\"")),
                It.IsAny<object>(),
                "categories"
            ), Times.Once);
        }

        [Fact]
        public async Task ClearFilterAsync_WhenCalled_ResetsSearchTextAndReloads()
        {
            // Arrange
            _viewModel.SearchText = "Lego";

            _graphQLClientMock.Setup(x => x.ExecuteAsync<CategoriesViewModel.CategoryConnection>(
                It.IsAny<string>(),
                It.IsAny<object>(),
                "categories"
            )).ReturnsAsync(new CategoriesViewModel.CategoryConnection { Nodes = new List<Category>() });

            // Act
            await _viewModel.ClearFilterCommand.ExecuteAsync(null);

            // Assert
            Assert.Empty(_viewModel.SearchText);
        }

        // ─── DeleteCategoryAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task DeleteCategoryAsync_OtherCategoryId_DoesNotCallGraphQL()
        {
            // Act – attempt to delete the protected "Other" category
            await _viewModel.DeleteCategoryAsync(AppConstants.OtherCategoryId);

            // Assert – GraphQL must never be called
            _graphQLClientMock.Verify(x => x.ExecuteAsync<bool>(
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<string>()
            ), Times.Never);
        }

        [Fact]
        public async Task DeleteCategoryAsync_ValidId_CallsGraphQLAndReloads()
        {
            // Arrange
            _graphQLClientMock.Setup(x => x.ExecuteAsync<bool>(
                It.Is<string>(q => q.Contains("deleteCategory")),
                It.IsAny<object>(),
                "deleteCategory"
            )).ReturnsAsync(true);

            _graphQLClientMock.Setup(x => x.ExecuteAsync<CategoriesViewModel.CategoryConnection>(
                It.IsAny<string>(),
                It.IsAny<object>(),
                "categories"
            )).ReturnsAsync(new CategoriesViewModel.CategoryConnection { Nodes = new List<Category>() });

            // Act
            await _viewModel.DeleteCategoryAsync(5);

            // Assert
            _graphQLClientMock.Verify(x => x.ExecuteAsync<bool>(
                It.Is<string>(q => q.Contains("deleteCategory")),
                It.IsAny<object>(),
                "deleteCategory"
            ), Times.Once);
        }

        // ─── RestoreCategoryAsync ────────────────────────────────────────────────

        [Fact]
        public async Task RestoreCategoryAsync_ValidId_CallsGraphQLAndReloads()
        {
            // Arrange
            _graphQLClientMock.Setup(x => x.ExecuteAsync<Category>(
                It.Is<string>(q => q.Contains("restoreCategory")),
                It.IsAny<object>(),
                "restoreCategory"
            )).ReturnsAsync(new Category { Id = 5, Name = "Restored" });

            _graphQLClientMock.Setup(x => x.ExecuteAsync<CategoriesViewModel.CategoryConnection>(
                It.IsAny<string>(),
                It.IsAny<object>(),
                "categories"
            )).ReturnsAsync(new CategoriesViewModel.CategoryConnection { Nodes = new List<Category>() });

            // Act
            await _viewModel.RestoreCategoryAsync(5);

            // Assert
            _graphQLClientMock.Verify(x => x.ExecuteAsync<Category>(
                It.Is<string>(q => q.Contains("restoreCategory")),
                It.IsAny<object>(),
                "restoreCategory"
            ), Times.Once);
        }
    }
}
