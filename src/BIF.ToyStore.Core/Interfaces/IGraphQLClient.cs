namespace BIF.ToyStore.Core.Interfaces
{
    public interface IGraphQLClient
    {
        Task<T?> ExecuteAsync<T>(string query, object? variables = null, string dataKey = "");
    }
}
