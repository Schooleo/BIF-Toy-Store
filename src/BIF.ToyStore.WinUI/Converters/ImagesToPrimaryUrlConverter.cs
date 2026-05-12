using BIF.ToyStore.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BIF.ToyStore.WinUI.Converters
{
    public class ImagesToPrimaryUrlConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            string url = string.Empty;
            if (value is ICollection<ProductImage> images)
            {
                url = images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl ?? images.FirstOrDefault()?.ImageUrl ?? string.Empty;
            }

            string? param = parameter as string;
            if (param == "visibility")
            {
                return string.IsNullOrEmpty(url) ? Visibility.Visible : Visibility.Collapsed;
            }
            if (param == "visibility_inv")
            {
                return string.IsNullOrEmpty(url) ? Visibility.Collapsed : Visibility.Visible;
            }

            if (!string.IsNullOrEmpty(url))
            {
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    return new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(uri);
                }
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
