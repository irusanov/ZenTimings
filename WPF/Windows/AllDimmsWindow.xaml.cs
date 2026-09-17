using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ZenTimings.Windows
{
    public partial class AllDimmsWindow : ThemedAdonisWindow
    {
        // Follows the theme. The panels are pictures of the main window, so a theme applied while this window is
        // open has to take them again, or the old theme's colours stay on the new background.
        private static readonly DependencyProperty ThemeAccentProperty = DependencyProperty.Register(
            "ThemeAccent", typeof(object), typeof(AllDimmsWindow), new PropertyMetadata(null, OnThemeAccentChanged));

        private readonly Func<AllDimmsCapture.Result> capture;
        private bool rebuildQueued;

        internal AllDimmsWindow(Func<AllDimmsCapture.Result> capture)
        {
            InitializeComponent();
            this.capture = capture;

            MaxWidth = SystemParameters.WorkArea.Width;
            MaxHeight = SystemParameters.WorkArea.Height;

            // SizeToContent settles only after the first layout pass, so the window would flash at its
            // default size. Open it off-screen and move it over the owner once it has rendered.
            Left = -32000;
            Top = -32000;
            ContentRendered += (sender, e) => CenterOnOwner();

            Build();
            SetResourceReference(ThemeAccentProperty, "AccentTextColor");
        }

        private static void OnThemeAccentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var window = (AllDimmsWindow)d;
            if (e.OldValue == null || window.rebuildQueued)
                return;

            // Once the theme swap has settled and the main window has restyled. A capture that fails then closes
            // the window rather than leaving the old colours up.
            window.rebuildQueued = true;
            window.Dispatcher.BeginInvoke(new Action(() =>
            {
                window.rebuildQueued = false;
                try
                {
                    window.Build();
                }
                catch
                {
                    window.Close();
                }
            }), DispatcherPriority.Background);
        }

        private void Build()
        {
            AllDimmsCapture.Result result = capture();

            // The theme accent, which every theme keeps readable against its own panels.
            Color accent = ((SolidColorBrush)FindResource("AccentTextColor")).Color;
            var fill = new SolidColorBrush(Color.FromArgb(0x33, accent.R, accent.G, accent.B));
            var stroke = new SolidColorBrush(Color.FromArgb(0xCC, accent.R, accent.G, accent.B));
            fill.Freeze();
            stroke.Freeze();

            ChannelsPanel.Children.Clear();
            foreach (AllDimmsCapture.Channel channel in result.Channels)
                ChannelsPanel.Children.Add(BuildFrame(channel, result.Highlights, fill, stroke));

            ChannelsPanel.Columns = BalancedColumns();
        }

        private static Border BuildFrame(AllDimmsCapture.Channel channel, List<Rect> highlights, Brush fill, Brush stroke)
        {
            var texts = new StackPanel();
            texts.Children.Add(new TextBlock
            {
                Text = channel.Header,
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 2, 0, 2),
            });

            // Capped to the panel width, so a long line wraps instead of widening the column.
            foreach (string line in channel.ModuleLines)
            {
                texts.Children.Add(new TextBlock
                {
                    Text = line,
                    FontSize = 11,
                    Opacity = 0.7,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = channel.Image.Width,
                    Margin = new Thickness(0, 0, 0, 2),
                });
            }

            var overlay = new Canvas();
            foreach (Rect rect in highlights)
            {
                var marker = new Rectangle
                {
                    Width = rect.Width,
                    Height = rect.Height,
                    RadiusX = 2,
                    RadiusY = 2,
                    Fill = fill,
                    Stroke = stroke,
                };
                Canvas.SetLeft(marker, rect.X);
                Canvas.SetTop(marker, rect.Y);
                overlay.Children.Add(marker);
            }

            var panel = new Grid
            {
                Width = channel.Image.Width,
                Height = channel.Image.Height,
                Margin = new Thickness(0, 2, 0, 0),
            };
            panel.Children.Add(new Image { Source = channel.Image, Stretch = Stretch.None, SnapsToDevicePixels = true });
            panel.Children.Add(overlay);

            // Docked to the bottom, so panels in one grid row line up however many module lines they carry.
            var column = new DockPanel();
            DockPanel.SetDock(panel, Dock.Bottom);
            column.Children.Add(panel);
            column.Children.Add(texts);

            var frame = new Border
            {
                Child = column,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 6, 8, 8),
                Margin = new Thickness(4),
            };
            frame.SetResourceReference(Border.BorderBrushProperty, "SeparatorColor");
            return frame;
        }

        // As many panels per row as fit the screen, spread evenly: four channels make 2x2 rather than 3 + 1.
        private int BalancedColumns()
        {
            int count = ChannelsPanel.Children.Count;
            var frame = (FrameworkElement)ChannelsPanel.Children[0];
            frame.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            double available = MaxWidth - BorderThickness.Left - BorderThickness.Right
                - ChannelsPanel.Margin.Left - ChannelsPanel.Margin.Right - SystemParameters.VerticalScrollBarWidth;
            int perRow = Math.Max(1, (int)(available / frame.DesiredSize.Width));
            int rows = (count + perRow - 1) / perRow;

            return (count + rows - 1) / rows;
        }

        private void CenterOnOwner()
        {
            Rect area = SystemParameters.WorkArea;
            double left = Owner.Left + (Owner.ActualWidth - ActualWidth) / 2;
            double top = Owner.Top + (Owner.ActualHeight - ActualHeight) / 2;

            // WorkArea only describes the primary monitor, so clamp only when the owner is on it.
            if (area.Contains(new Point(Owner.Left + Owner.ActualWidth / 2, Owner.Top + Owner.ActualHeight / 2)))
            {
                left = Math.Max(area.Left, Math.Min(left, area.Right - ActualWidth));
                top = Math.Max(area.Top, Math.Min(top, area.Bottom - ActualHeight));
            }

            Left = left;
            Top = top;
        }
    }
}
