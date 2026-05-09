using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Infrastructure.Services;

namespace BIF.ToyStore.Tests.Services
{
    public class RuntimeSettingsServiceTests
    {
        [Fact]
        public void GetResolvedDatabasePath_DefaultsToToyStoreDbUnderAppBaseDirectory()
        {
            var localSettings = new InMemoryLocalSettingsService();
            IRuntimeSettingsService service = new RuntimeSettingsService(localSettings, new DatabasePathService());

            var resolvedPath = service.GetResolvedDatabasePath();

            Assert.Equal(Path.Combine(AppContext.BaseDirectory, "ToyStore.db"), resolvedPath);
        }

        [Fact]
        public void SetConfiguredDatabasePath_BlankValue_FallsBackToToyStoreDb()
        {
            var localSettings = new InMemoryLocalSettingsService();
            IRuntimeSettingsService service = new RuntimeSettingsService(localSettings, new DatabasePathService());

            service.SetConfiguredDatabasePath("   ");

            Assert.Equal("ToyStore.db", service.GetConfiguredDatabasePath());
        }

        private sealed class InMemoryLocalSettingsService : ILocalSettingsService
        {
            private readonly Dictionary<string, string> _stringValues = new(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<string, int> _intValues = new(StringComparer.OrdinalIgnoreCase);

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
            }

            public int GetInt(string key, int defaultValue)
            {
                return _intValues.TryGetValue(key, out var value) ? value : defaultValue;
            }

            public void SetBool(string key, bool value)
            {
            }

            public bool GetBool(string key, bool defaultValue)
            {
                return defaultValue;
            }
        }
    }
}
