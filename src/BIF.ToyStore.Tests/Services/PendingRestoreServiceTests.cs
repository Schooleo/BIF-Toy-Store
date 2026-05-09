using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Infrastructure.Services;
using BIF.ToyStore.ViewModels.Utils;

namespace BIF.ToyStore.Tests.Services
{
    public class PendingRestoreServiceTests
    {
        [Fact]
        public void ScheduleLatestBackupRestore_LatestBackup_SetsPendingRestorePaths()
        {
            using var scope = new LocalAppDataScope();

            var backupDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BIF.ToyStore",
                "Backups");
            Directory.CreateDirectory(backupDirectory);

            var oldBackup = Path.Combine(backupDirectory, "ToyStore_20260101_000000.bak");
            var latestBackup = Path.Combine(backupDirectory, "ToyStore_20260102_000000.bak");
            File.WriteAllText(oldBackup, "old");
            Thread.Sleep(20);
            File.WriteAllText(latestBackup, "new");

            var local = new InMemoryLocalSettingsService();
            IPendingRestoreService service = new PendingRestoreService(local, new DatabasePathService());

            var result = service.ScheduleLatestBackupRestore("ToyStore.db");

            Assert.Equal(RestoreScheduleStatus.Scheduled, result.Status);
            Assert.Equal(latestBackup, local.GetString(AppPreferenceKeys.PendingRestoreBackupPath));
            Assert.Equal(
                Path.Combine(AppContext.BaseDirectory, "ToyStore.db"),
                local.GetString(AppPreferenceKeys.PendingRestoreTargetPath));
        }

        [Fact]
        public void ApplyPendingRestoreIfScheduled_CopiesBackupAndClearsPendingPaths()
        {
            using var scope = new LocalAppDataScope();

            var workingDirectory = Path.Combine(Path.GetTempPath(), "BIFToyStoreRestoreTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workingDirectory);

            var backupPath = Path.Combine(workingDirectory, "scheduled.bak");
            var targetPath = Path.Combine(workingDirectory, "ToyStore.db");
            File.WriteAllText(backupPath, "backup-bytes");

            var local = new InMemoryLocalSettingsService();
            local.SetString(AppPreferenceKeys.PendingRestoreBackupPath, backupPath);
            local.SetString(AppPreferenceKeys.PendingRestoreTargetPath, targetPath);

            IPendingRestoreService service = new PendingRestoreService(local, new DatabasePathService());

            var result = service.ApplyPendingRestoreIfScheduled();

            Assert.True(result.Applied);
            Assert.Null(result.Error);
            Assert.Equal("backup-bytes", File.ReadAllText(targetPath));
            Assert.Equal(string.Empty, local.GetString(AppPreferenceKeys.PendingRestoreBackupPath));
            Assert.Equal(string.Empty, local.GetString(AppPreferenceKeys.PendingRestoreTargetPath));
        }

        private sealed class InMemoryLocalSettingsService : ILocalSettingsService
        {
            private readonly Dictionary<string, string> _stringValues = new(StringComparer.OrdinalIgnoreCase);

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
            }

            public int GetInt(string key, int defaultValue)
            {
                return defaultValue;
            }

            public void SetBool(string key, bool value)
            {
            }

            public bool GetBool(string key, bool defaultValue)
            {
                return defaultValue;
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
