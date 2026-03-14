using BIF.ToyStore.Core.Interfaces;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BIF.ToyStore.ViewModels.Utils
{
    public class GraphQLClient : IGraphQLClient
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;

        public GraphQLClient()
        {
            _httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5000/") };

            // Ensures JSON properties map correctly
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            // Parse Enum from strings
            _jsonOptions.Converters.Add(new JsonStringEnumConverter());
        }

        public async Task<T?> ExecuteAsync<T>(string query, object? variables = null, string dataKey = "")
        {
            var requestBody = new { query, variables };

            var response = await _httpClient.PostAsJsonAsync("graphql", requestBody);
            response.EnsureSuccessStatusCode();

            // Use JsonDocument for more flexible parsing
            using var jsonDocument = await response.Content.ReadFromJsonAsync<JsonDocument>() 
                ?? throw new Exception("Empty response from server.");
            var root = jsonDocument.RootElement;

            // Check for GraphQL Server Errors
            if (root.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0)
            {
                var errorMessage = errors[0].GetProperty("message").GetString();
                throw new Exception($"GraphQL Error: {errorMessage}");
            }

            if (!root.TryGetProperty("data", out var data))
            {
                throw new Exception("No data returned from GraphQL.");
            }

            // Drill down into the specific query node (e.g., "login" or "products")
            if (!string.IsNullOrEmpty(dataKey))
            {
                if (data.TryGetProperty(dataKey, out var specificData))
                {
                    if (specificData.ValueKind == JsonValueKind.Null) return default;

                    return specificData.Deserialize<T>(_jsonOptions);
                }
                throw new Exception($"The key '{dataKey}' was not found in the GraphQL response.");
            }

            // If no dataKey is provided, deserialize the entire data block
            return data.Deserialize<T>(_jsonOptions);
        }
    }
}