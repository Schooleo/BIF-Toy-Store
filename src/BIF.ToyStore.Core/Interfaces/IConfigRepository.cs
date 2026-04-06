using BIF.ToyStore.Core.Settings;

namespace BIF.ToyStore.Core.Interfaces
{
    public interface IConfigRepository
    {
        Task<AppConfig?> GetSingletonNoTrackingAsync();

        Task<AppConfig> GetOrCreateSingletonTrackedAsync();

        Task SaveChangesAsync();
    }
}
