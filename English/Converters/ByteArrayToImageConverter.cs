using Microsoft.Maui.Controls;
using System;
using System.Globalization;
using System.IO;

namespace English.Converters
{
    public class ByteArrayToImageConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not byte[] bytes || bytes.Length == 0)
                return null;

            return ImageSource.FromStream(() => new MemoryStream(bytes));
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
