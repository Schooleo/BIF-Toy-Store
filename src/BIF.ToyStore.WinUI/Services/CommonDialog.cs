using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using System.Threading.Tasks;
using Windows.Foundation;

namespace BIF.ToyStore.WinUI.Services
{
    public enum CommonDialogType
    {
        Confirmation,
        Warning,
        Error,
        Information,
        Help
    }

    public static class CommonDialog
    {
        public static Task<ContentDialogResult> ShowAsync(
            XamlRoot xamlRoot,
            CommonDialogType type,
            string title,
            string message,
            string? primaryButtonText = "OK",
            string? closeButtonText = "Cancel",
            string? secondaryButtonText = null,
            ContentDialogButton defaultButton = ContentDialogButton.Primary,
            bool destructivePrimary = false)
        {
            var accentBrush = GetTypeBrush(type);
            var onSurfaceBrush = GetBrush("FluentPlayOnSurfaceBrush", Colors.Black);
            var onSurfaceVariantBrush = GetBrush("FluentPlayOnSurfaceVariantBrush", Color.FromArgb(255, 64, 71, 82));
            var primaryBrush = GetBrush("FluentPlayPrimaryBrush", Colors.DodgerBlue);
            var onPrimaryBrush = GetBrush("FluentPlayOnPrimaryBrush", Colors.White);
            var outlineBrush = GetBrush("FluentPlayOutlineVariantBrush", Color.FromArgb(255, 192, 199, 212));
            var primaryHoverBrush = GetBrush("FluentPlayPrimaryHoverBrush", Color.FromArgb(255, 0, 72, 131));
            var primaryPressedBrush = GetBrush("FluentPlayPrimaryPressedBrush", Color.FromArgb(255, 0, 57, 104));
            var destructiveBrush = GetBrush("FluentPlayErrorColor", Color.FromArgb(255, 186, 26, 26));
            var destructiveHoverBrush = new SolidColorBrush(Color.FromArgb(255, 168, 23, 23));
            var destructivePressedBrush = new SolidColorBrush(Color.FromArgb(255, 145, 20, 20));
            var whiteBrush = new SolidColorBrush(Colors.White);
            var neutralHoverBrush = GetBrush("FluentPlaySurfaceContainerLowBrush", Color.FromArgb(255, 243, 243, 243));
            var neutralPressedBrush = GetBrush("FluentPlaySurfaceContainerHighBrush", Color.FromArgb(255, 232, 232, 232));

            var headerIcon = new FontIcon
            {
                Glyph = GetTypeGlyph(type),
                Foreground = accentBrush,
                FontSize = 18,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };

            var headerText = new TextBlock
            {
                Text = title,
                Foreground = onSurfaceBrush,
                FontSize = 18,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };

            var headerPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            headerPanel.Children.Add(headerIcon);
            headerPanel.Children.Add(headerText);

            var contentText = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                Foreground = onSurfaceVariantBrush,
                FontSize = 14
            };

            var dialog = new ContentDialog
            {
                XamlRoot = xamlRoot,
                Title = headerPanel,
                DefaultButton = defaultButton,
                Content = contentText
            };

            if (!string.IsNullOrWhiteSpace(primaryButtonText))
            {
                dialog.PrimaryButtonText = primaryButtonText;
            }

            if (!string.IsNullOrWhiteSpace(closeButtonText))
            {
                dialog.CloseButtonText = closeButtonText;
            }

            if (!string.IsNullOrWhiteSpace(secondaryButtonText))
            {
                dialog.SecondaryButtonText = secondaryButtonText;
            }

            dialog.Resources["ContentDialogBackground"] = GetBrush("FluentPlaySurfaceBrush", Colors.White);
            dialog.Resources["ContentDialogTitleForeground"] = onSurfaceBrush;
            dialog.Resources["ContentDialogForeground"] = onSurfaceVariantBrush;

            // Normal dialog buttons (Cancel/Close): white surface with border and neutral gray hover.
            dialog.Resources["ButtonBackground"] = whiteBrush;
            dialog.Resources["ButtonForeground"] = onSurfaceVariantBrush;
            dialog.Resources["ButtonBorderBrush"] = outlineBrush;
            dialog.Resources["ButtonBackgroundPointerOver"] = neutralHoverBrush;
            dialog.Resources["ButtonBackgroundPressed"] = neutralPressedBrush;
            dialog.Resources["ButtonForegroundPointerOver"] = onSurfaceVariantBrush;
            dialog.Resources["ButtonForegroundPressed"] = onSurfaceVariantBrush;
            dialog.Resources["ButtonBorderBrushPointerOver"] = outlineBrush;
            dialog.Resources["ButtonBorderBrushPressed"] = outlineBrush;

            // Primary dialog button: blue by default, red for destructive actions.
            dialog.Resources["AccentButtonBackground"] = destructivePrimary ? destructiveBrush : primaryBrush;
            dialog.Resources["AccentButtonForeground"] = onPrimaryBrush;
            dialog.Resources["AccentButtonBorderBrush"] = destructivePrimary ? destructiveBrush : primaryBrush;
            dialog.Resources["AccentButtonBackgroundPointerOver"] = destructivePrimary ? destructiveHoverBrush : primaryHoverBrush;
            dialog.Resources["AccentButtonBackgroundPressed"] = destructivePrimary ? destructivePressedBrush : primaryPressedBrush;
            dialog.Resources["AccentButtonForegroundPointerOver"] = onPrimaryBrush;
            dialog.Resources["AccentButtonForegroundPressed"] = onPrimaryBrush;
            dialog.Resources["AccentButtonBorderBrushPointerOver"] = destructivePrimary ? destructiveHoverBrush : primaryHoverBrush;
            dialog.Resources["AccentButtonBorderBrushPressed"] = destructivePrimary ? destructivePressedBrush : primaryPressedBrush;

            IAsyncOperation<ContentDialogResult> operation = dialog.ShowAsync();
            var completion = new TaskCompletionSource<ContentDialogResult>();

            operation.Completed = (info, status) =>
            {
                if (status == AsyncStatus.Error)
                {
                    completion.TrySetException(info.ErrorCode);
                    return;
                }

                if (status == AsyncStatus.Canceled)
                {
                    completion.TrySetCanceled();
                    return;
                }

                completion.TrySetResult(info.GetResults());
            };

            return completion.Task;
        }

        private static Brush GetTypeBrush(CommonDialogType type)
        {
            return type switch
            {
                CommonDialogType.Confirmation => GetBrush("FluentPlayPrimaryBrush", Colors.DodgerBlue),
                CommonDialogType.Warning => GetBrush("FluentPlaySecondaryContainerColor", Color.FromArgb(255, 253, 183, 0)),
                CommonDialogType.Error => GetBrush("FluentPlayErrorColor", Color.FromArgb(255, 186, 26, 26)),
                CommonDialogType.Information => GetBrush("FluentPlayPrimaryFixedColor", Color.FromArgb(255, 211, 227, 255)),
                CommonDialogType.Help => GetBrush("FluentPlayPrimaryFixedDimColor", Color.FromArgb(255, 163, 201, 255)),
                _ => GetBrush("FluentPlayPrimaryBrush", Colors.DodgerBlue)
            };
        }

        private static string GetTypeGlyph(CommonDialogType type)
        {
            return type switch
            {
                CommonDialogType.Confirmation => "\uE73E",
                CommonDialogType.Warning => "\uE7BA",
                CommonDialogType.Error => "\uEA39",
                CommonDialogType.Information => "\uE946",
                CommonDialogType.Help => "\uE897",
                _ => "\uE946"
            };
        }

        private static Brush GetBrush(string resourceKey, Color fallbackColor)
        {
            if (Application.Current.Resources.TryGetValue(resourceKey, out var existingBrush)
                && existingBrush is Brush brush)
            {
                return brush;
            }

            if (Application.Current.Resources.TryGetValue(resourceKey, out var existingColor)
                && existingColor is Color color)
            {
                return new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));
            }

            return new SolidColorBrush(fallbackColor);
        }

    }
}
