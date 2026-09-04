using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ZenStates.Core.Hardware.DRAM;

namespace ZenTimings.Windows
{
    public partial class AdvancedTimingsWindow : ThemedAdonisWindow
    {
        private class TimingGridItem
        {
            public string PropertyName { get; set; }
            public string[] Values { get; set; }
            public bool IsMismatch { get; set; }
        }

        private List<TimingGridItem> _allRows = new List<TimingGridItem>();
        private List<TimingGridItem> _baseRows = new List<TimingGridItem>();
        private List<TimingGridItem> _extendedRows = new List<TimingGridItem>();
        private int _channelCount;

        public AdvancedTimingsWindow()
        {
            InitializeComponent();
            LoadTimings();
        }

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
                    _baseRows.Clear();
                    _extendedRows.Clear();
                    BaseTimingsGrid.ItemsSource = _baseRows;
                    ExtendedTimingsGrid.ItemsSource = _extendedRows;
                    StatusText.Text = "No memory timings available.";
                    return;
                }

                var props = allTimings[0].Value.GetType().GetProperties();
                var uniqueTimings = allTimings
                    .GroupBy(t => t.Key)
                    .Select(g => g.First())
                    .ToList();

                _channelCount = uniqueTimings.Count;
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

                var splitIndex = GetExtendedStartIndex(_allRows);
                _baseRows = splitIndex > 0 ? _allRows.Take(splitIndex).ToList() : new List<TimingGridItem>(_allRows);
                _extendedRows = splitIndex >= 0 ? _allRows.Skip(splitIndex).ToList() : new List<TimingGridItem>();

                BuildColumns(uniqueTimings);
                ApplyFilter();
            }
            catch (Exception ex)
            {
                StatusText.Text = "Failed to load advanced timings.";
                MessageBox.Show($"Failed to load advanced timings:{Environment.NewLine}{ex.Message}", "Advanced Timings", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BuildColumns(List<KeyValuePair<uint, BaseDramTimings>> uniqueTimings)
        {
            ConfigureGridColumns(ExtendedTimingsGrid, uniqueTimings, "Timing");
            ConfigureGridColumns(BaseTimingsGrid, uniqueTimings, "Timing");
        }

        private void ConfigureGridColumns(DataGrid grid, List<KeyValuePair<uint, BaseDramTimings>> uniqueTimings, string nameHeader)
        {
            grid.Columns.Clear();

            var nameColumn = new DataGridTextColumn
            {
                Header = nameHeader,
                Binding = new System.Windows.Data.Binding("PropertyName"),
                ElementStyle = (Style)FindResource("TimingNameTextStyle")
            };
            grid.Columns.Add(nameColumn);

            for (int i = 0; i < uniqueTimings.Count; i++)
            {
                var valueColumn = new DataGridTextColumn
                {
                    Header = $"DCT {uniqueTimings[i].Key >> 20}",
                    Binding = new System.Windows.Data.Binding($"Values[{i}]"),
                    ElementStyle = (Style)FindResource("TimingValueTextStyle")
                };

                grid.Columns.Add(valueColumn);
            }
        }

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
            if (BaseTimingsGrid == null || ExtendedTimingsGrid == null || StatusText == null)
                return;

            var query = TimingSearchTextBox?.Text;
            var showDifferencesOnly = DifferencesOnlyCheckBox?.IsChecked == true;
            var showAllTimings = ExtendedTimingsCheckBox?.IsChecked != false;

            IEnumerable<TimingGridItem> leftRows = _baseRows;
            IEnumerable<TimingGridItem> rightRows = _extendedRows;

            if (!string.IsNullOrWhiteSpace(query))
            {
                var q = query.Trim();
                leftRows = leftRows.Where(r => r.PropertyName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
                rightRows = rightRows.Where(r => r.PropertyName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            if (showDifferencesOnly)
            {
                leftRows = leftRows.Where(r => r.IsMismatch);
                rightRows = rightRows.Where(r => r.IsMismatch);
            }

            var left = leftRows.ToList();
            var right = rightRows.ToList();

            BaseTimingsGrid.ItemsSource = right;
            ExtendedTimingsGrid.ItemsSource = left;

            var showLeft = _extendedRows.Count > 0;
            var showRight = showAllTimings && _baseRows.Count > 0;
            if (!showLeft)
                showRight = true;

            BaseTimingsColumn.Width = showLeft ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
            BaseTimingsGrid.Visibility = showLeft ? Visibility.Visible : Visibility.Collapsed;

            ExtendedTimingsColumn.Width = showRight ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
            ExtendedTimingsGrid.Visibility = showRight ? Visibility.Visible : Visibility.Collapsed;

            TimingsSplitterColumn.Width = showLeft && showRight ? new GridLength(8) : new GridLength(0);

            StatusText.Text = showLeft && showRight
                ? $"{left.Count} base + {right.Count} extended timings shown across {_channelCount} channel(s)."
                : showRight
                    ? $"{right.Count} timings shown across {_channelCount} channel(s)."
                    : $"{left.Count} timings shown across {_channelCount} channel(s).";
        }

        private void TimingSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void DifferencesOnlyCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            ApplyFilter();
        }

        private void ExtendedTimingsCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            ApplyFilter();
        }

        private static int GetExtendedStartIndex(IReadOnlyList<TimingGridItem> rows)
        {
            if (rows == null || rows.Count == 0)
                return -1;

            for (int i = 0; i < rows.Count; i++)
                if (string.Equals(rows[i].PropertyName, "RFCsb", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(rows[i].PropertyName, "RFCb", StringComparison.OrdinalIgnoreCase))
                    return i;

            return -1;
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
