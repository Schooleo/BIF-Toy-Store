using BIF.ToyStore.ViewModels.Utils;
using System.Net;
using System.Text;
using System.Text.Json;

namespace BIF.ToyStore.Tests.ViewModels
{
    public class GraphQLClientTests
    {
        [Fact]
        public async Task ExecuteAsync_ValidDataKey_ReturnsTypedObject()
        {
            using var server = new StubGraphQlServer();
            server.EnqueueResponse(HttpStatusCode.OK, """
            {
              "data": {
                "appConfig": {
                  "taxRate": 0.08,
                  "currencySymbol": "USD"
                }
              }
            }
            """);

            var client = new GraphQLClient(server.BaseAddress);

            var result = await client.ExecuteAsync<ConfigDto>("query { appConfig { taxRate currencySymbol } }", dataKey: "appConfig");

            Assert.NotNull(result);
            Assert.Equal(0.08m, result!.TaxRate);
            Assert.Equal("USD", result.CurrencySymbol);
        }

        [Fact]
        public async Task ExecuteAsync_HttpError_ThrowsHttpRequestException()
        {
            using var server = new StubGraphQlServer();
            server.EnqueueResponse(HttpStatusCode.InternalServerError, "server exploded");

            var client = new GraphQLClient(server.BaseAddress);

            var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
                client.ExecuteAsync<ConfigDto>("query { appConfig { taxRate } }", dataKey: "appConfig"));

            Assert.Contains("HTTP 500", ex.Message);
        }

        [Fact]
        public async Task ExecuteAsync_GraphQlErrors_ThrowsInvalidOperationExceptionWithAllMessages()
        {
            using var server = new StubGraphQlServer();
            server.EnqueueResponse(HttpStatusCode.OK, """
            {
              "errors": [
                { "message": "First issue" },
                { "message": "Second issue" }
              ]
            }
            """);

            var client = new GraphQLClient(server.BaseAddress);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                client.ExecuteAsync<ConfigDto>("query { appConfig { taxRate } }", dataKey: "appConfig"));

            Assert.Contains("First issue", ex.Message);
            Assert.Contains("Second issue", ex.Message);
        }

        [Fact]
        public async Task ExecuteAsync_MissingDataKey_ThrowsKeyNotFoundException()
        {
            using var server = new StubGraphQlServer();
            server.EnqueueResponse(HttpStatusCode.OK, """
            {
              "data": {
                "other": { "taxRate": 0.1 }
              }
            }
            """);

            var client = new GraphQLClient(server.BaseAddress);

            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                client.ExecuteAsync<ConfigDto>("query { appConfig { taxRate } }", dataKey: "appConfig"));
        }

        [Fact]
        public async Task UploadFileAsync_EmptyVariableName_ThrowsArgumentException()
        {
            using var server = new StubGraphQlServer();
            var client = new GraphQLClient(server.BaseAddress);

            await Assert.ThrowsAsync<ArgumentException>(() =>
                client.UploadFileAsync<ConfigDto>("mutation {}", string.Empty, "fake.xlsx", "x"));
        }

        [Fact]
        public async Task UploadFileAsync_MissingFile_ThrowsFileNotFoundException()
        {
            using var server = new StubGraphQlServer();
            var client = new GraphQLClient(server.BaseAddress);

            await Assert.ThrowsAsync<FileNotFoundException>(() =>
                client.UploadFileAsync<ConfigDto>("mutation {}", "file", "definitely-missing.xlsx", "x"));
        }

        private sealed class ConfigDto
        {
            public decimal TaxRate { get; set; }
            public string CurrencySymbol { get; set; } = string.Empty;
        }

        private sealed class StubGraphQlServer : IDisposable
        {
            private readonly HttpListener _listener;
            private readonly Queue<(HttpStatusCode status, string body)> _responses = new();
            private readonly CancellationTokenSource _cancellation = new();
            private readonly Task _serverLoop;

            public string BaseAddress { get; }

            public StubGraphQlServer()
            {
                var port = GetFreePort();
                BaseAddress = $"http://127.0.0.1:{port}/";

                _listener = new HttpListener();
                _listener.Prefixes.Add(BaseAddress);
                _listener.Start();

                _serverLoop = Task.Run(RunAsync);
            }

            public void EnqueueResponse(HttpStatusCode status, string body)
            {
                lock (_responses)
                {
                    _responses.Enqueue((status, body));
                }
            }

            private async Task RunAsync()
            {
                while (!_cancellation.IsCancellationRequested)
                {
                    HttpListenerContext? context = null;
                    try
                    {
                        context = await _listener.GetContextAsync();
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                    catch (HttpListenerException)
                    {
                        break;
                    }

                    (HttpStatusCode status, string body) response;
                    lock (_responses)
                    {
                        response = _responses.Count > 0
                            ? _responses.Dequeue()
                            : (HttpStatusCode.InternalServerError, "no queued response");
                    }

                    context.Response.StatusCode = (int)response.status;
                    context.Response.ContentType = "application/json";

                    var payload = Encoding.UTF8.GetBytes(response.body);
                    context.Response.ContentLength64 = payload.Length;
                    await context.Response.OutputStream.WriteAsync(payload);
                    context.Response.Close();
                }
            }

            public void Dispose()
            {
                _cancellation.Cancel();
                _listener.Stop();
                _listener.Close();

                try
                {
                    _serverLoop.Wait(TimeSpan.FromSeconds(2));
                }
                catch
                {
                    // Best-effort shutdown for test server.
                }

                _cancellation.Dispose();
            }

            private static int GetFreePort()
            {
                var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                var port = ((IPEndPoint)listener.LocalEndpoint).Port;
                listener.Stop();
                return port;
            }
        }
    }
}
