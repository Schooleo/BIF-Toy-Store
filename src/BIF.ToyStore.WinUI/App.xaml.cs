using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Infrastructure.Data;
using BIF.ToyStore.Infrastructure.GraphQL;
using BIF.ToyStore.Infrastructure.Repositories;
using BIF.ToyStore.Infrastructure.Services;
using BIF.ToyStore.ViewModels.Pages;
using BIF.ToyStore.ViewModels.Utils;
using BIF.ToyStore.WinUI.Services;
using CommunityToolkit.Mvvm.Messaging;
using HotChocolate.Types;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Threading.Tasks;

namespace BIF.ToyStore.WinUI
{
    public partial class App : Application
    {
        public IServiceProvider Services { get; }
        public MainWindow? MainWindowInstance => _window as MainWindow;

        private readonly IHost _host;
        private readonly bool _pendingRestoreApplied;
        private readonly string? _pendingRestoreApplyError;

        public new static App Current => (App)Application.Current;

        public App()
        {
            InitializeComponent();

            var bootstrapSettings = new LocalSettingsService();
            (_pendingRestoreApplied, _pendingRestoreApplyError) = ApplyPendingRestoreIfScheduled(bootstrapSettings);
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
                        services.AddScoped<IProductRepository, ProductRepository>();
                        services.AddScoped<ICategoryRepository, CategoryRepository>();

                        // Services
                        services.AddMemoryCache();
                        services.AddScoped<IAuthService, AuthService>();
                        services.AddScoped<IConfigService, ConfigService>();
                        services.AddScoped<IOrderService, OrderService>();
                        services.AddSingleton<ICredentialVaultService, CredentialVaultService>();
                        services.AddSingleton<ILocalSettingsService, LocalSettingsService>();
                        services.AddSingleton<IAppInfoService, AppInfoService>();
                        services.AddSingleton<IExcelFilePickerService, ExcelFilePickerService>();
                        services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);

                        // GraphQL
                        services.AddGraphQLServer()
                                .AddQueryType<Queries>()
                                .AddMutationType<Mutations>()
                                .AddTypeExtension<CategoryExtension>()
                                .AddTypeExtension<ProductExtension>()
                                .AddType<UploadType>()
                                .AddFiltering()
                                .AddSorting()
                                .ModifyCostOptions(o => o.MaxFieldCost = 5000);

                        // Utils
                        services.AddSingleton<IGraphQLClient>(_ => new GraphQLClient($"http://localhost:{serverPort}/"));

                        // ViewModels
                        services.AddTransient<InitialSetupViewModel>();
                        services.AddTransient<LoginViewModel>();
                        services.AddTransient<ProductsViewModel>();
                        services.AddTransient<CategoriesViewModel>();
                        services.AddTransient<DashboardViewModel>();
                        services.AddTransient<UserManagementViewModel>();
                        services.AddTransient<POSViewModel>();
                        services.AddTransient<OrderViewModel>();
                        services.AddTransient<ReportsViewModel>();
                        services.AddTransient<SettingsViewModel>();
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

            if (_pendingRestoreApplied && _window.Content?.XamlRoot is { } appliedRoot)
            {
                await CommonDialog.ShowAsync(
                    appliedRoot,
                    CommonDialogType.Information,
                    title: "Restore Applied",
                    message: "A scheduled restore was applied successfully before startup.",
                    primaryButtonText: "OK",
                    closeButtonText: null);
            }

            if (!string.IsNullOrWhiteSpace(_pendingRestoreApplyError) && _window.Content?.XamlRoot is { } errorRoot)
            {
                await CommonDialog.ShowAsync(
                    errorRoot,
                    CommonDialogType.Warning,
                    title: "Restore Pending",
                    message: "Scheduled restore could not be applied: " + _pendingRestoreApplyError,
                    primaryButtonText: "OK",
                    closeButtonText: null);
            }

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

        private static (bool applied, string? error) ApplyPendingRestoreIfScheduled(LocalSettingsService localSettings)
        {
            var backupPath = localSettings.GetString(AppPreferenceKeys.PendingRestoreBackupPath);
            var targetPath = localSettings.GetString(AppPreferenceKeys.PendingRestoreTargetPath);

            if (string.IsNullOrWhiteSpace(backupPath) || string.IsNullOrWhiteSpace(targetPath))
            {
                return (false, null);
            }

            try
            {
                if (!File.Exists(backupPath))
                {
                    return (false, "Backup file was not found.");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? AppContext.BaseDirectory);
                File.Copy(backupPath, targetPath, overwrite: true);
                RestoreCompanionFile(backupPath + "-wal", targetPath + "-wal");
                RestoreCompanionFile(backupPath + "-shm", targetPath + "-shm");

                localSettings.SetString(AppPreferenceKeys.PendingRestoreBackupPath, string.Empty);
                localSettings.SetString(AppPreferenceKeys.PendingRestoreTargetPath, string.Empty);

                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        private static void RestoreCompanionFile(string sourcePath, string targetPath)
        {
            if (File.Exists(sourcePath))
            {
                File.Copy(sourcePath, targetPath, overwrite: true);
                return;
            }

            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
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
                await CommonDialog.ShowAsync(
                    xamlRoot,
                    CommonDialogType.Error,
                    title: "Background Server Crashed!",
                    message: ex.Message + "\n\n" + ex.InnerException?.Message,
                    primaryButtonText: "OK",
                    closeButtonText: null);
                return;
            }

            // Otherwise wait for the content Loaded event
            var tcs = new TaskCompletionSource();
            ((FrameworkElement)_window!.Content).Loaded += async (_, _) =>
            {
                await CommonDialog.ShowAsync(
                    _window.Content.XamlRoot,
                    CommonDialogType.Error,
                    title: "Background Server Crashed!",
                    message: ex.Message + "\n\n" + ex.InnerException?.Message,
                    primaryButtonText: "OK",
                    closeButtonText: null);
                tcs.TrySetResult();
            };
            await tcs.Task;
        }
    }
}
