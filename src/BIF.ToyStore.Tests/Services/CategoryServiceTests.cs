using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.Infrastructure.Services;

namespace BIF.ToyStore.Tests.Services
{
    public class CategoryServiceTests
    {
        [Fact]
        public async Task GetCategoriesAsync_NextDirection_UsesFirstAndAfterVariables()
        {
            var graphQLClient = new RecordingGraphQLClient
            {
                ExecuteFactory = type => CreateCategoryConnection(
                    type,
                    totalCount: 1,
                    hasNextPage: false,
                    hasPreviousPage: false,
                    startCursor: "s1",
                    endCursor: "e1",
                    nodes: [new Category { Id = 1, Name = "Lego" }])
            };

            var service = new CategoryService(graphQLClient);

            var result = await service.GetCategoriesAsync(new CategoryListQuery
            {
                PageSize = 20,
                Direction = "next",
                AfterCursor = "cursor-next"
            });

            Assert.Single(result.Items);
            Assert.NotNull(graphQLClient.LastVariables);
            Assert.Equal(20, GetVariableValue<int?>(graphQLClient.LastVariables, "first"));
            Assert.Null(GetVariableValue<int?>(graphQLClient.LastVariables, "last"));
            Assert.Equal("cursor-next", GetVariableValue<string?>(graphQLClient.LastVariables, "after"));
            Assert.Null(GetVariableValue<string?>(graphQLClient.LastVariables, "before"));
        }

        [Fact]
        public async Task GetCategoriesAsync_PrevDirection_UsesLastAndBeforeVariables()
        {
            var graphQLClient = new RecordingGraphQLClient
            {
                ExecuteFactory = type => CreateCategoryConnection(
                    type,
                    totalCount: 1,
                    hasNextPage: false,
                    hasPreviousPage: true,
                    startCursor: "s1",
                    endCursor: "e1",
                    nodes: [new Category { Id = 2, Name = "Board Games" }])
            };

            var service = new CategoryService(graphQLClient);

            var result = await service.GetCategoriesAsync(new CategoryListQuery
            {
                PageSize = 20,
                Direction = "prev",
                BeforeCursor = "cursor-prev"
            });

            Assert.Single(result.Items);
            Assert.NotNull(graphQLClient.LastVariables);
            Assert.Null(GetVariableValue<int?>(graphQLClient.LastVariables, "first"));
            Assert.Equal(20, GetVariableValue<int?>(graphQLClient.LastVariables, "last"));
            Assert.Null(GetVariableValue<string?>(graphQLClient.LastVariables, "after"));
            Assert.Equal("cursor-prev", GetVariableValue<string?>(graphQLClient.LastVariables, "before"));
        }

        [Fact]
        public async Task GetCategoriesAsync_NodesMissing_ReturnsEmptyItems()
        {
            var graphQLClient = new RecordingGraphQLClient
            {
                ExecuteFactory = type => CreateCategoryConnection(
                    type,
                    totalCount: 0,
                    hasNextPage: false,
                    hasPreviousPage: false,
                    startCursor: null,
                    endCursor: null,
                    nodes: null)
            };

            var service = new CategoryService(graphQLClient);

            var result = await service.GetCategoriesAsync(new CategoryListQuery
            {
                PageSize = 20
            });

            Assert.Empty(result.Items);
            Assert.Equal(0, result.TotalCount);
            Assert.False(result.HasNextPage);
            Assert.False(result.HasPreviousPage);
        }

        private static T? GetVariableValue<T>(object source, string propertyName)
        {
            var property = source.GetType().GetProperty(propertyName);
            Assert.NotNull(property);

            return (T?)property.GetValue(source);
        }

        private static object CreateCategoryConnection(
            Type connectionType,
            int totalCount,
            bool hasNextPage,
            bool hasPreviousPage,
            string? startCursor,
            string? endCursor,
            List<Category>? nodes)
        {
            var connection = Activator.CreateInstance(connectionType)
                ?? throw new InvalidOperationException("Cannot create category connection instance.");

            SetProperty(connection, "TotalCount", totalCount);

            var pageInfoProperty = connectionType.GetProperty("PageInfo")
                ?? throw new InvalidOperationException("Missing PageInfo property.");
            var pageInfo = Activator.CreateInstance(pageInfoProperty.PropertyType)
                ?? throw new InvalidOperationException("Cannot create pageInfo instance.");

            SetProperty(pageInfo, "HasNextPage", hasNextPage);
            SetProperty(pageInfo, "HasPreviousPage", hasPreviousPage);
            SetProperty(pageInfo, "StartCursor", startCursor);
            SetProperty(pageInfo, "EndCursor", endCursor);

            pageInfoProperty.SetValue(connection, pageInfo);
            SetProperty(connection, "Nodes", nodes);

            return connection;
        }

        private static void SetProperty(object target, string propertyName, object? value)
        {
            var property = target.GetType().GetProperty(propertyName)
                ?? throw new InvalidOperationException($"Missing property: {propertyName}");
            property.SetValue(target, value);
        }

        private sealed class RecordingGraphQLClient : IGraphQLClient
        {
            public object? LastVariables { get; private set; }
            public Func<Type, object?>? ExecuteFactory { get; init; }

            public Task<T?> ExecuteAsync<T>(string query, object? variables = null, string dataKey = "")
            {
                LastVariables = variables;
                var result = ExecuteFactory?.Invoke(typeof(T));
                return Task.FromResult((T?)result);
            }

            public Task<T?> UploadFileAsync<T>(string query, string variableName, string filePath, string dataKey = "")
            {
                throw new NotSupportedException("Upload is not used in these tests.");
            }
        }
    }
}
