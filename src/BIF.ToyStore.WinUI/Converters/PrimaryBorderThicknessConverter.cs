using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace BIF.ToyStore.WinUI.Converters
{
    public class PrimaryBorderThicknessConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool isPrimary && isPrimary)
            {
                return new Thickness(2);
            }
            return new Thickness(0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
