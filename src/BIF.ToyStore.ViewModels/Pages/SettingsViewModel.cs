using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.ViewModels.Base;
using BIF.ToyStore.ViewModels.Utils;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace BIF.ToyStore.ViewModels.Pages
{
    public partial class SettingsViewModel : BaseViewModel
    {
        private readonly IGraphQLClient _graphQLClient;
        private readonly ILocalSettingsService _localSettingsService;

        private string _databasePath = "ToyStore.db";
        private int _originalCommunicationPort = 5000;
        private bool _originalAutoReconnect = true;

        [ObservableProperty]
        private double _communicationPort = 5000;

        [ObservableProperty]
        private bool _autoReconnect = true;

        [ObservableProperty]
        private double _taxRate = 10.0;

        [ObservableProperty]
        private string _selectedCurrency = "VND";

        [ObservableProperty]
        private string _receiptHeader = string.Empty;

        [ObservableProperty]
        private string _receiptFooter = string.Empty;

        [ObservableProperty]
        private int _selectedItemsPerPage = 20;

        [ObservableProperty]
        private bool _startOnLastOpened;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private string _lastBackupStatus = "NO BACKUP RECORDED";

        public IReadOnlyList<string> CurrencyOptions { get; } = ["VND", "USD"];

        public IReadOnlyList<int> ItemsPerPageOptions { get; } = [5, 10, 15, 20];

        public SettingsViewModel(IGraphQLClient graphQLClient, ILocalSettingsService localSettingsService)
        {
            _graphQLClient = graphQLClient;
            _localSettingsService = localSettingsService;
            Title = "Settings";
        }

        public async Task LoadAsync()
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            StatusMessage = string.Empty;

            try
            {
                LoadLocalSettings();
                await LoadStoreSettingsAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = "Unable to load settings: " + ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task SaveChangesAsync()
        {
            ErrorMessage = string.Empty;
            StatusMessage = string.Empty;

            if (!ValidateInputs())
            {
                return;
            }

            IsBusy = true;
            try
            {
                SaveLocalSettings();
                await SaveStoreSettingsAsync();
                StatusMessage = "Settings saved successfully.";
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to save settings: " + ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task DiscardChangesAsync()
        {
            await LoadAsync();
        }

        [RelayCommand]
        private async Task CreateBackupAsync()
        {
            ErrorMessage = string.Empty;
            StatusMessage = string.Empty;

            try
            {
                var sourcePath = ResolveDatabasePath(_databasePath);
                if (!File.Exists(sourcePath))
                {
                    ErrorMessage = "Database file not found for backup.";
                    return;
                }

                var backupDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "BIF.ToyStore",
                    "Backups");

                Directory.CreateDirectory(backupDirectory);

                var backupFileName = $"ToyStore_{DateTime.Now:yyyyMMdd_HHmmss}.bak";
                var destinationPath = Path.Combine(backupDirectory, backupFileName);

                await CreateVacuumBackupWithRetryAsync(sourcePath, destinationPath);

                var nowUtc = DateTime.UtcNow;
                _localSettingsService.SetString(AppPreferenceKeys.LastBackupUtc, nowUtc.ToString("o", CultureInfo.InvariantCulture));
                LastBackupStatus = FormatBackupStatus(nowUtc);

                StatusMessage = "Backup created successfully.";
            }
            catch (Exception ex)
            {
                ErrorMessage = "Backup failed: " + ex.Message;
            }
        }

        [RelayCommand]
        private async Task RestoreSystemAsync()
        {
            ErrorMessage = string.Empty;
            StatusMessage = string.Empty;

            try
            {
                var backupDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "BIF.ToyStore",
                    "Backups");

                if (!Directory.Exists(backupDirectory))
                {
                    ErrorMessage = "No backup directory found.";
                    return;
                }

                var latestBackup = new DirectoryInfo(backupDirectory)
                    .GetFiles("*.bak")
                    .OrderByDescending(file => file.LastWriteTimeUtc)
                    .FirstOrDefault();

                if (latestBackup is null)
                {
                    ErrorMessage = "No backup file found.";
                    return;
                }

                var destinationPath = ResolveDatabasePath(_databasePath);
                _localSettingsService.SetString(AppPreferenceKeys.PendingRestoreBackupPath, latestBackup.FullName);
                _localSettingsService.SetString(AppPreferenceKeys.PendingRestoreTargetPath, destinationPath);

                StatusMessage = "Restore scheduled. Restart the app to apply restored data.";
            }
            catch (Exception ex)
            {
                ErrorMessage = "Restore failed: " + ex.Message;
            }
        }

        private bool ValidateInputs()
        {
            var port = (int)Math.Round(CommunicationPort);
            if (port is < 1 or > 65535)
            {
                ErrorMessage = "Communication port must be between 1 and 65535.";
                return false;
            }

            if (TaxRate is < 0 or > 100)
            {
                ErrorMessage = "Tax rate must be between 0 and 100.";
                return false;
            }

            if (!CurrencyOptions.Contains(SelectedCurrency))
            {
                ErrorMessage = "Please select a supported currency.";
                return false;
            }

            if (!ItemsPerPageOptions.Contains(SelectedItemsPerPage))
            {
                ErrorMessage = "Items per page must be one of the available options.";
                return false;
            }

            return true;
        }

        private void LoadLocalSettings()
        {
            CommunicationPort = _localSettingsService.GetInt(
                AppPreferenceKeys.CommunicationPort,
                _localSettingsService.GetInt(AppPreferenceKeys.LocalServerPort, 5000));
            AutoReconnect = _localSettingsService.GetBool(AppPreferenceKeys.AutoReconnect, true);

            _originalCommunicationPort = (int)Math.Round(CommunicationPort);
            _originalAutoReconnect = AutoReconnect;

            SelectedItemsPerPage = _localSettingsService.GetInt(AppPreferenceKeys.ProductsItemsPerPage, 20);
            StartOnLastOpened = _localSettingsService.GetBool(AppPreferenceKeys.StartOnLastOpened, false);

            var persistedBackupUtc = ParseBackupUtc(_localSettingsService.GetString(AppPreferenceKeys.LastBackupUtc));
            var latestBackupUtc = GetLatestBackupTimestampUtc();
            var effectiveBackupUtc = MaxTimestamp(persistedBackupUtc, latestBackupUtc);

            if (effectiveBackupUtc.HasValue)
            {
                LastBackupStatus = FormatBackupStatus(effectiveBackupUtc.Value);
                _localSettingsService.SetString(
                    AppPreferenceKeys.LastBackupUtc,
                    effectiveBackupUtc.Value.ToString("o", CultureInfo.InvariantCulture));
            }
            else
            {
                LastBackupStatus = "NO BACKUP RECORDED";
            }
        }

        private void SaveLocalSettings()
        {
            var port = (int)Math.Round(CommunicationPort);
            _localSettingsService.SetInt(AppPreferenceKeys.CommunicationPort, port);
            _localSettingsService.SetInt(AppPreferenceKeys.LocalServerPort, port);
            _localSettingsService.SetBool(AppPreferenceKeys.AutoReconnect, AutoReconnect);
            _localSettingsService.SetInt(AppPreferenceKeys.ProductsItemsPerPage, SelectedItemsPerPage);
            _localSettingsService.SetBool(AppPreferenceKeys.StartOnLastOpened, StartOnLastOpened);

            _originalCommunicationPort = port;
            _originalAutoReconnect = AutoReconnect;
        }

        public bool HasServerConfigurationChanges()
        {
            var currentPort = (int)Math.Round(CommunicationPort);

            return currentPort != _originalCommunicationPort
                   || AutoReconnect != _originalAutoReconnect;
        }

        private async Task LoadStoreSettingsAsync()
        {
            const string query = @"query GetStoreSettings {
                appConfig {
                    taxRate
                    currencySymbol
                    receiptHeader
                    receiptFooter
                    databasePath
                }
            }";

            var config = await _graphQLClient.ExecuteAsync<StoreSettingsView>(query, dataKey: "appConfig");
            if (config is null)
            {
                return;
            }

            TaxRate = (double)(config.TaxRate * 100m);
            SelectedCurrency = string.IsNullOrWhiteSpace(config.CurrencySymbol) ? "VND" : config.CurrencySymbol;
            ReceiptHeader = config.ReceiptHeader;
            ReceiptFooter = config.ReceiptFooter;
            _databasePath = string.IsNullOrWhiteSpace(config.DatabasePath) ? "ToyStore.db" : config.DatabasePath;
        }

        private async Task SaveStoreSettingsAsync()
        {
            const string mutation = @"mutation UpdateStoreSettings($input: UpdateStoreSettingsInput!) {
                updateStoreSettings(input: $input) {
                    taxRate
                    currencySymbol
                    receiptHeader
                    receiptFooter
                    databasePath
                }
            }";

            var variables = new
            {
                input = new
                {
                    taxRate = decimal.Round((decimal)TaxRate / 100m, 4),
                    currencySymbol = SelectedCurrency,
                    receiptHeader = ReceiptHeader,
                    receiptFooter = ReceiptFooter
                }
            };

            var updated = await _graphQLClient.ExecuteAsync<StoreSettingsView>(
                mutation,
                variables,
                dataKey: "updateStoreSettings");

            if (updated is not null)
            {
                _databasePath = string.IsNullOrWhiteSpace(updated.DatabasePath) ? _databasePath : updated.DatabasePath;
            }
        }

        private static string ResolveDatabasePath(string configuredPath)
        {
            if (Path.IsPathRooted(configuredPath))
            {
                return configuredPath;
            }

            return Path.Combine(AppContext.BaseDirectory, configuredPath);
        }

        private static DateTime? ParseBackupUtc(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
            {
                return null;
            }

            return parsed.ToUniversalTime();
        }

        private static DateTime? MaxTimestamp(DateTime? left, DateTime? right)
        {
            if (!left.HasValue)
            {
                return right;
            }

            if (!right.HasValue)
            {
                return left;
            }

            return left.Value >= right.Value ? left : right;
        }

        private static string FormatBackupStatus(DateTime backupUtc)
        {
            var elapsed = DateTime.UtcNow - backupUtc;

            if (elapsed < TimeSpan.FromMinutes(1))
            {
                return "LAST: JUST NOW";
            }

            if (elapsed < TimeSpan.FromHours(1))
            {
                var minutes = Math.Max(1, (int)Math.Floor(elapsed.TotalMinutes));
                return $"LAST: {minutes} MIN{(minutes == 1 ? string.Empty : "S")} AGO";
            }

            if (elapsed < TimeSpan.FromDays(1))
            {
                var hours = Math.Max(1, (int)Math.Floor(elapsed.TotalHours));
                return $"LAST: {hours} HOUR{(hours == 1 ? string.Empty : "S")} AGO";
            }

            var days = Math.Max(1, (int)Math.Floor(elapsed.TotalDays));
            return $"LAST: {days} DAY{(days == 1 ? string.Empty : "S")} AGO";
        }

        private static DateTime? GetLatestBackupTimestampUtc()
        {
            var backupDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BIF.ToyStore",
                "Backups");

            if (!Directory.Exists(backupDirectory))
            {
                return null;
            }

            var latestBackup = new DirectoryInfo(backupDirectory)
                .GetFiles("*.bak")
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .FirstOrDefault();

            return latestBackup?.LastWriteTimeUtc;
        }

        private static async Task CopyWithRetryAsync(string sourcePath, string destinationPath)
        {
            const int maxAttempts = 5;
            const int retryDelayMs = 250;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    await using var sourceStream = new FileStream(
                        sourcePath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete);

                    await using var destinationStream = new FileStream(
                        destinationPath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None);

                    await sourceStream.CopyToAsync(destinationStream);
                    return;
                }
                catch (IOException) when (attempt < maxAttempts)
                {
                    await Task.Delay(retryDelayMs);
                }
                catch (UnauthorizedAccessException) when (attempt < maxAttempts)
                {
                    await Task.Delay(retryDelayMs);
                }
            }

            throw new IOException(
                "The database file is currently in use. Close other tools accessing the database and try backup again.");
        }

        private static async Task CreateVacuumBackupWithRetryAsync(string sourceDatabasePath, string backupDatabasePath)
        {
            const int maxAttempts = 5;
            const int retryDelayMs = 250;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    if (File.Exists(backupDatabasePath))
                    {
                        File.Delete(backupDatabasePath);
                    }

                    var escapedBackupPath = backupDatabasePath.Replace("'", "''", StringComparison.Ordinal);
                    var connectionString = new SqliteConnectionStringBuilder
                    {
                        DataSource = sourceDatabasePath,
                        Mode = SqliteOpenMode.ReadWrite
                    }.ToString();

                    await using var connection = new SqliteConnection(connectionString);
                    await connection.OpenAsync();

                    await using var command = connection.CreateCommand();
                    command.CommandText = $"VACUUM INTO '{escapedBackupPath}';";
                    await command.ExecuteNonQueryAsync();
                    return;
                }
                catch (SqliteException ex) when (
                    (ex.SqliteErrorCode == 5 || ex.SqliteErrorCode == 6) && attempt < maxAttempts)
                {
                    await Task.Delay(retryDelayMs);
                }
                catch (IOException) when (attempt < maxAttempts)
                {
                    await Task.Delay(retryDelayMs);
                }
                catch (UnauthorizedAccessException) when (attempt < maxAttempts)
                {
                    await Task.Delay(retryDelayMs);
                }
            }

            throw new IOException(
                "The database is currently busy. Try backup again in a moment.");
        }

        private sealed class StoreSettingsView
        {
            public decimal TaxRate { get; set; }
            public string CurrencySymbol { get; set; } = "VND";
            public string ReceiptHeader { get; set; } = string.Empty;
            public string ReceiptFooter { get; set; } = string.Empty;
            public string DatabasePath { get; set; } = "ToyStore.db";
        }
    }
}
