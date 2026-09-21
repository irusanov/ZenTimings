using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ZenTimings.Controls
{
    /// <summary>
    /// A label and its value as one element, so a timings panel is a single list of rows that can be
    /// reordered freely instead of two parallel label/value columns that must stay in step.
    /// <para>
    /// Rows line up within the nearest ancestor with <c>Grid.IsSharedSizeScope="True"</c>. Setting
    /// <see cref="UIElement.IsEnabled"/> greys out the whole row, label and value together, e.g. when
    /// the value isn't available.
    /// </para>
    /// </summary>
    public partial class TimingRow : UserControl
    {
        public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
            nameof(Label), typeof(string), typeof(TimingRow), new PropertyMetadata(null));

        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
            nameof(Value), typeof(string), typeof(TimingRow), new PropertyMetadata(null));

        public static readonly DependencyProperty LabelToolTipProperty = DependencyProperty.Register(
            nameof(LabelToolTip), typeof(object), typeof(TimingRow), new PropertyMetadata(null));

        public static readonly DependencyProperty ValueToolTipProperty = DependencyProperty.Register(
            nameof(ValueToolTip), typeof(object), typeof(TimingRow), new PropertyMetadata(null));

        public static readonly DependencyProperty ValueMinWidthProperty = DependencyProperty.Register(
            nameof(ValueMinWidth), typeof(double), typeof(TimingRow), new PropertyMetadata(0.0, OnValueMinWidthChanged));

        public TimingRow()
        {
            InitializeComponent();
            ValueColumn.MinWidth = ValueMinWidth;
        }

        /// <summary>Row caption. A string so bindings to it may use StringFormat.</summary>
        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        /// <summary>Displayed value. A string so bindings to it may use StringFormat; "N/A" renders greyed out.</summary>
        public string Value
        {
            get => (string)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public object LabelToolTip
        {
            get => GetValue(LabelToolTipProperty);
            set => SetValue(LabelToolTipProperty, value);
        }

        public object ValueToolTip
        {
            get => GetValue(ValueToolTipProperty);
            set => SetValue(ValueToolTipProperty, value);
        }

        /// <summary>Minimum width of the value column, e.g. set for a whole group through an implicit style.</summary>
        public double ValueMinWidth
        {
            get => (double)GetValue(ValueMinWidthProperty);
            set => SetValue(ValueMinWidthProperty, value);
        }

        // A ColumnDefinition isn't in the visual tree, so it can't reach the row through RelativeSource.
        private static void OnValueMinWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            TimingRow row = (TimingRow)d;
            if (row.ValueColumn != null)
                row.ValueColumn.MinWidth = (double)e.NewValue;
        }

        /// <summary>The TextBlock that renders <see cref="Value"/>, for callers that measure or recolour it.</summary>
        public TextBlock ValueTextBlock => ValueBlock;

        /// <summary>
        /// Path of the binding behind the text <paramref name="text"/> shows. For a row's value block that is
        /// the row's own <see cref="Value"/> binding (e.g. "Timings.CL"), not the internal link to the row;
        /// for any other TextBlock it is its Text binding. Null when unbound.
        /// </summary>
        public static string GetDisplayedBindingPath(TextBlock text)
        {
            if (text == null)
                return null;

            for (DependencyObject parent = VisualTreeHelper.GetParent(text); parent != null; parent = VisualTreeHelper.GetParent(parent))
            {
                if (parent is TimingRow row)
                {
                    return ReferenceEquals(row.ValueBlock, text)
                        ? BindingOperations.GetBinding(row, ValueProperty)?.Path?.Path
                        : null;
                }
            }

            return BindingOperations.GetBinding(text, TextBlock.TextProperty)?.Path?.Path;
        }
    }
}
