namespace BIF.ToyStore.Core.Interfaces
{
    public interface IPendingRestoreService
    {
        RestoreScheduleResult ScheduleLatestBackupRestore(string configuredDatabasePath);
        PendingRestoreApplyResult ApplyPendingRestoreIfScheduled();
    }

    public enum RestoreScheduleStatus
    {
        Scheduled,
        BackupDirectoryMissing,
        BackupFileMissing
    }

    public sealed record RestoreScheduleResult(
        RestoreScheduleStatus Status,
        string? BackupPath = null,
        string? TargetPath = null);

    public sealed record PendingRestoreApplyResult(bool Applied, string? Error = null);
}
