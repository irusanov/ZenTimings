using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ZenStates.Core.Hardware.DRAM;
using ZenStates.Core.Hardware.DRAM.DDR5.Spd;
using MessageBox = AdonisUI.Controls.MessageBox;
using MessageBoxButton = AdonisUI.Controls.MessageBoxButton;
using MessageBoxImage = AdonisUI.Controls.MessageBoxImage;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace ZenTimings.Windows
{
    public partial class SpdInfoWindow : ThemedAdonisWindow
    {
        private class SlotItem
        {
            public int Index { get; set; }
            public byte I2cAddress { get; set; }
            public string Display { get; set; }
            public object SpdInfo { get; set; }
        }

        private class GridItem
        {
            public string Name { get; set; }
            public string Value { get; set; }
        }

        private MemoryConfig _memoryConfig;
        private readonly List<SlotItem> _slots = new List<SlotItem>();

        public SpdInfoWindow()
        {
            InitializeComponent();
            Loaded += SpdInfoWindow_Loaded;
        }

        private async void SpdInfoWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _memoryConfig = CpuSingleton.Instance.memoryConfig;
            await LoadSlotsAsync();
        }

        private void ComboSlots_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = ComboSlots.SelectedItem as SlotItem;
            RenderSelected(selected);
        }

        private async void ButtonDumpSpd_Click(object sender, RoutedEventArgs e)
        {
            var selected = ComboSlots.SelectedItem as SlotItem;
            if (selected == null)
            {
                MessageBox.Show("No DIMM slot selected.", "Dump SPD", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dlg = new SaveFileDialog
            {
                Filter = "SPD files (*.spd)|*.spd|Binary files (*.bin)|*.bin|All files (*.*)|*.*",
                FilterIndex = 1,
                DefaultExt = "spd",
                FileName = $"SPD_0x{selected.I2cAddress:X2}.spd",
                RestoreDirectory = true
            };

            if (dlg.ShowDialog() != true)
                return;

            ButtonDumpSpd.IsEnabled = false;
            StatusText.Text = $"Dumping SPD for {selected.Display}…";

            bool success = false;
            string error = null;

            try
            {
                var filePath = dlg.FileName;
                var address = selected.I2cAddress;
                success = await Task.Run(() => Ddr5SpdReader.DumpDdr5SpdToFile(address, filePath));
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }

            ButtonDumpSpd.IsEnabled = true;

            if (error != null)
            {
                StatusText.Text = "Dump failed.";
                MessageBox.Show($"Failed to dump SPD: {error}", "Dump SPD", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else if (!success)
            {
                StatusText.Text = "Dump failed.";
                MessageBox.Show("Failed to dump SPD. The operation returned false.", "Dump SPD", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                StatusText.Text = $"SPD dumped to {dlg.FileName}";
            }
        }

        private async Task LoadSlotsAsync()
        {
            StatusText.Text = "Reading SPD data…";
            ComboSlots.IsEnabled = false;

            List<SlotItem> loaded = null;
            string error = null;

            try
            {
                loaded = await Task.Run(() =>
                {
                    var result = new List<SlotItem>();

                    var spdByAddress = _memoryConfig?.SpdInfo;
                    if (spdByAddress == null || spdByAddress.Count == 0)
                        return null;

                    bool hasPartial = false;
                    foreach (var entry in (IEnumerable)spdByAddress)
                    {
                        var v = entry.GetType().GetProperty("Value")?.GetValue(entry, null);
                        if (v == null) continue;
                        var partialField = v.GetType().GetField("IsPartial");
                        if (partialField != null && true.Equals(partialField.GetValue(v)))
                        {
                            hasPartial = true;
                            break;
                        }
                    }

                    if (hasPartial)
                    {
                        Dispatcher.Invoke(() => StatusText.Text = "Partial SPD detected, refreshing…");
                        _memoryConfig.RefreshSpdInfo();
                    }

                    int idx = 0;
                    foreach (var kvp in (IEnumerable)_memoryConfig.SpdInfo)
                    {
                        var kvpType = kvp.GetType();
                        var keyObj = kvpType.GetProperty("Key")?.GetValue(kvp, null);
                        var valueObj = kvpType.GetProperty("Value")?.GetValue(kvp, null);

                        byte address;
                        try { address = Convert.ToByte(keyObj); }
                        catch { idx++; continue; }

                        var module = (_memoryConfig.Modules != null && idx < _memoryConfig.Modules.Count)
                            ? _memoryConfig.Modules[idx] : null;
                        var slotName = (module != null && !string.IsNullOrEmpty(module.Slot))
                            ? module.Slot : $"DIMM {idx}";

                        result.Add(new SlotItem
                        {
                            Index = idx,
                            I2cAddress = address,
                            Display = $"{slotName} (0x{address:X2})",
                            SpdInfo = valueObj
                        });
                        idx++;
                    }

                    return result;
                });
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }

            // Back on UI thread
            ComboSlots.IsEnabled = true;
            _slots.Clear();

            if (error != null)
            {
                SetNoDataState($"Error reading SPD: {error}");
                return;
            }

            if (loaded == null || loaded.Count == 0)
            {
                SetNoDataState("No SPD data detected");
                return;
            }

            foreach (var s in loaded)
                _slots.Add(s);

            ComboSlots.ItemsSource = null;
            ComboSlots.ItemsSource = _slots;
            ComboSlots.SelectedIndex = 0;
            StatusText.Text = $"Loaded {_slots.Count} SPD module(s)";
        }

        private void RenderSelected(SlotItem slot)
        {
            if (slot == null || slot.SpdInfo == null)
            {
                SetNoDataState("No SPD data available.");
                return;
            }

            var spdInfo = slot.SpdInfo;
            var spdType = spdInfo.GetType();

            // Fields to skip from the General tab (handled separately as profile tabs)
            var skipFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "XmpProfiles", "ExpoProfile1", "ExpoProfile2",
                "ThermalData", "PmicData", "RawSpd", "IsPartial"
            };

            var general = new List<GridItem>();
            var xmpProfiles = new Dictionary<int, List<GridItem>>();
            var expoProfiles = new Dictionary<int, List<GridItem>>();

            foreach (var field in spdType.GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (skipFields.Contains(field.Name))
                    continue;

                object value;
                try { value = field.GetValue(spdInfo); }
                catch { continue; }

                general.Add(new GridItem { Name = field.Name, Value = FormatValue(value) });
            }

            if (general.Count == 0)
                general.Add(new GridItem { Name = "Info", Value = "No general fields available." });

            // XMP profiles — XmpProfiles is a Ddr5XmpProfile[]; only include IsValid entries
            var xmpProfilesField = spdType.GetField("XmpProfiles");
            if (xmpProfilesField != null)
            {
                var arr = xmpProfilesField.GetValue(spdInfo) as Array;
                if (arr != null)
                {
                    for (int i = 0; i < arr.Length; i++)
                    {
                        var profile = arr.GetValue(i);
                        if (profile == null) continue;
                        var isValidField = profile.GetType().GetField("IsValid");
                        if (isValidField != null && !true.Equals(isValidField.GetValue(profile))) continue;
                        xmpProfiles[i + 1] = FieldsToGridItems(profile);
                    }
                }
            }

            // EXPO profiles — ExpoProfile1 and ExpoProfile2 are individual fields; only include IsValid entries
            foreach (var expoFieldName in new[] { "ExpoProfile1", "ExpoProfile2" })
            {
                var expoField = spdType.GetField(expoFieldName);
                if (expoField == null) continue;
                var profile = expoField.GetValue(spdInfo);
                if (profile == null) continue;
                var isValidField = profile.GetType().GetField("IsValid");
                if (isValidField != null && !true.Equals(isValidField.GetValue(profile))) continue;
                int profileNum = expoFieldName == "ExpoProfile1" ? 1 : 2;
                expoProfiles[profileNum] = FieldsToGridItems(profile);
            }

            GeneralGrid.ItemsSource = general;
            RebuildProfileTabs(xmpProfiles, expoProfiles);
            StatusText.Text = $"Showing SPD for {slot.Display}";
        }

        private static List<GridItem> FieldsToGridItems(object obj)
        {
            var result = new List<GridItem>();
            if (obj == null) return result;
            foreach (var field in obj.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                object value;
                try { value = field.GetValue(obj); }
                catch { continue; }
                result.Add(new GridItem { Name = field.Name, Value = FormatValue(value) });
            }
            return result;
        }

        private void SetNoDataState(string message)
        {
            GeneralGrid.ItemsSource = new List<GridItem> { new GridItem { Name = "Info", Value = message } };
            RebuildProfileTabs(new Dictionary<int, List<GridItem>>(), new Dictionary<int, List<GridItem>>());
            ComboSlots.ItemsSource = null;
            StatusText.Text = message;
        }

        private void RebuildProfileTabs(
            Dictionary<int, List<GridItem>> xmpProfiles,
            Dictionary<int, List<GridItem>> expoProfiles)
        {
            while (ProfilesTabControl.Items.Count > 1)
                ProfilesTabControl.Items.RemoveAt(1);

            if (xmpProfiles.Count == 0)
            {
                ProfilesTabControl.Items.Add(CreateProfileTab("XMP", new List<GridItem>
                {
                    new GridItem { Name = "Info", Value = "No XMP profiles available." }
                }));
            }
            else
            {
                foreach (var profile in xmpProfiles.OrderBy(k => k.Key))
                    ProfilesTabControl.Items.Add(CreateProfileTab($"XMP {profile.Key}", profile.Value));
            }

            if (expoProfiles.Count == 0)
            {
                ProfilesTabControl.Items.Add(CreateProfileTab("EXPO", new List<GridItem>
                {
                    new GridItem { Name = "Info", Value = "No EXPO profiles available." }
                }));
            }
            else
            {
                foreach (var profile in expoProfiles.OrderBy(k => k.Key))
                    ProfilesTabControl.Items.Add(CreateProfileTab($"EXPO {profile.Key}", profile.Value));
            }
        }

        private static TabItem CreateProfileTab(string header, List<GridItem> rows)
        {
            var nameStyle = new Style(typeof(TextBlock));
            nameStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, new System.Windows.DynamicResourceExtension("TextColor")));
            nameStyle.Setters.Add(new Setter(TextBlock.OpacityProperty, 0.75));
            nameStyle.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(4, 0, 4, 0)));

            var valueStyle = new Style(typeof(TextBlock));
            valueStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, new System.Windows.DynamicResourceExtension("AccentTextColor")));
            valueStyle.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(4, 0, 4, 0)));

            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                HeadersVisibility = DataGridHeadersVisibility.None,
                GridLinesVisibility = DataGridGridLinesVisibility.None,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                CanUserResizeRows = false,
                CanUserResizeColumns = false,
                CanUserReorderColumns = false,
                CanUserSortColumns = false,
                IsReadOnly = true,
                Focusable = false,
                FontSize = 11,
                ItemsSource = rows
            };

            grid.Columns.Add(new DataGridTextColumn
            {
                Width = new DataGridLength(240),
                Binding = new System.Windows.Data.Binding("Name"),
                ElementStyle = nameStyle
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                Binding = new System.Windows.Data.Binding("Value"),
                ElementStyle = valueStyle
            });

            return new TabItem
            {
                Header = header,
                Content = grid
            };
        }

        private static string FormatValue(object value)
        {
            if (value == null)
                return "N/A";

            var type = value.GetType();
            if (type.IsPrimitive || value is string || value is decimal)
                return value.ToString();

            if (value is IEnumerable enumerable && !(value is string))
            {
                var parts = new List<string>();
                foreach (var item in enumerable)
                {
                    if (item == null) continue;
                    parts.Add(item.ToString());
                    if (parts.Count >= 8) break;
                }
                if (parts.Count == 0)
                    return "(empty)";
                return string.Join(", ", parts);
            }

            return value.ToString();
        }
    }
}
