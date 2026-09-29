using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ZenStates.Core.Hardware.DRAM;
using ZenTimings.Common;
using ZenTimings.Settings;
using ZenTimings.Utils;

namespace ZenTimings.Windows
{
    public partial class AdvancedTimingsWindow : ThemedWindow
    {
        private class TimingGridItem
        {
            public string PropertyName { get; set; }
            public string[] Values { get; set; }
            public bool IsMismatch { get; set; }
        }

        // Fixed row and header heights make the number of rows that fit in a column predictable
        private const double GridFontSize = 11;
        private const double GridRowHeight = 20;
        private const double GridHeaderHeight = 24;
        private const double PanelGap = 8;
        // Cell border (1 + 1) + TextBlock padding (3 + 6 or 3 + 3) + 3px slack; the text is measured bold,
        // like mismatched rows. Keep in sync with the text and header styles in the XAML.
        private const double NameCellExtra = 2 + 9 + 3;
        private const double ValueCellExtra = 2 + 6 + 3;

        private const string NameHeader = "Timing";

        private List<TimingGridItem> _allRows = new List<TimingGridItem>();
        private List<TimingGridItem> _visibleRows = new List<TimingGridItem>();
        private readonly List<DataGrid> _panels = new List<DataGrid>();
        private string[] _valueHeaders = new string[0];
        private int _channelCount;

        private double _nameColumnWidth;
        private double _valueColumnWidth;
        private double _panelWidth;

        // Border and padding of the themed DataGrid, measured once the first panel is loaded
        private double _chromeWidth = 4;
        private double _chromeHeight = 4;
        private bool _chromeMeasured;

        // Last applied layout, to skip work while resizing doesn't change the flow
        private int _layoutCapacity = -1;
        private int _layoutPanels = -1;
        private bool _rowsChanged = true;
        private bool _initialWidthApplied;

        // Set when a saved size and position were restored, so the default two-panel width is not applied
        private bool _placementRestored;

        public AdvancedTimingsWindow()
        {
            InitializeComponent();
            Loaded += AdvancedTimingsWindow_Loaded;
            Closing += AdvancedTimingsWindow_Closing;
            RestoreWindowPlacement();
            LoadTimings();
        }

        private static double ScrollBarWidth => SystemParameters.VerticalScrollBarWidth;

        private void LoadTimings()
        {
            try
            {
                MemoryConfig memConfigs = CpuSingleton.Instance.GetMemoryConfig();
                SetMemorySticksSummary(memConfigs);

                var allTimings = memConfigs?.Timings;
                if (allTimings == null || allTimings.Count == 0)
                {
                    _allRows.Clear();
                    _valueHeaders = new string[0];
                    MeasureColumns();
                    ApplyFilter();
                    StatusText.Text = "No memory timings available.";
                    return;
                }

                var props = allTimings[0].Value.GetType().GetProperties();
                var uniqueTimings = allTimings
                    .GroupBy(t => t.Key)
                    .Select(g => g.First())
                    .ToList();

                _channelCount = uniqueTimings.Count;
                _valueHeaders = uniqueTimings.Select(t => $"DCT {t.Key >> 20}").ToArray();
                _allRows = props
                    .Where(p => p.Name != "Item")
                    .Select(property =>
                    {
                        var values = uniqueTimings.Select(t => $"{t.Value[property.Name]}").ToArray();
                        return new TimingGridItem
                        {
                            PropertyName = property.Name,
                            Values = values,
                            IsMismatch = HasMismatch(values)
                        };
                    })
                    .ToList();

                MeasureColumns();
                ApplyFilter();
            }
            catch (Exception ex)
            {
                StatusText.Text = "Failed to load advanced timings.";
                MessageBox.Show($"Failed to load advanced timings:{Environment.NewLine}{ex.Message}", "Advanced Timings", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region Column flow layout

        // Widths are measured over all rows, not just the filtered ones, so the columns don't jump
        // while typing in the search box, and all panels line up.
        private void MeasureColumns()
        {
            double nameWidth = MeasureText(NameHeader);
            double valueWidth = 0;

            foreach (string header in _valueHeaders)
                valueWidth = Math.Max(valueWidth, MeasureText(header));

            foreach (TimingGridItem row in _allRows)
            {
                nameWidth = Math.Max(nameWidth, MeasureText(row.PropertyName));
                foreach (string value in row.Values)
                    valueWidth = Math.Max(valueWidth, MeasureText(value));
            }

            _nameColumnWidth = Math.Ceiling(nameWidth + NameCellExtra);
            _valueColumnWidth = Math.Ceiling(valueWidth + ValueCellExtra);
            UpdatePanelWidth();
        }

        private void UpdatePanelWidth()
        {
            _panelWidth = _nameColumnWidth + _valueColumnWidth * _valueHeaders.Length + _chromeWidth;
        }

        // Measured bold, the widest weight the text styles use
        private double MeasureText(string text)
        {
            var typeface = new Typeface(FontFamily, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
            var formatted = new FormattedText(
                text ?? string.Empty,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                typeface,
                GridFontSize,
                Brushes.Black,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            return formatted.WidthIncludingTrailingWhitespace;
        }

        private DataGrid CreatePanel()
        {
            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.None,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Focusable = false,
                IsReadOnly = true,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                CanUserResizeRows = false,
                // Sorting or resizing a single panel would break the flow and the alignment between panels
                CanUserSortColumns = false,
                CanUserResizeColumns = false,
                CanUserReorderColumns = false,
                RowStyle = (Style)FindResource("TimingRowStyle"),
                FontSize = GridFontSize,
                RowHeight = GridRowHeight,
                ColumnHeaderHeight = GridHeaderHeight,
                VerticalAlignment = VerticalAlignment.Stretch,
            };

            grid.Columns.Add(new DataGridTextColumn
            {
                Header = NameHeader,
                Binding = new Binding(nameof(TimingGridItem.PropertyName)),
                ElementStyle = (Style)FindResource("TimingNameTextStyle"),
                HeaderStyle = (Style)FindResource("TimingNameHeaderStyle"),
                Width = new DataGridLength(_nameColumnWidth),
            });

            for (int i = 0; i < _valueHeaders.Length; i++)
            {
                grid.Columns.Add(new DataGridTextColumn
                {
                    Header = _valueHeaders[i],
                    Binding = new Binding($"Values[{i}]"),
                    ElementStyle = (Style)FindResource("TimingValueTextStyle"),
                    HeaderStyle = (Style)FindResource("TimingValueHeaderStyle"),
                    Width = new DataGridLength(_valueColumnWidth),
                });
            }

            grid.Loaded += Panel_Loaded;
            return grid;
        }

        // The theme's border and padding are only known once a grid is in the visual tree
        private void Panel_Loaded(object sender, RoutedEventArgs e)
        {
            if (_chromeMeasured || !(sender is DataGrid grid))
                return;

            _chromeMeasured = true;
            _chromeWidth = grid.BorderThickness.Left + grid.BorderThickness.Right + grid.Padding.Left + grid.Padding.Right + 2;
            _chromeHeight = grid.BorderThickness.Top + grid.BorderThickness.Bottom + grid.Padding.Top + grid.Padding.Bottom + 2;
            UpdatePanelWidth();

            Dispatcher.BeginInvoke(new Action(() => LayoutPanels(force: true)), DispatcherPriority.Loaded);
        }

        /// <summary>Rows that fit in one panel without scrolling.</summary>
        private int GetCapacity(double hostHeight)
        {
            return Math.Max(1, (int)Math.Floor((hostHeight - GridHeaderHeight - _chromeHeight) / GridRowHeight));
        }

        private void LayoutPanels(bool force = false)
        {
            if (TimingsHost == null || TimingsPanels == null || _panelWidth <= 0)
                return;

            double width = TimingsHost.ActualWidth;
            double height = TimingsHost.ActualHeight;
            if (width <= 0 || height <= 0)
                return;

            int rowCount = _visibleRows.Count;
            int capacity = GetCapacity(height);
            int maxPanels = Math.Max(1, (int)Math.Floor((width - ScrollBarWidth + PanelGap) / (_panelWidth + PanelGap)));
            int neededPanels = Math.Max(1, (rowCount + capacity - 1) / capacity);
            int panelCount = Math.Min(maxPanels, neededPanels);

            if (!force && !_rowsChanged && capacity == _layoutCapacity && panelCount == _layoutPanels)
                return;

            _layoutCapacity = capacity;
            _layoutPanels = panelCount;
            _rowsChanged = false;

            while (_panels.Count < panelCount)
                _panels.Add(CreatePanel());

            TimingsPanels.Children.Clear();

            for (int i = 0; i < panelCount; i++)
            {
                bool isLast = i == panelCount - 1;
                int start = i * capacity;
                int take = isLast ? rowCount - start : capacity;

                // Every panel but the last holds exactly as many rows as fit, the last takes the rest and scrolls
                DataGrid grid = _panels[i];
                grid.ItemsSource = take > 0 ? _visibleRows.GetRange(start, take) : new List<TimingGridItem>();
                grid.VerticalScrollBarVisibility = isLast ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
                grid.Width = _panelWidth + (isLast ? ScrollBarWidth : 0);
                grid.Margin = new Thickness(0, 0, isLast ? 0 : PanelGap, 0);

                TimingsPanels.Children.Add(grid);
            }

            for (int i = panelCount; i < _panels.Count; i++)
                _panels[i].ItemsSource = null;
        }

        private void TimingsHost_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            LayoutPanels();
        }

        private void AdvancedTimingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // After the first panel reported its border, so the widths are final
            Dispatcher.BeginInvoke(new Action(ApplyInitialWidth), DispatcherPriority.ContextIdle);
        }

        // Without a saved size and position the window opens auto-fitted, centered where it was opened
        private void ApplyInitialWidth()
        {
            if (_initialWidthApplied || !CanFitWidth())
                return;

            _initialWidthApplied = true;

            Rect workArea = GetWorkArea();
            MinWidth = Math.Min(workArea.Width, _panelWidth + ScrollBarWidth + ActualWidth - TimingsHost.ActualWidth);

            if (!_placementRestored)
                FitToContent(keepCentered: true);
        }

        #region Size and position

        // Uses the saved size and position when saving is enabled and the whole window would be on a screen,
        // the same rule as the other windows
        private void RestoreWindowPlacement()
        {
            AppSettings settings = AppSettings.Instance;
            if (!settings.SaveWindowPosition)
                return;

            double left = settings.AdvancedTimingsWindowLeft;
            double top = settings.AdvancedTimingsWindowTop;
            double width = settings.AdvancedTimingsWindowWidth;
            double height = settings.AdvancedTimingsWindowHeight;

            if (left == -1 || top == -1 || width <= 0 || height <= 0 || !IsOnVirtualScreen(left, top, width, height))
                return;

            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = left;
            Top = top;
            Width = Math.Max(width, MinWidth);
            Height = Math.Max(height, MinHeight);
            _placementRestored = true;
        }

        private void AdvancedTimingsWindow_Closing(object sender, CancelEventArgs e)
        {
            AppSettings settings = AppSettings.Instance;
            if (!settings.SaveWindowPosition)
                return;

            // A maximized or minimized window saves the size it returns to
            Rect bounds = WindowState == WindowState.Normal || RestoreBounds.IsEmpty
                ? new Rect(Left, Top, Width, Height)
                : RestoreBounds;

            if (double.IsNaN(bounds.Width) || double.IsNaN(bounds.Height) || bounds.Width <= 0 || bounds.Height <= 0)
                return;

            settings.AdvancedTimingsWindowLeft = bounds.Left;
            settings.AdvancedTimingsWindowTop = bounds.Top;
            settings.AdvancedTimingsWindowWidth = bounds.Width;
            settings.AdvancedTimingsWindowHeight = bounds.Height;
            settings.Save();
        }

        private static bool IsOnVirtualScreen(double left, double top, double width, double height)
        {
            double virtualLeft = SystemParameters.VirtualScreenLeft;
            double virtualTop = SystemParameters.VirtualScreenTop;
            double virtualRight = virtualLeft + SystemParameters.VirtualScreenWidth;
            double virtualBottom = virtualTop + SystemParameters.VirtualScreenHeight;

            return left >= virtualLeft && top >= virtualTop &&
                   left + width <= virtualRight && top + height <= virtualBottom;
        }

        #endregion

        private void AutoFit_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState != WindowState.Normal)
                WindowState = WindowState.Normal;

            UpdateLayout();
            if (CanFitWidth())
                FitToContent();
        }

        /// <summary>
        /// Sizes the window to show all shown timings without scrolling. Grows taller first, up to the
        /// screen height, and only adds panels side by side when one column can't hold them all. The rows
        /// are then spread evenly over the panels, so the height is no more than the fullest panel needs.
        /// </summary>
        private void FitToContent(bool keepCentered = false)
        {
            Rect workArea = GetWorkArea();
            double centerX = Left + ActualWidth / 2;
            double centerY = Top + ActualHeight / 2;
            int rowCount = Math.Max(1, _visibleRows.Count);

            double chromeHeight = ActualHeight - TimingsHost.ActualHeight;
            double maxHostHeight = Math.Max(0, workArea.Height - chromeHeight);

            int maxCapacity = GetCapacity(maxHostHeight);
            int panels = Math.Max(1, (rowCount + maxCapacity - 1) / maxCapacity);
            int rowsPerPanel = (rowCount + panels - 1) / panels;

            // Width first: a wider window can wrap the module summary into fewer lines, which changes the chrome height
            double chromeWidth = ActualWidth - TimingsHost.ActualWidth;
            double contentWidth = panels * _panelWidth + (panels - 1) * PanelGap + ScrollBarWidth;
            double newWidth = Math.Max(MinWidth, Math.Min(contentWidth + chromeWidth, workArea.Width));
            Width = newWidth;
            double left = keepCentered ? centerX - newWidth / 2 : Left;
            Left = Math.Max(workArea.Left, Math.Min(left, workArea.Right - newWidth));
            UpdateLayout();

            chromeHeight = ActualHeight - TimingsHost.ActualHeight;
            // One pixel of slack so layout rounding can't cost the last row
            double hostHeight = GridHeaderHeight + _chromeHeight + rowsPerPanel * GridRowHeight + 1;
            double newHeight = Math.Max(MinHeight, Math.Min(hostHeight + chromeHeight, workArea.Height));
            Height = newHeight;
            double top = keepCentered ? centerY - newHeight / 2 : Top;
            Top = Math.Max(workArea.Top, Math.Min(top, workArea.Bottom - newHeight));
        }

        private bool CanFitWidth()
        {
            return _panelWidth > 0 && TimingsHost.ActualWidth > 0 && TimingsHost.ActualHeight > 0;
        }

        // Work area of the monitor the window is on, in device independent units
        private Rect GetWorkArea()
        {
            try
            {
                IntPtr handle = new WindowInteropHelper(this).Handle;
                PresentationSource source = PresentationSource.FromVisual(this);
                if (handle != IntPtr.Zero && source?.CompositionTarget != null)
                {
                    System.Drawing.Rectangle area = System.Windows.Forms.Screen.FromHandle(handle).WorkingArea;
                    Matrix toDip = source.CompositionTarget.TransformFromDevice;
                    Point topLeft = toDip.Transform(new Point(area.Left, area.Top));
                    Point bottomRight = toDip.Transform(new Point(area.Right, area.Bottom));
                    return new Rect(topLeft, bottomRight);
                }
            }
            catch
            {
                // Fall back to the primary monitor
            }

            return SystemParameters.WorkArea;
        }

        #endregion

        private void SetMemorySticksSummary(MemoryConfig memoryConfig)
        {
            var modules = memoryConfig?.Modules;
            if (modules == null || modules.Count == 0)
            {
                MemorySticksText.Text = "N/A";
                return;
            }

            var descriptions = modules.Select((m, i) =>
            {
                var moduleText = m != null ? m.ToString() : string.Empty;
                if (string.IsNullOrWhiteSpace(moduleText))
                {
                    moduleText = !string.IsNullOrWhiteSpace(m?.Slot)
                        ? m.Slot
                        : !string.IsNullOrWhiteSpace(m?.DeviceLocator)
                            ? m.DeviceLocator
                            : $"DIMM {i}";
                }

                return $"{moduleText} (DCT {m.DctOffset >> 20})";
            });

            MemorySticksText.Text = string.Join(Environment.NewLine, descriptions);
        }

        private void ApplyFilter()
        {
            if (StatusText == null)
                return;

            var query = TimingSearchTextBox?.Text;
            var showDifferencesOnly = DifferencesOnlyCheckBox?.IsChecked == true;

            IEnumerable<TimingGridItem> rows = _allRows;

            if (!string.IsNullOrWhiteSpace(query))
            {
                var q = query.Trim();
                rows = rows.Where(r => r.PropertyName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            if (showDifferencesOnly)
                rows = rows.Where(r => r.IsMismatch);

            _visibleRows = rows.ToList();
            _rowsChanged = true;
            LayoutPanels();

            StatusText.Text = $"{_visibleRows.Count} timings shown across {_channelCount} channel(s).";
        }

        private void TimingSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void DifferencesOnlyCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            ApplyFilter();
        }

        // One table in display order, however the rows currently flow across the panels
        private void CopyTimings_Click(object sender, RoutedEventArgs e)
        {
            var text = new System.Text.StringBuilder();
            text.AppendLine(MemorySticksText.Text);
            text.AppendLine();

            text.AppendLine(string.Join("\t", new[] { NameHeader }.Concat(_valueHeaders)));
            foreach (TimingGridItem row in _visibleRows)
                text.AppendLine(string.Join("\t", new[] { row.PropertyName }.Concat(row.Values)));

            ClipboardUtils.Copy(text.ToString(), sender as Button);
        }

        private static bool HasMismatch(IReadOnlyList<string> values)
        {
            if (values == null || values.Count <= 1)
                return false;

            var first = values[0] ?? string.Empty;
            for (int i = 1; i < values.Count; i++)
                if (!string.Equals(first, values[i] ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                    return true;

            return false;
        }
    }
}
