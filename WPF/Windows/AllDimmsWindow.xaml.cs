using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ZenStates.Core.Hardware.DRAM;
using ZenTimings.Controls;
using ZenTimings.ViewModels;

namespace ZenTimings.Windows
{
    public partial class AllDimmsWindow : ThemedAdonisWindow
    {
        private readonly Func<AllDimmsCapture.Result> describe;
        private readonly Func<FrameworkElement> createPanel;
        private readonly MainViewModel sourceViewModel;
        private readonly Window centerOnWindow;
        private readonly List<RenderedFrame> frames = new List<RenderedFrame>();

        private sealed class RenderedFrame
        {
            public FrameworkElement Panel;
            public Canvas Overlay;
            public BaseDramTimings Timings;
        }

        // createPanel builds a panel the way the main window builds its own, including the rows its code-behind fills.
        internal AllDimmsWindow(Func<AllDimmsCapture.Result> describe, Func<FrameworkElement> createPanel, MainViewModel sourceViewModel, Window centerOnWindow)
        {
            InitializeComponent();
            this.describe = describe;
            this.createPanel = createPanel ?? throw new ArgumentNullException(nameof(createPanel));
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
            foreach (AllDimmsCapture.DimmFrame dimm in result.Frames)
                ChannelsPanel.Children.Add(BuildFrame(dimm));

            ChannelsPanel.Columns = BalancedColumns();
        }

        private void ApplyHighlights()
        {
            if (frames.Count == 0)
                return;

            UpdateLayout();

            List<BaseDramTimings> timings = frames.Select(frame => frame.Timings).ToList();
            List<Rect> highlights = FindDifferingCells(frames[0].Panel, timings);
            foreach (RenderedFrame frame in frames)
            {
                ApplyMismatchForeground(frame.Panel, timings);

                frame.Overlay.Children.Clear();
                foreach (Rect rect in highlights)
                    AddHighlight(frame.Overlay, rect);
            }
        }

        private const double ModuleLogoMaxWidth = 40;
        private const double ModuleLogoMargin = 16;

        private Border BuildFrame(AllDimmsCapture.DimmFrame dimm)
        {
            FrameworkElement panel = CreatePanel(dimm);
            panel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            var texts = new StackPanel();
            texts.Children.Add(BuildModuleRow(dimm.Module, panel.DesiredSize.Width));

            var overlay = new Canvas { IsHitTestVisible = false };
            var panelHost = new Grid
            {
                Margin = new Thickness(0, 2, 0, 0),
            };
            panelHost.Children.Add(panel);
            panelHost.Children.Add(overlay);

            frames.Add(new RenderedFrame
            {
                Panel = panel,
                Overlay = overlay,
                Timings = dimm.Timings,
            });

            // Docked to the bottom, so panels in one grid row line up regardless of how tall their module row is.
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

        // One module: vendor logo (if known) on the left, vendor/part number and DRAM IC/PMIC stacked to its right.
        private static FrameworkElement BuildModuleRow(AllDimmsCapture.ModuleInfo module, double maxWidth)
        {
            bool hasLogo = !string.IsNullOrEmpty(module.LogoResourceName);
            double textMaxWidth = Math.Max(20, maxWidth - (hasLogo ? ModuleLogoMaxWidth + ModuleLogoMargin : 0));
            TextAlignment textAlignment = hasLogo ? TextAlignment.Left : TextAlignment.Center;

            var lines = new StackPanel
            {
                MaxWidth = textMaxWidth,
                VerticalAlignment = VerticalAlignment.Center,
            };

            if (!string.IsNullOrEmpty(module.VendorLine))
            {
                lines.Children.Add(new TextBlock
                {
                    Text = module.VendorLine,
                    FontSize = 11,
                    Opacity = 0.7,
                    TextAlignment = textAlignment,
                    TextWrapping = TextWrapping.Wrap,
                });
            }

            if (!string.IsNullOrEmpty(module.DetailLine))
            {
                lines.Children.Add(new TextBlock
                {
                    Text = module.DetailLine,
                    FontSize = 11,
                    Opacity = 0.7,
                    TextAlignment = textAlignment,
                    TextWrapping = TextWrapping.Wrap,
                });
            }

            if (!hasLogo)
            {
                lines.HorizontalAlignment = HorizontalAlignment.Center;
                lines.Margin = new Thickness(0, 2, 0, 2);
                return lines;
            }

            var image = new Image
            {
                MaxWidth = ModuleLogoMaxWidth,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, ModuleLogoMargin, 0),
            };
            image.SetResourceReference(Image.SourceProperty, module.LogoResourceName);

            var row = new DockPanel
            {
                LastChildFill = true,
                MaxWidth = maxWidth,
                Margin = new Thickness(0, 2, 0, 2),
            };
            DockPanel.SetDock(image, Dock.Left);
            row.Children.Add(image);
            row.Children.Add(lines);
            return row;
        }

        private FrameworkElement CreatePanel(AllDimmsCapture.DimmFrame dimm)
        {
            FrameworkElement panel = createPanel() ?? throw new InvalidOperationException("No timings panel for this memory type.");
            panel.DataContext = sourceViewModel.CreateChannelViewModel(dimm.Timings, dimm.PmicData, dimm.Capacity);
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
                string path = TimingRow.GetDisplayedBindingPath(text);
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
                .Where(text => text.IsVisible && Differs(channels, TimingRow.GetDisplayedBindingPath(text)))
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

        // A near-square grid: up to three channels share one row (1x1, 2x1, 3x1), four make 2x2, five to nine
        // a grid three wide, ten to sixteen four wide, and so on. Fewer columns when the screen is too narrow,
        // spread evenly so the last row isn't left with a single panel.
        private int BalancedColumns()
        {
            int count = ChannelsPanel.Children.Count;
            if (count == 0)
                return 1;

            int preferred = count <= 3 ? count : (int)Math.Ceiling(Math.Sqrt(count));

            var frame = (FrameworkElement)ChannelsPanel.Children[0];
            frame.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            double available = MaxWidth - BorderThickness.Left - BorderThickness.Right
                - ChannelsPanel.Margin.Left - ChannelsPanel.Margin.Right - SystemParameters.VerticalScrollBarWidth;
            int fits = Math.Max(1, (int)(available / frame.DesiredSize.Width));
            if (preferred <= fits)
                return preferred;

            int rows = (count + fits - 1) / fits;
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
