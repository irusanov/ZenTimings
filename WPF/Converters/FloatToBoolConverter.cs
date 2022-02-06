using System;
using System.Globalization;
using System.Windows.Data;

namespace ZenTimings.Converters
{
    public class FloatToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null && (float)value != 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter,
            CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
