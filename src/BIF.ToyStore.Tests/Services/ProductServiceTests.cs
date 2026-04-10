using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.Infrastructure.Services;

namespace BIF.ToyStore.Tests.Services
{
    public class ProductServiceTests
    {
        [Fact]
        public async Task GetCategoriesAsync_DefaultTake_Uses250AsFirstVariable()
        {
            var graphQLClient = new RecordingGraphQLClient
            {
                ExecuteFactory = type => CreateCategoryConnection(type, nodes: [new Category { Id = 1, Name = "Other" }])
            };

            var service = new ProductService(graphQLClient);

            var result = await service.GetCategoriesAsync();

            Assert.Single(result);
            Assert.NotNull(graphQLClient.LastVariables);
            Assert.Equal(250, GetVariableValue<int>(graphQLClient.LastVariables, "first"));
        }

        private static T GetVariableValue<T>(object source, string propertyName)
        {
            var property = source.GetType().GetProperty(propertyName);
            Assert.NotNull(property);

            return (T)property.GetValue(source)!;
        }

        private static object CreateCategoryConnection(Type connectionType, List<Category>? nodes)
        {
            var connection = Activator.CreateInstance(connectionType)
                ?? throw new InvalidOperationException("Cannot create category connection instance.");
            var nodesProperty = connectionType.GetProperty("Nodes")
                ?? throw new InvalidOperationException("Missing Nodes property.");
            nodesProperty.SetValue(connection, nodes);
            return connection;
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
