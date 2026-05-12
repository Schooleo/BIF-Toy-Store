namespace BIF.ToyStore.Core.Interfaces
{
    public interface IGraphQLClient
    {
        Task<T?> ExecuteAsync<T>(string query, object? variables = null, string dataKey = "");
        Task<T?> UploadFileAsync<T>(string query, string variableName, string filePath, string dataKey = "", object? variables = null);
    }
}
