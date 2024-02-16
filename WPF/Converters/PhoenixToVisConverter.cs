using System;
using System.Globalization;
using System.Windows.Data;
using ZenStates.Core;

namespace ZenTimings.Converters
{
    public class PhoenixToVisConverter: IValueConverter
    {
        enum Parameters
        {
            Visible,
            Invisible,
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Cpu.CodeName codeName = (Cpu.CodeName)value;
            var direction = (Parameters)Enum.Parse(typeof(Parameters), (string)parameter);

            if (codeName == Cpu.CodeName.Phoenix || codeName == Cpu.CodeName.Phoenix2 || codeName == Cpu.CodeName.HawkPoint)
            {
                return direction == Parameters.Visible ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            }

            return direction == Parameters.Visible ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter,
            CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
