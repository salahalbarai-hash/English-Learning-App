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
            // ConvertBack is not needed for this converter in the UI bindings.
            // Return null to indicate no value is produced (avoids runtime exceptions).
            return null;
        }
    }
}
