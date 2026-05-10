using BIF.ToyStore.Core.Interfaces;

namespace BIF.ToyStore.Infrastructure.Services
{
    public class RuntimeSettingsService : IRuntimeSettingsService
    {
        private const int DefaultLocalServerPort = 5000;
        private const string DefaultDatabasePath = "ToyStore.db";
        private const string LocalServerPortKey = "LocalServerPort";
        private const string DatabasePathKey = "DatabasePath";

        private readonly ILocalSettingsService _localSettingsService;
        private readonly IDatabasePathService _databasePathService;

        public RuntimeSettingsService(
            ILocalSettingsService localSettingsService,
            IDatabasePathService databasePathService)
        {
            _localSettingsService = localSettingsService;
            _databasePathService = databasePathService;
        }

        public int GetLocalServerPort()
        {
            return _localSettingsService.GetInt(LocalServerPortKey, DefaultLocalServerPort);
        }

        public void SetLocalServerPort(int port)
        {
            _localSettingsService.SetInt(LocalServerPortKey, port);
        }

        public string GetConfiguredDatabasePath()
        {
            var configuredPath = _localSettingsService.GetString(DatabasePathKey, DefaultDatabasePath);
            return string.IsNullOrWhiteSpace(configuredPath)
                ? DefaultDatabasePath
                : configuredPath.Trim();
        }

        public void SetConfiguredDatabasePath(string configuredPath)
        {
            var normalizedPath = string.IsNullOrWhiteSpace(configuredPath)
                ? DefaultDatabasePath
                : configuredPath.Trim();

            _localSettingsService.SetString(DatabasePathKey, normalizedPath);
        }

        public string GetResolvedDatabasePath()
        {
            return _databasePathService.ResolveDatabasePath(GetConfiguredDatabasePath());
        }
    }
}
