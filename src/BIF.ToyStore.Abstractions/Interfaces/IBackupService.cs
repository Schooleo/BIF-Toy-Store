namespace BIF.ToyStore.Core.Interfaces
{
    public interface IBackupService
    {
        DateTime? GetLatestBackupTimestampUtc();
        Task<BackupCreationResult> CreateBackupAsync(string configuredDatabasePath);
    }

    public enum BackupCreationStatus
    {
        Success,
        DatabaseNotFound
    }

    public sealed record BackupCreationResult(
        BackupCreationStatus Status,
        string? BackupPath = null,
        DateTime? CreatedAtUtc = null);
}
