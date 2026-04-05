using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace BIF.ToyStore.WinUI.Converters
{
    public class InverseStringNullOrEmptyToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return string.IsNullOrWhiteSpace(value as string)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
