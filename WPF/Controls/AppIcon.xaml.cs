using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ZenTimings.Controls
{
    /// <summary>
    /// A reusable icon control that renders a shared vector icon from the
    /// AppIcons resource dictionary, selected by its <see cref="Kind"/> key.
    /// </summary>
    public partial class AppIcon : UserControl
    {
        public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
            nameof(Kind), typeof(string), typeof(AppIcon),
            new PropertyMetadata(null, OnKindChanged));

        public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
            nameof(Data), typeof(Geometry), typeof(AppIcon), new PropertyMetadata(null));

        public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
            nameof(Fill), typeof(Brush), typeof(AppIcon), new PropertyMetadata(Brushes.Black));

        public static readonly DependencyProperty IconWidthProperty = DependencyProperty.Register(
            nameof(IconWidth), typeof(double), typeof(AppIcon), new PropertyMetadata(12.0));

        public static readonly DependencyProperty IconHeightProperty = DependencyProperty.Register(
            nameof(IconHeight), typeof(double), typeof(AppIcon), new PropertyMetadata(12.0));

        public AppIcon()
        {
            InitializeComponent();
        }

        /// <summary>
        /// The resource key of the icon geometry (e.g. "IconVoltage") defined in AppIcons.xaml.
        /// </summary>
        public string Kind
        {
            get => (string)GetValue(KindProperty);
            set => SetValue(KindProperty, value);
        }

        public Geometry Data
        {
            get => (Geometry)GetValue(DataProperty);
            set => SetValue(DataProperty, value);
        }

        public Brush Fill
        {
            get => (Brush)GetValue(FillProperty);
            set => SetValue(FillProperty, value);
        }

        public double IconWidth
        {
            get => (double)GetValue(IconWidthProperty);
            set => SetValue(IconWidthProperty, value);
        }

        public double IconHeight
        {
            get => (double)GetValue(IconHeightProperty);
            set => SetValue(IconHeightProperty, value);
        }

        private static void OnKindChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var icon = (AppIcon)d;
            var key = e.NewValue as string;
            if (string.IsNullOrEmpty(key))
            {
                icon.Data = null;
                return;
            }

            if (icon.TryFindResource(key) is Geometry geometry)
            {
                icon.Data = geometry;
            }
        }
    }
}
