using System;
using System.Globalization;
using System.Windows.Data;

namespace ZenTimings.Converters
{
    class FloatToNAConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value != null && (float)value == 0)
                return "N/A";
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
