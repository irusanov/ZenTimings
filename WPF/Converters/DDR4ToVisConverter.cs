using System;
using System.Globalization;
using System.Windows.Data;
using ZenStates.Core.DRAM;

namespace ZenTimings.Converters
{
    class DDR4ToVisConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if ((MemType)value == MemType.DDR4 || (MemType)value == MemType.LPDDR4)
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