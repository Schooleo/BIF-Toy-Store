using BIF.ToyStore.Core.Interfaces;
using IOPath = System.IO.Path;

namespace BIF.ToyStore.Infrastructure.Services
{
    public class DatabasePathService : IDatabasePathService
    {
        public string ResolveDatabasePath(string configuredPath)
        {
            if (IOPath.IsPathRooted(configuredPath))
            {
                return configuredPath;
            }

            return IOPath.Combine(AppContext.BaseDirectory, configuredPath);
        }
    }
}
