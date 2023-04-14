using System;
using System.Globalization;
using System.Windows.Data;

namespace ZenTimings.Converters
{
    class DDR5ToVisConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if ((MemoryConfig.MemType)value >= MemoryConfig.MemType.DDR5)
            {
                return System.Windows.Visibility.Visible;
            }
            return System.Windows.Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter,
            CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}