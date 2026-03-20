using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Infrastructure.Data;
using BIF.ToyStore.Infrastructure.GraphQL;
using BIF.ToyStore.Infrastructure.Repositories;
using BIF.ToyStore.Infrastructure.Services;
using BIF.ToyStore.ViewModels.Pages;
using BIF.ToyStore.ViewModels.Utils;
using BIF.ToyStore.WinUI.Services;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

namespace BIF.ToyStore.WinUI
{
    public partial class App : Application
    {
        public IServiceProvider Services { get; }
        public MainWindow? MainWindowInstance => _window as MainWindow;

        private readonly IHost _host;

        public new static App Current => (App)Application.Current;

        public App()
        {
            InitializeComponent();

            var bootstrapSettings = new LocalSettingsService();
            var serverPort = bootstrapSettings.GetInt(AppPreferenceKeys.LocalServerPort, 5000);

            // Create the Host Builder
            _host = Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    // Configure the Kestrel Web Server
                    webBuilder.UseKestrel(options =>
                    {
                        options.ListenLocalhost(serverPort);
                    });

                    // Configure your Services & GraphQL
                    webBuilder.ConfigureServices(services =>
                    {
                        // Database
                        services.AddDbContext<AppDbContext>();

                        // Repositories
                        services.AddScoped(typeof(IRepository<>), typeof(BaseRepository<>));

                        // Services
                        services.AddMemoryCache();
                        services.AddScoped<IAuthService, AuthService>();
                        services.AddScoped<IConfigService, ConfigService>();
                        services.AddScoped<IOrderService, OrderService>();
                        services.AddSingleton<ICredentialVaultService, CredentialVaultService>();
                        services.AddSingleton<ILocalSettingsService, LocalSettingsService>();
                        services.AddSingleton<IAppInfoService, AppInfoService>();
                        services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);

                        // GraphQL
                        services.AddGraphQLServer()
                                .AddQueryType<Queries>()
                                .AddMutationType<Mutations>()
                                .AddFiltering()
                                .AddSorting();

                        // Utils
                        services.AddSingleton<IGraphQLClient>(_ => new GraphQLClient($"http://localhost:{serverPort}/"));

                        // ViewModels
                        services.AddTransient<InitialSetupViewModel>();
                        services.AddTransient<LoginViewModel>();
                    });

                    // Map the GraphQL Endpoint
                    webBuilder.Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapGraphQL(); // Exposes /graphql
                        });
                    });
                })
                .Build();

            // Extract the services for WinUI to use
            Services = _host.Services;
        }

        private Window? _window;
        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            _window = new MainWindow();
            _window.Activate();

            try
            {
                // Start the background GraphQL server
                await _host.StartAsync();

                // Temporary scope for fetching the database
                using var scope = _host.Services.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Run the seeder
                await DatabaseSeeder.SeedAsync(dbContext);

                var graphQLClient = _host.Services.GetRequiredService<IGraphQLClient>();
                var setupState = await graphQLClient.ExecuteAsync<SetupStateView>(
                    @"query SetupState {
                        setupState {
                            isInitialSetupCompleted
                        }
                    }",
                    dataKey: "setupState");

                var mainWindow = (MainWindow)_window;
                if (setupState?.IsInitialSetupCompleted == true)
                {
                    mainWindow.NavigateToLogin();
                }
                else
                {
                    mainWindow.NavigateToInitialSetup();
                }
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync(ex);
            }
        }

        private sealed class SetupStateView
        {
            public bool IsInitialSetupCompleted { get; set; }
        }

        // Shows a ContentDialog safely
        private async Task ShowErrorDialogAsync(Exception ex)
        {
            // If the visual tree is already ready, show immediately
            if (_window?.Content?.XamlRoot is { } xamlRoot)
            {
                await CreateErrorDialog(ex, xamlRoot).ShowAsync();
                return;
            }

            // Otherwise wait for the content Loaded event
            var tcs = new TaskCompletionSource();
            ((FrameworkElement)_window!.Content).Loaded += async (_, _) =>
            {
                await CreateErrorDialog(ex, _window.Content.XamlRoot).ShowAsync();
                tcs.TrySetResult();
            };
            await tcs.Task;
        }

        private static ContentDialog CreateErrorDialog(Exception ex, XamlRoot xamlRoot) =>
            new()
            {
                Title = "Background Server Crashed!",
                Content = ex.Message + "\n\n" + ex.InnerException?.Message,
                CloseButtonText = "OK",
                XamlRoot = xamlRoot
            };
    }
}
