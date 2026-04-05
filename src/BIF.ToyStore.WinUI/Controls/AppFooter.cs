using BIF.ToyStore.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace BIF.ToyStore.WinUI.Controls
{
    public sealed class AppFooter : UserControl
    {
        public AppFooter()
        {
            var appInfoService = App.Current.Services.GetRequiredService<IAppInfoService>();

            Content = new TextBlock
            {
                Text = $"TOY STORE MANAGEMENT SYSTEM - {appInfoService.GetAppVersion().ToUpperInvariant()}",
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 165, 170, 177)),
                FontSize = 10,
                CharacterSpacing = 40
            };
        }
    }
}
