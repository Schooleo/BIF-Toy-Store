using Microsoft.UI.Xaml.Data;
using System;

namespace BIF.ToyStore.WinUI.Converters
{
    /// <summary>
    /// Converts stock quantity (int) to formatted string: "{stock} Units"
    /// </summary>
    public class StockFormatConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, string language)
        {
            if (value is int stock)
            {
                return $"{stock} Units";
            }
            return string.Empty;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
