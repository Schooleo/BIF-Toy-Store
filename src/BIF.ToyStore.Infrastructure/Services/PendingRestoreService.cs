using BIF.ToyStore.Core.Interfaces;
using IOPath = System.IO.Path;

namespace BIF.ToyStore.Infrastructure.Services
{
    public class PendingRestoreService : IPendingRestoreService
    {
        private const string PendingRestoreBackupPathKey = "PendingRestoreBackupPath";
        private const string PendingRestoreTargetPathKey = "PendingRestoreTargetPath";

        private readonly ILocalSettingsService _localSettingsService;
        private readonly IDatabasePathService _databasePathService;

        public PendingRestoreService(
            ILocalSettingsService localSettingsService,
            IDatabasePathService databasePathService)
        {
            _localSettingsService = localSettingsService;
            _databasePathService = databasePathService;
        }

        public RestoreScheduleResult ScheduleLatestBackupRestore(string configuredDatabasePath)
        {
            var backupDirectory = GetBackupDirectory();
            if (!Directory.Exists(backupDirectory))
            {
                return new RestoreScheduleResult(RestoreScheduleStatus.BackupDirectoryMissing);
            }

            var latestBackup = new DirectoryInfo(backupDirectory)
                .GetFiles("*.bak")
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .FirstOrDefault();

            if (latestBackup is null)
            {
                return new RestoreScheduleResult(RestoreScheduleStatus.BackupFileMissing);
            }

            var destinationPath = _databasePathService.ResolveDatabasePath(configuredDatabasePath);
            _localSettingsService.SetString(PendingRestoreBackupPathKey, latestBackup.FullName);
            _localSettingsService.SetString(PendingRestoreTargetPathKey, destinationPath);

            return new RestoreScheduleResult(
                RestoreScheduleStatus.Scheduled,
                latestBackup.FullName,
                destinationPath);
        }

        public PendingRestoreApplyResult ApplyPendingRestoreIfScheduled()
        {
            var backupPath = _localSettingsService.GetString(PendingRestoreBackupPathKey);
            var targetPath = _localSettingsService.GetString(PendingRestoreTargetPathKey);

            if (string.IsNullOrWhiteSpace(backupPath) || string.IsNullOrWhiteSpace(targetPath))
            {
                return new PendingRestoreApplyResult(false);
            }

            try
            {
                if (!File.Exists(backupPath))
                {
                    return new PendingRestoreApplyResult(false, "Backup file was not found.");
                }

                Directory.CreateDirectory(IOPath.GetDirectoryName(targetPath) ?? AppContext.BaseDirectory);
                File.Copy(backupPath, targetPath, overwrite: true);

                _localSettingsService.SetString(PendingRestoreBackupPathKey, string.Empty);
                _localSettingsService.SetString(PendingRestoreTargetPathKey, string.Empty);

                return new PendingRestoreApplyResult(true);
            }
            catch (Exception ex)
            {
                return new PendingRestoreApplyResult(false, ex.Message);
            }
        }

        private static string GetBackupDirectory()
        {
            return IOPath.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BIF.ToyStore",
                "Backups");
        }
    }
}
