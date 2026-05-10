using BIF.ToyStore.Core.Interfaces;
using Microsoft.Data.Sqlite;
using IOPath = System.IO.Path;

namespace BIF.ToyStore.Infrastructure.Services
{
    public class BackupService : IBackupService
    {
        private readonly IDatabasePathService _databasePathService;

        public BackupService(IDatabasePathService databasePathService)
        {
            _databasePathService = databasePathService;
        }

        public DateTime? GetLatestBackupTimestampUtc()
        {
            var backupDirectory = GetBackupDirectory();
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

        public async Task<BackupCreationResult> CreateBackupAsync(string configuredDatabasePath)
        {
            var sourcePath = _databasePathService.ResolveDatabasePath(configuredDatabasePath);
            if (!File.Exists(sourcePath))
            {
                return new BackupCreationResult(BackupCreationStatus.DatabaseNotFound);
            }

            var backupDirectory = GetBackupDirectory();
            Directory.CreateDirectory(backupDirectory);

            var backupFileName = $"ToyStore_{DateTime.Now:yyyyMMdd_HHmmss}.bak";
            var destinationPath = IOPath.Combine(backupDirectory, backupFileName);

            await CreateVacuumBackupWithRetryAsync(sourcePath, destinationPath);

            return new BackupCreationResult(
                BackupCreationStatus.Success,
                destinationPath,
                DateTime.UtcNow);
        }

        private static string GetBackupDirectory()
        {
            return IOPath.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BIF.ToyStore",
                "Backups");
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
    }
}
