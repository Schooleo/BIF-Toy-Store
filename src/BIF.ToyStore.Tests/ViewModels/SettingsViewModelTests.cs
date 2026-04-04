using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.ViewModels.Pages;
using BIF.ToyStore.ViewModels.Utils;
using Microsoft.Data.Sqlite;
using System.Globalization;
using System.Reflection;

namespace BIF.ToyStore.Tests.ViewModels.Pages
{
    public class SettingsViewModelTests
    {
        [Fact]
        public async Task LoadAsync_LoadsLocalAndServerSettings()
        {
            var local = new InMemoryLocalSettingsService();
            local.SetInt(AppPreferenceKeys.CommunicationPort, 6000);
            local.SetBool(AppPreferenceKeys.AutoReconnect, false);
            local.SetInt(AppPreferenceKeys.ProductsItemsPerPage, 15);
            local.SetBool(AppPreferenceKeys.StartOnLastOpened, true);

            var graph = new FakeGraphQlClient();
            graph.SetStoreSettings(
                taxRate: 0.075m,
                currencySymbol: "USD",
                receiptHeader: "Header",
                receiptFooter: "Footer",
                databasePath: "ToyStore.db");

            var vm = new SettingsViewModel(graph, local);

            await vm.LoadAsync();

            Assert.Equal(6000, (int)vm.CommunicationPort);
            Assert.False(vm.AutoReconnect);
            Assert.Equal(7.5, vm.TaxRate, 1);
            Assert.Equal("USD", vm.SelectedCurrency);
            Assert.Equal("Header", vm.ReceiptHeader);
            Assert.Equal("Footer", vm.ReceiptFooter);
            Assert.Equal(15, vm.SelectedItemsPerPage);
            Assert.True(vm.StartOnLastOpened);
            Assert.False(vm.HasServerConfigurationChanges());
            Assert.Equal(string.Empty, vm.ErrorMessage);
        }

        [Fact]
        public async Task SaveChanges_InvalidPort_SetsValidationErrorAndSkipsSave()
        {
            var local = new InMemoryLocalSettingsService();
            var graph = new FakeGraphQlClient();
            var vm = new SettingsViewModel(graph, local)
            {
                CommunicationPort = 0,
                TaxRate = 10,
                SelectedCurrency = "VND",
                SelectedItemsPerPage = 20
            };

            await vm.SaveChangesCommand.ExecuteAsync(null);

            Assert.Equal("Communication port must be between 1 and 65535.", vm.ErrorMessage);
            Assert.Equal(string.Empty, vm.StatusMessage);
            Assert.False(local.SetIntCalls.ContainsKey(AppPreferenceKeys.CommunicationPort));
        }

        [Fact]
        public async Task SaveChanges_ValidInput_PersistsAndSetsSuccessStatus()
        {
            var local = new InMemoryLocalSettingsService();
            var graph = new FakeGraphQlClient();
            graph.SetStoreSettings(0.1m, "VND", "H", "F", "ToyStore.db");

            var vm = new SettingsViewModel(graph, local);
            await vm.LoadAsync();

            vm.CommunicationPort = 5050;
            vm.AutoReconnect = false;
            vm.TaxRate = 8;
            vm.SelectedCurrency = "USD";
            vm.ReceiptHeader = "New Header";
            vm.ReceiptFooter = "New Footer";
            vm.SelectedItemsPerPage = 10;
            vm.StartOnLastOpened = true;

            await vm.SaveChangesCommand.ExecuteAsync(null);

            Assert.Equal("Settings saved successfully.", vm.StatusMessage);
            Assert.Equal(5050, local.GetInt(AppPreferenceKeys.CommunicationPort, -1));
            Assert.Equal(5050, local.GetInt(AppPreferenceKeys.LocalServerPort, -1));
            Assert.False(local.GetBool(AppPreferenceKeys.AutoReconnect, true));
            Assert.Equal(10, local.GetInt(AppPreferenceKeys.ProductsItemsPerPage, -1));
            Assert.True(local.GetBool(AppPreferenceKeys.StartOnLastOpened, false));
            Assert.False(vm.HasServerConfigurationChanges());
        }

        [Fact]
        public async Task CreateBackup_MissingDatabase_SetsErrorMessage()
        {
            var local = new InMemoryLocalSettingsService();
            var graph = new FakeGraphQlClient();
            graph.SetStoreSettings(0.1m, "VND", "H", "F", "missing-db-file.db");
            var vm = new SettingsViewModel(graph, local);

            await vm.LoadAsync();
            await vm.CreateBackupCommand.ExecuteAsync(null);

            Assert.Equal("Database file not found for backup.", vm.ErrorMessage);
        }

        [Fact]
        public async Task HasServerConfigurationChanges_DetectsPortOrReconnectChanges()
        {
            var local = new InMemoryLocalSettingsService();
            local.SetInt(AppPreferenceKeys.CommunicationPort, 5000);
            local.SetBool(AppPreferenceKeys.AutoReconnect, true);

            var graph = new FakeGraphQlClient();
            graph.SetStoreSettings(0.1m, "VND", "H", "F", "ToyStore.db");

            var vm = new SettingsViewModel(graph, local);
            await vm.LoadAsync();

            Assert.False(vm.HasServerConfigurationChanges());

            vm.CommunicationPort = 6001;
            Assert.True(vm.HasServerConfigurationChanges());

            vm.CommunicationPort = 5000;
            vm.AutoReconnect = false;
            Assert.True(vm.HasServerConfigurationChanges());
        }

        [Fact]
        public async Task RestoreSystem_LatestBackup_SchedulesPendingRestorePaths()
        {
            using var scope = new LocalAppDataScope();

            var backupDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BIF.ToyStore",
                "Backups");
            Directory.CreateDirectory(backupDirectory);

            var oldBackup = Path.Combine(backupDirectory, "ToyStore_20260101_000000.bak");
            var latestBackup = Path.Combine(backupDirectory, "ToyStore_20260102_000000.bak");
            await File.WriteAllTextAsync(oldBackup, "old");
            await Task.Delay(20);
            await File.WriteAllTextAsync(latestBackup, "new");

            var local = new InMemoryLocalSettingsService();
            var graph = new FakeGraphQlClient();
            graph.SetStoreSettings(0.1m, "VND", "H", "F", "ToyStore.db");
            var vm = new SettingsViewModel(graph, local);

            await vm.LoadAsync();
            await vm.RestoreSystemCommand.ExecuteAsync(null);

            Assert.Equal(latestBackup, local.GetString(AppPreferenceKeys.PendingRestoreBackupPath));
            Assert.Equal(
                Path.Combine(AppContext.BaseDirectory, "ToyStore.db"),
                local.GetString(AppPreferenceKeys.PendingRestoreTargetPath));
            Assert.Equal("Restore scheduled. Restart the app to apply restored data.", vm.StatusMessage);
            Assert.Equal(string.Empty, vm.ErrorMessage);
        }

        [Fact]
        public async Task CreateBackup_UsesVacuumInto_AndProducesReadableSnapshot()
        {
            using var scope = new LocalAppDataScope();

            var sourceDirectory = Path.Combine(Path.GetTempPath(), "BIFToyStoreTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(sourceDirectory);
            var sourceDbPath = Path.Combine(sourceDirectory, "ToyStore.db");

            await using (var sourceConnection = new SqliteConnection($"Data Source={sourceDbPath}"))
            {
                await sourceConnection.OpenAsync();
                await using var cmd = sourceConnection.CreateCommand();
                cmd.CommandText = "CREATE TABLE IF NOT EXISTS BackupProbe (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL); INSERT INTO BackupProbe(Name) VALUES ('snapshot-row');";
                await cmd.ExecuteNonQueryAsync();
            }

            var local = new InMemoryLocalSettingsService();
            var graph = new FakeGraphQlClient();
            graph.SetStoreSettings(0.1m, "VND", "H", "F", sourceDbPath);
            var vm = new SettingsViewModel(graph, local);

            await vm.LoadAsync();
            await vm.CreateBackupCommand.ExecuteAsync(null);

            var backupDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BIF.ToyStore",
                "Backups");

            var backupFile = new DirectoryInfo(backupDirectory)
                .GetFiles("*.bak")
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .FirstOrDefault();

            Assert.NotNull(backupFile);

            await using var backupConnection = new SqliteConnection($"Data Source={backupFile!.FullName}");
            await backupConnection.OpenAsync();
            await using var verify = backupConnection.CreateCommand();
            verify.CommandText = "SELECT Name FROM BackupProbe LIMIT 1;";
            var value = await verify.ExecuteScalarAsync();

            Assert.Equal("snapshot-row", value as string);
            Assert.Equal(string.Empty, vm.ErrorMessage);
        }

        private sealed class InMemoryLocalSettingsService : ILocalSettingsService
        {
            private readonly Dictionary<string, string> _stringValues = new(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<string, int> _intValues = new(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<string, bool> _boolValues = new(StringComparer.OrdinalIgnoreCase);

            public Dictionary<string, int> SetIntCalls { get; } = new(StringComparer.OrdinalIgnoreCase);

            public void SetString(string key, string value)
            {
                _stringValues[key] = value;
            }

            public string GetString(string key, string defaultValue = "")
            {
                return _stringValues.TryGetValue(key, out var value) ? value : defaultValue;
            }

            public void SetInt(string key, int value)
            {
                _intValues[key] = value;
                SetIntCalls[key] = value;
            }

            public int GetInt(string key, int defaultValue)
            {
                return _intValues.TryGetValue(key, out var value) ? value : defaultValue;
            }

            public void SetBool(string key, bool value)
            {
                _boolValues[key] = value;
            }

            public bool GetBool(string key, bool defaultValue)
            {
                return _boolValues.TryGetValue(key, out var value) ? value : defaultValue;
            }
        }

        private sealed class FakeGraphQlClient : IGraphQLClient
        {
            private decimal _taxRate;
            private string _currencySymbol = "VND";
            private string _receiptHeader = string.Empty;
            private string _receiptFooter = string.Empty;
            private string _databasePath = "ToyStore.db";

            public void SetStoreSettings(decimal taxRate, string currencySymbol, string receiptHeader, string receiptFooter, string databasePath)
            {
                _taxRate = taxRate;
                _currencySymbol = currencySymbol;
                _receiptHeader = receiptHeader;
                _receiptFooter = receiptFooter;
                _databasePath = databasePath;
            }

            public Task<T?> ExecuteAsync<T>(string query, object? variables = null, string dataKey = "")
            {
                var result = CreateStoreSettingsLikeObject<T>();
                return Task.FromResult(result);
            }

            public Task<T?> UploadFileAsync<T>(string query, string variableName, string filePath, string dataKey = "")
            {
                return Task.FromResult(default(T));
            }

            private T? CreateStoreSettingsLikeObject<T>()
            {
                var instance = Activator.CreateInstance(typeof(T));
                if (instance is null)
                {
                    return default;
                }

                SetPropertyIfExists(instance, "TaxRate", _taxRate);
                SetPropertyIfExists(instance, "CurrencySymbol", _currencySymbol);
                SetPropertyIfExists(instance, "ReceiptHeader", _receiptHeader);
                SetPropertyIfExists(instance, "ReceiptFooter", _receiptFooter);
                SetPropertyIfExists(instance, "DatabasePath", _databasePath);

                return (T)instance;
            }

            private static void SetPropertyIfExists(object target, string propertyName, object value)
            {
                var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property is null || !property.CanWrite)
                {
                    return;
                }

                if (property.PropertyType == typeof(decimal) && value is decimal d)
                {
                    property.SetValue(target, d);
                    return;
                }

                if (property.PropertyType == typeof(string) && value is string s)
                {
                    property.SetValue(target, s);
                }
            }
        }

        private sealed class LocalAppDataScope : IDisposable
        {
            private readonly string? _originalLocalAppData;
            private readonly string _tempRoot;

            public LocalAppDataScope()
            {
                _originalLocalAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
                _tempRoot = Path.Combine(Path.GetTempPath(), "BIFToyStoreTests", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(_tempRoot);
                Environment.SetEnvironmentVariable("LOCALAPPDATA", _tempRoot);
            }

            public void Dispose()
            {
                Environment.SetEnvironmentVariable("LOCALAPPDATA", _originalLocalAppData);
                if (Directory.Exists(_tempRoot))
                {
                    Directory.Delete(_tempRoot, recursive: true);
                }
            }
        }
    }
}
