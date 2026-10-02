using System.Windows;
using System.Windows.Controls;

namespace ZenTimings.Controls
{
    /// <summary>
    /// A changelog line: a bullet icon, the text and an optional beta badge.
    /// </summary>
    public partial class ChangelogItem : UserControl
    {
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(ChangelogItem), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty IsBetaProperty =
            DependencyProperty.Register(nameof(IsBeta), typeof(bool), typeof(ChangelogItem), new PropertyMetadata(false));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public bool IsBeta
        {
            get => (bool)GetValue(IsBetaProperty);
            set => SetValue(IsBetaProperty, value);
        }

        public ChangelogItem()
        {
            InitializeComponent();
        }
    }
}
