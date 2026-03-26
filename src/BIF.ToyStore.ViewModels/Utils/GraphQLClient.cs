using BIF.ToyStore.Core.Interfaces;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BIF.ToyStore.ViewModels.Utils
{
    public class GraphQLClient : IGraphQLClient
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;

        public GraphQLClient(string baseAddress = "http://localhost:5000/")
        {
            _httpClient = new HttpClient { BaseAddress = new Uri(baseAddress) };

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
            return await ParseGraphQLResponseAsync<T>(response, dataKey);
        }

        public async Task<T?> UploadFileAsync<T>(string query, string variableName, string filePath, string dataKey = "")
        {
            if (string.IsNullOrWhiteSpace(variableName))
            {
                throw new ArgumentException("Variable name is required.", nameof(variableName));
            }

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                throw new FileNotFoundException("The selected file was not found.", filePath);
            }

            var operations = JsonSerializer.Serialize(new
            {
                query,
                variables = new Dictionary<string, object?>
                {
                    [variableName] = null
                }
            }, _jsonOptions);

            var map = JsonSerializer.Serialize(new Dictionary<string, string[]>
            {
                ["0"] = [$"variables.{variableName}"]
            });

            using var stream = File.OpenRead(filePath);
            using var multipart = new MultipartFormDataContent();
            multipart.Add(new StringContent(operations, Encoding.UTF8, "application/json"), "operations");
            multipart.Add(new StringContent(map, Encoding.UTF8, "application/json"), "map");

            var fileContent = new StreamContent(stream);
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            var contentType = extension == ".xls"
                ? "application/vnd.ms-excel"
                : extension == ".xlsx"
                    ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                    : "application/octet-stream";

            fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            multipart.Add(fileContent, "0", Path.GetFileName(filePath));

            // Server requires GraphQL preflight header for multipart uploads
            var request = new HttpRequestMessage(HttpMethod.Post, "graphql")
            {
                Content = multipart
            };
            request.Headers.Add("GraphQL-Preflight", "1");

            var response = await _httpClient.SendAsync(request);
            return await ParseGraphQLResponseAsync<T>(response, dataKey);
        }

        /// <summary>
        /// Parse GraphQL response and handle errors uniformly across ExecuteAsync and UploadFileAsync.
        /// Collects all GraphQL errors (not just the first one) and provides better diagnostics.
        /// </summary>
        private async Task<T?> ParseGraphQLResponseAsync<T>(HttpResponseMessage response, string dataKey)
        {
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                var statusCode = (int)response.StatusCode;

                var errorDetails = responseBody.Length > 500 ? responseBody[..500] + "..." : responseBody;
                throw new HttpRequestException(
                    $"HTTP {statusCode} Error: {errorDetails}",
                    null,
                    response.StatusCode);
            }

            using var jsonDocument = await response.Content.ReadFromJsonAsync<JsonDocument>()
                ?? throw new InvalidOperationException("Empty response from server.");
            var root = jsonDocument.RootElement;

            // Collect ALL GraphQL errors, not just the first one
            if (root.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0)
            {
                var messages = Enumerable.Range(0, errors.GetArrayLength())
                    .Select(i => errors[i].TryGetProperty("message", out var msg) ? msg.GetString() : "Unknown error")
                    .Where(m => m is not null);

                throw new InvalidOperationException($"GraphQL Error(s): {string.Join("; ", messages)}");
            }

            if (!root.TryGetProperty("data", out var data))
            {
                throw new InvalidOperationException("No 'data' field in GraphQL response.");
            }

            if (!string.IsNullOrEmpty(dataKey))
            {
                if (!data.TryGetProperty(dataKey, out var specificData))
                {
                    throw new KeyNotFoundException($"Key '{dataKey}' not found in GraphQL response.");
                }

                if (specificData.ValueKind == JsonValueKind.Null)
                {
                    return default;
                }

                return specificData.Deserialize<T>(_jsonOptions);
            }

            return data.Deserialize<T>(_jsonOptions);
        }
    }
}