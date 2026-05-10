using System;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;

namespace BIF.ToyStore.WinUI.Converters
{
    public class StringToImageSourceConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is not string source || string.IsNullOrWhiteSpace(source))
            {
                return null;
            }

            try
            {
                if (Uri.TryCreate(source, UriKind.Absolute, out var uri))
                {
                    return new BitmapImage(uri);
                }
            }
            catch
            {
                // Fallback or log error
            }
            
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
