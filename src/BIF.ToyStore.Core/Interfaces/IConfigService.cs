using BIF.ToyStore.Core.Models;
using BIF.ToyStore.Core.Settings;

namespace BIF.ToyStore.Core.Interfaces
{
    public interface IConfigService
    {
        Task<AppConfig> GetConfigAsync();
        Task<bool> IsInitialSetupCompletedAsync();
        Task<AppConfig> CompleteInitialSetupAsync(InitialSetupConfiguration setupConfiguration);
        Task<AppConfig> UpdateConfigAsync(string displayName, decimal taxRate, int localServerPort, string databasePath);
    }
}
