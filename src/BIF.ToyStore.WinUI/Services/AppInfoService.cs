using BIF.ToyStore.Core.Interfaces;
using System;
using Windows.ApplicationModel;

namespace BIF.ToyStore.WinUI.Services
{
    public class AppInfoService : IAppInfoService
    {
        public string GetAppVersion()
        {
            try
            {
                var version = Package.Current.Id.Version;
                return $"Version {version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
            }
            catch (InvalidOperationException)
            {
                var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                if (version is null)
                {
                    return "Version 0.0.0.0";
                }

                return $"Version {version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
            }
        }
    }
}
