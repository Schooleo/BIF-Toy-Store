using BIF.ToyStore.Core.Interfaces;
using System.IO;
using IOPath = System.IO.Path;

namespace BIF.ToyStore.Infrastructure.Services
{
    public class DatabasePathService : IDatabasePathService
    {
        private const string AppDataFolderName = "BIF.ToyStore";

        public string ResolveDatabasePath(string configuredPath)
        {
            if (IOPath.IsPathRooted(configuredPath))
            {
                return configuredPath;
            }

            var baseDirectory = GetWritableBaseDirectory();
            var resolvedPath = IOPath.Combine(baseDirectory, configuredPath);
            var resolvedDirectory = IOPath.GetDirectoryName(resolvedPath);

            if (!string.IsNullOrWhiteSpace(resolvedDirectory))
            {
                Directory.CreateDirectory(resolvedDirectory);
            }

            return resolvedPath;
        }

        private static string GetWritableBaseDirectory()
        {
            var localAppDataPath = Environment.GetEnvironmentVariable("LOCALAPPDATA");
            if (string.IsNullOrWhiteSpace(localAppDataPath))
            {
                localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            }

            return IOPath.Combine(localAppDataPath, AppDataFolderName);
        }
    }
}
