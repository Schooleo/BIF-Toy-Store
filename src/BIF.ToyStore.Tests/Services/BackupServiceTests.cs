using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Infrastructure.Services;
using Microsoft.Data.Sqlite;

namespace BIF.ToyStore.Tests.Services
{
    public class BackupServiceTests
    {
        [Fact]
        public async Task CreateBackupAsync_UsesVacuumInto_AndProducesReadableSnapshot()
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

            IBackupService service = new BackupService(new DatabasePathService());

            var result = await service.CreateBackupAsync(sourceDbPath);

            Assert.Equal(BackupCreationStatus.Success, result.Status);
            Assert.False(string.IsNullOrWhiteSpace(result.BackupPath));
            Assert.NotNull(result.CreatedAtUtc);

            await using var backupConnection = new SqliteConnection($"Data Source={result.BackupPath}");
            await backupConnection.OpenAsync();
            await using var verify = backupConnection.CreateCommand();
            verify.CommandText = "SELECT Name FROM BackupProbe LIMIT 1;";
            var value = await verify.ExecuteScalarAsync();

            Assert.Equal("snapshot-row", value as string);
        }

        [Fact]
        public async Task CreateBackupAsync_MissingDatabase_ReturnsDatabaseNotFound()
        {
            IBackupService service = new BackupService(new DatabasePathService());

            var result = await service.CreateBackupAsync("missing-db-file.db");

            Assert.Equal(BackupCreationStatus.DatabaseNotFound, result.Status);
            Assert.Null(result.BackupPath);
            Assert.Null(result.CreatedAtUtc);
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
