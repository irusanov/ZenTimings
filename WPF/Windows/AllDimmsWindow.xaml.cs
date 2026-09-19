using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using ZenStates.Core.Hardware.DRAM;
using ZenTimings.ViewModels;

namespace ZenTimings.Windows
{
    public partial class AllDimmsWindow : ThemedAdonisWindow
    {
        private readonly Func<AllDimmsCapture.Result> describe;
        private readonly Type panelType;
        private readonly MainViewModel sourceViewModel;
        private readonly Window centerOnWindow;
        private readonly List<ChannelFrame> frames = new List<ChannelFrame>();

        private sealed class ChannelFrame
        {
            public FrameworkElement Panel;
            public Canvas Overlay;
            public BaseDramTimings Timings;
        }

        internal AllDimmsWindow(Func<AllDimmsCapture.Result> describe, Type panelType, MainViewModel sourceViewModel, Window centerOnWindow)
        {
            InitializeComponent();
            this.describe = describe;
            this.panelType = panelType;
            this.sourceViewModel = sourceViewModel;
            this.centerOnWindow = centerOnWindow;

            MaxWidth = SystemParameters.WorkArea.Width;
            MaxHeight = SystemParameters.WorkArea.Height;

            // SizeToContent settles only after the first layout pass, so the window would flash at its
            // default size. Open it off-screen and move it over the reference window once it has rendered.
            Left = -32000;
            Top = -32000;
            ContentRendered += (sender, e) =>
            {
                CenterOnReferenceWindow();
                ApplyHighlights();
            };

            Build();
        }

        private void Build()
        {
            AllDimmsCapture.Result result = describe();

            ChannelsPanel.Children.Clear();
            frames.Clear();
            foreach (AllDimmsCapture.Channel channel in result.Channels)
                ChannelsPanel.Children.Add(BuildFrame(channel));

            ChannelsPanel.Columns = BalancedColumns();
        }

        private void ApplyHighlights()
        {
            if (frames.Count == 0)
                return;

            UpdateLayout();

            List<BaseDramTimings> timings = frames.Select(frame => frame.Timings).ToList();
            List<Rect> highlights = FindDifferingCells(frames[0].Panel, timings);
            foreach (ChannelFrame frame in frames)
            {
                ApplyMismatchForeground(frame.Panel, timings);

                frame.Overlay.Children.Clear();
                foreach (Rect rect in highlights)
                    AddHighlight(frame.Overlay, rect);
            }
        }

        private Border BuildFrame(AllDimmsCapture.Channel channel)
        {
            FrameworkElement panel = CreatePanel(channel);
            panel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            var texts = new StackPanel();
            //texts.Children.Add(new TextBlock
            //{
            //    Text = channel.Header,
            //    FontWeight = FontWeights.SemiBold,
            //    TextAlignment = TextAlignment.Center,
            //    Margin = new Thickness(0, 2, 0, 2),
            //});

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
                    MaxWidth = panel.DesiredSize.Width,
                    Margin = new Thickness(0, 2, 0, 2),
                });
            }

            var overlay = new Canvas { IsHitTestVisible = false };
            var panelHost = new Grid
            {
                Margin = new Thickness(0, 2, 0, 0),
            };
            panelHost.Children.Add(panel);
            panelHost.Children.Add(overlay);

            frames.Add(new ChannelFrame
            {
                Panel = panel,
                Overlay = overlay,
                Timings = channel.Timings,
            });

            // Docked to the bottom, so panels in one grid row line up however many module lines they carry.
            var column = new DockPanel();
            DockPanel.SetDock(panelHost, Dock.Bottom);
            column.Children.Add(panelHost);
            column.Children.Add(texts);

            var frame = new Border
            {
                Child = column,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(0),
                Margin = new Thickness(4),
            };
            frame.SetResourceReference(Border.BorderBrushProperty, "SeparatorColor");
            return frame;
        }

        private FrameworkElement CreatePanel(AllDimmsCapture.Channel channel)
        {
            var panel = (FrameworkElement)Activator.CreateInstance(panelType);
            panel.DataContext = sourceViewModel.CreateChannelViewModel(channel.Timings, channel.PmicData);
            return panel;
        }

        private void AddHighlight(Canvas overlay, Rect rect)
        {
            var fillMarker = new Rectangle
            {
                Width = rect.Width,
                Height = rect.Height,
                RadiusX = 2,
                RadiusY = 2,
            };

            fillMarker.SetResourceReference(Shape.FillProperty, "TimingMismatchBackgroundBrush");
            Canvas.SetLeft(fillMarker, rect.X);
            Canvas.SetTop(fillMarker, rect.Y);
            overlay.Children.Add(fillMarker);

            //var strokeMarker = new Rectangle
            //{
            //    Width = rect.Width,
            //    Height = rect.Height,
            //    RadiusX = 2,
            //    RadiusY = 2,
            //    Fill = Brushes.Transparent,
            //};
            //strokeMarker.Stroke = ResolveBrush("TimingMismatchBrush", "AccentTextColor");
            //Canvas.SetLeft(strokeMarker, rect.X);
            //Canvas.SetTop(strokeMarker, rect.Y);
            //overlay.Children.Add(strokeMarker);
        }

        private void ApplyMismatchForeground(FrameworkElement panel, List<BaseDramTimings> channels)
        {
            foreach (TextBlock text in Descendants(panel).OfType<TextBlock>())
            {
                string path = BindingOperations.GetBinding(text, TextBlock.TextProperty)?.Path?.Path;
                if (text.IsVisible && Differs(channels, path))
                {
                    text.SetResourceReference(TextBlock.ForegroundProperty, "TimingMismatchBrush");
                }
                else if (path != null && path.StartsWith("Timings.", StringComparison.Ordinal))
                {
                    text.ClearValue(TextBlock.ForegroundProperty);
                }
            }
        }

        private static List<Rect> FindDifferingCells(FrameworkElement panel, List<BaseDramTimings> channels)
        {
            return Descendants(panel)
                .OfType<TextBlock>()
                .Where(text => text.IsVisible && Differs(channels, BindingOperations.GetBinding(text, TextBlock.TextProperty)?.Path?.Path))
                .Select(text =>
                {
                    Rect bounds = text.TransformToAncestor(panel).TransformBounds(new Rect(text.RenderSize));
                    double horizontalInset = Math.Min(4, Math.Max(0, bounds.Width * 0.25));
                    double verticalInset = Math.Min(2, Math.Max(0, bounds.Height * 0.20));
                    bounds.Inflate(-horizontalInset, -verticalInset);
                    return bounds;
                })
                .ToList();
        }

        private static bool Differs(List<BaseDramTimings> channels, string path)
        {
            const string prefix = "Timings.";
            if (path == null || !path.StartsWith(prefix, StringComparison.Ordinal))
                return false;

            PropertyDescriptor property = TypeDescriptor.GetProperties(channels[0])[path.Substring(prefix.Length)];
            if (property == null)
                return false;

            object first = property.GetValue(channels[0]);
            return channels.Skip(1).Any(timings => !Equals(property.GetValue(timings), first));
        }

        private static IEnumerable<DependencyObject> Descendants(DependencyObject node)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(node, i);
                yield return child;

                foreach (DependencyObject descendant in Descendants(child))
                    yield return descendant;
            }
        }

        // As many panels per row as fit the screen, spread evenly: four channels make 2x2 rather than 3 + 1.
        private int BalancedColumns()
        {
            int count = ChannelsPanel.Children.Count;
            if (count == 0)
                return 1;

            var frame = (FrameworkElement)ChannelsPanel.Children[0];
            frame.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            double available = MaxWidth - BorderThickness.Left - BorderThickness.Right
                - ChannelsPanel.Margin.Left - ChannelsPanel.Margin.Right - SystemParameters.VerticalScrollBarWidth;
            int perRow = Math.Max(1, (int)(available / frame.DesiredSize.Width));
            int rows = (count + perRow - 1) / perRow;

            return (count + rows - 1) / rows;
        }

        private void CenterOnReferenceWindow()
        {
            if (centerOnWindow == null)
                return;

            Rect area = SystemParameters.WorkArea;
            double left = centerOnWindow.Left + (centerOnWindow.ActualWidth - ActualWidth) / 2;
            double top = centerOnWindow.Top + (centerOnWindow.ActualHeight - ActualHeight) / 2;

            // WorkArea only describes the primary monitor, so clamp only when the reference window is on it.
            if (area.Contains(new Point(centerOnWindow.Left + centerOnWindow.ActualWidth / 2, centerOnWindow.Top + centerOnWindow.ActualHeight / 2)))
            {
                left = Math.Max(area.Left, Math.Min(left, area.Right - ActualWidth));
                top = Math.Max(area.Top, Math.Min(top, area.Bottom - ActualHeight));
            }

            Left = left;
            Top = top;
        }
    }
}
