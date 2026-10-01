using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ZenStates.Core.Hardware.DRAM;
using ZenStates.Core.Hardware.DRAM.DDR4.Spd;
using ZenStates.Core.Hardware.DRAM.DDR5.Spd;
using ZenTimings.Common;
using ZenTimings.Utils;
using MessageBox = ZenTimings.Theming.MessageBox;
using MessageBoxButton = ZenTimings.Theming.MessageBoxButton;
using MessageBoxImage = ZenTimings.Theming.MessageBoxImage;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace ZenTimings.Windows
{
    public partial class SpdInfoWindow : ThemedWindow
    {
        private class SlotItem
        {
            public int Index { get; set; }
            public byte I2cAddress { get; set; }
            public string Display { get; set; }
            /// <summary>Ddr5SpdInfo or Ddr4SpdInfo.</summary>
            public object SpdInfo { get; set; }
        }

        private MemoryConfig _memoryConfig;
        private readonly List<SlotItem> _slots = new List<SlotItem>();
        private SpdModuleView _view;

        public SpdInfoWindow()
        {
            InitializeComponent();
            Loaded += SpdInfoWindow_Loaded;
        }

        private bool IsDdr4 => _memoryConfig?.Type == MemType.DDR4;

        // The SPD entries of the installed memory, keyed by SMBus address: Ddr5SpdInfo or Ddr4SpdInfo values
        private List<KeyValuePair<byte, object>> GetSpdEntries()
        {
            if (_memoryConfig == null)
                return null;

            if (IsDdr4)
                return _memoryConfig.Ddr4Spd?.Select(e => new KeyValuePair<byte, object>(e.Key, e.Value)).ToList();

            return _memoryConfig.SpdInfo?.Select(e => new KeyValuePair<byte, object>(e.Key, e.Value)).ToList();
        }

        private static bool IsPartial(object spd)
        {
            return (spd as Ddr5SpdInfo)?.IsPartial ?? (spd as Ddr4SpdInfo)?.IsPartial ?? false;
        }

        private async void SpdInfoWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _memoryConfig = CpuSingleton.Instance.memoryConfig;
            await LoadSlotsAsync();
            await Dispatcher.InvokeAsync(() => SizeToContent = SizeToContent.Manual, System.Windows.Threading.DispatcherPriority.ContextIdle);
        }

        private void CopyTab_Click(object sender, RoutedEventArgs e)
        {
            if (_view == null)
                return;

            // The serial number identifies the exact module, the text leaves it out as it is meant to be pasted elsewhere
            bool profiles = ProfilesTabControl.SelectedItem == ProfilesTab;
            string body = profiles ? _view.ProfilesText() : _view.ModuleText();
            if (string.IsNullOrWhiteSpace(body))
                return;

            string tab = profiles ? "Profiles" : "Module";
            string title = $"SPD {(ComboSlots.SelectedItem as SlotItem)?.Display} - {tab}";
            ClipboardUtils.Copy($"{title}{Environment.NewLine}{Environment.NewLine}{body.TrimEnd()}", sender as Button);
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
                FileName = GetDumpFileName(selected),
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
                bool ddr4 = IsDdr4;
                success = await Task.Run(() => ddr4
                    ? Ddr4SpdReader.DumpToFile(address, filePath)
                    : Ddr5SpdReader.DumpToFile(address, filePath));
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

        private static string GetDumpFileName(SlotItem slot)
        {
            var parts = new List<string> { "SPD" };

            if (slot.SpdInfo is Ddr5SpdInfo ddr5)
            {
                AddFileNamePart(parts, ddr5.ModuleManufacturer);
                AddFileNamePart(parts, ddr5.ModulePartNumber);
            }
            else if (slot.SpdInfo is Ddr4SpdInfo ddr4)
            {
                AddFileNamePart(parts, ddr4.ModuleManufacturer);
                AddFileNamePart(parts, ddr4.ModulePartNumber);
            }

            parts.Add($"0x{slot.I2cAddress:X2}");
            return string.Join("_", parts) + ".spd";
        }

        private static void AddFileNamePart(List<string> parts, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            var invalid = System.IO.Path.GetInvalidFileNameChars();
            var cleaned = new string(value.Trim().Select(c => invalid.Contains(c) || char.IsWhiteSpace(c) ? '-' : c).ToArray()).Trim('-');

            if (cleaned.Length > 0)
                parts.Add(cleaned);
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

                    var spdByAddress = GetSpdEntries();
                    if (spdByAddress == null || spdByAddress.Count == 0)
                        return null;

                    if (spdByAddress.Any(entry => IsPartial(entry.Value)))
                    {
                        Dispatcher.Invoke(() => StatusText.Text = "Partial SPD detected, refreshing…");
                        _memoryConfig.RefreshSpdInfo();
                    }

                    var entries = GetSpdEntries();
                    if (entries == null)
                        return null;

                    for (int idx = 0; idx < entries.Count; idx++)
                    {
                        byte address = entries[idx].Key;
                        var module = (_memoryConfig.Modules != null && idx < _memoryConfig.Modules.Count)
                            ? _memoryConfig.Modules[idx] : null;
                        var slotName = (module != null && !string.IsNullOrEmpty(module.Slot))
                            ? module.Slot : $"DIMM {idx}";

                        result.Add(new SlotItem
                        {
                            Index = idx,
                            I2cAddress = address,
                            Display = $"{slotName} (0x{address:X2})",
                            SpdInfo = entries[idx].Value
                        });
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
                ShowView(SpdModuleView.Message("No SPD data available."));
                return;
            }

            ShowView(SpdPresenter.Build(slot.SpdInfo));
            StatusText.Text = $"Showing SPD for {slot.Display}";
        }

        private void ShowView(SpdModuleView view)
        {
            _view = view;
            ModuleGrid.ItemsSource = view.Module;
            ComponentsGrid.ItemsSource = view.Components;
            ComponentsSection.Visibility = view.Components.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            ProfilesList.ItemsSource = view.Profiles;
            NoProfilesText.Visibility = view.Profiles.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        private void SetNoDataState(string message)
        {
            // Clearing the slots raises SelectionChanged, which renders an empty slot: show the message after
            ComboSlots.ItemsSource = null;
            ShowView(SpdModuleView.Message(message));
            StatusText.Text = message;
        }
    }
}
