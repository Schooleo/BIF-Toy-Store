namespace BIF.ToyStore.Core.Interfaces
{
    public interface IRuntimeSettingsService
    {
        int GetLocalServerPort();
        void SetLocalServerPort(int port);
        string GetConfiguredDatabasePath();
        void SetConfiguredDatabasePath(string configuredPath);
        string GetResolvedDatabasePath();
    }
}
