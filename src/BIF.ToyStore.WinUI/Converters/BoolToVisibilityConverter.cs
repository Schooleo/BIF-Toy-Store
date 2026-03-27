using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace BIF.ToyStore.WinUI.Converters
{
    public sealed class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool flag = value is bool b && b;
            bool invert = parameter is string s && string.Equals(s, "Invert", StringComparison.OrdinalIgnoreCase);
            return (flag ^ invert) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return value is Visibility v && v == Visibility.Visible;
        }
    }
}
