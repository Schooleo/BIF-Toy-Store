using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;

namespace BIF.ToyStore.WinUI.Converters
{
    public class OrderStatusTagBrushConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, string language)
        {
            var status = NormalizeStatus(value?.ToString());
            var useForeground = string.Equals(parameter?.ToString(), "Foreground", StringComparison.OrdinalIgnoreCase);

            var (backgroundHex, foregroundHex) = status switch
            {
                "paid" => ("#F2CF93", "#6A4A00"),
                "cancelled" => ("#FFD9D6", "#C62828"),
                "canceled" => ("#FFD9D6", "#C62828"),
                "pending" => ("#F2CF93", "#6A4A00"),
                "new" => ("#E0E0E0", "#0B4F8A"),
                _ => ("#ECEFF1", "#455A64")
            };

            return new SolidColorBrush(ParseHexColor(useForeground ? foregroundHex : backgroundHex));
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, string language)
        {
            throw new NotImplementedException();
        }

        private static string NormalizeStatus(string? status)
        {
            return (status ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static Windows.UI.Color ParseHexColor(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
            {
                return ColorHelper.FromArgb(255, 236, 239, 241);
            }

            var raw = hex.TrimStart('#');
            if (raw.Length == 6)
            {
                var r = System.Convert.ToByte(raw.Substring(0, 2), 16);
                var g = System.Convert.ToByte(raw.Substring(2, 2), 16);
                var b = System.Convert.ToByte(raw.Substring(4, 2), 16);
                return ColorHelper.FromArgb(255, r, g, b);
            }

            if (raw.Length == 8)
            {
                var a = System.Convert.ToByte(raw.Substring(0, 2), 16);
                var r = System.Convert.ToByte(raw.Substring(2, 2), 16);
                var g = System.Convert.ToByte(raw.Substring(4, 2), 16);
                var b = System.Convert.ToByte(raw.Substring(6, 2), 16);
                return ColorHelper.FromArgb(a, r, g, b);
            }

            return ColorHelper.FromArgb(255, 236, 239, 241);
        }
    }
}
