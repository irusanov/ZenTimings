using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ZenTimings.Theming
{
    /// <summary>
    /// Attached properties the control templates use for values a control has no property for.
    /// </summary>
    public static class ThemeAssist
    {
        /// <summary>Corner radius of a control's border. The control styles set it from ThemeDimensions.CornerRadius.</summary>
        public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.RegisterAttached(
            "CornerRadius", typeof(CornerRadius), typeof(ThemeAssist), new FrameworkPropertyMetadata(new CornerRadius()));

        public static CornerRadius GetCornerRadius(DependencyObject obj) => (CornerRadius)obj.GetValue(CornerRadiusProperty);

        public static void SetCornerRadius(DependencyObject obj, CornerRadius value) => obj.SetValue(CornerRadiusProperty, value);

        /// <summary>Background of a GroupBox header.</summary>
        public static readonly DependencyProperty HeaderBackgroundProperty = DependencyProperty.RegisterAttached(
            "HeaderBackground", typeof(System.Windows.Media.Brush), typeof(ThemeAssist), new FrameworkPropertyMetadata(null));

        public static System.Windows.Media.Brush GetHeaderBackground(DependencyObject obj) => (System.Windows.Media.Brush)obj.GetValue(HeaderBackgroundProperty);

        public static void SetHeaderBackground(DependencyObject obj, System.Windows.Media.Brush value) => obj.SetValue(HeaderBackgroundProperty, value);
    }

    /// <summary>
    /// Keeps only some corners of a <see cref="CornerRadius"/>. The parameter lists the corners to keep as four
    /// digits in the order top-left, top-right, bottom-right, bottom-left, e.g. "1100" for the top corners.
    /// </summary>
    public sealed class CornerRadiusMaskConverter : IValueConverter
    {
        public static readonly CornerRadiusMaskConverter Instance = new CornerRadiusMaskConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            CornerRadius radius = value is CornerRadius r ? r : new CornerRadius();
            string mask = parameter as string ?? "1111";

            return new CornerRadius(
                Keep(mask, 0) ? radius.TopLeft : 0,
                Keep(mask, 1) ? radius.TopRight : 0,
                Keep(mask, 2) ? radius.BottomRight : 0,
                Keep(mask, 3) ? radius.BottomLeft : 0);
        }

        private static bool Keep(string mask, int index) => mask.Length > index && mask[index] == '1';

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }
}
