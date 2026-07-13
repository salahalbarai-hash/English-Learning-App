using System.Globalization;

namespace English.Converters
{
    public class SelectedAnswerToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.Equals(
                System.Convert.ToString(value),
                System.Convert.ToString(parameter),
                StringComparison.Ordinal
            )
            ? Colors.LightGray
            : Colors.White;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
