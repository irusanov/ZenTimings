using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ZenStates.Core.Hardware;
using ZenStates.Core.Hardware.DRAM;
using ZenStates.Core.Hardware.DRAM.DDR5.Pmic;
using ZenStates.Core.Hardware.DRAM.DDR5.Spd;
using ZenStates.Core.Hardware.DRAM.DDR5.Thermal;

namespace ZenTimings.Windows
{
    public class CountToVisibilityConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value is int count && count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class WidthOffsetConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is double width)
            {
                double offset = 16;
                if (parameter != null && double.TryParse(parameter.ToString(), out double parsedOffset))
                    offset = parsedOffset;

                return Math.Max(0, width - offset);
            }

            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public partial class SensorsWindow : ThemedAdonisWindow
    {
        private readonly DispatcherTimer updateTimer;
        private readonly DispatcherTimer _uptimeStatusTimer;
        private DateTime _windowOpenedAt;
        private readonly MemoryConfig memoryConfig;
        private readonly ObservableCollection<ModuleViewModel> moduleViewModels = new ObservableCollection<ModuleViewModel>();
        private readonly ObservableCollection<SensorGroupViewModel> sensorGroupViewModels = new ObservableCollection<SensorGroupViewModel>();
        private readonly List<SensorTelemetryLink> sensorTelemetryLinks = new List<SensorTelemetryLink>();
        private bool _isRefreshing;

        public SensorsWindow()
        {
            InitializeComponent();
            memoryConfig = CpuSingleton.Instance.memoryConfig;

            updateTimer = new DispatcherTimer();
            updateTimer.Tick += RefreshTimer_Tick;

            _uptimeStatusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _uptimeStatusTimer.Tick += UptimeTimer_Tick;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ModulesContainer.ItemsSource = moduleViewModels;
            SensorGroupsContainer.ItemsSource = sensorGroupViewModels;
            AppSettings.Instance.PropertyChanged += AppSettings_PropertyChanged;
            ToggleAutoOpen.IsChecked = AppSettings.Instance.AutoOpenTelemetry;
            await LoadModulesDataAsync();
            LoadSensorGroups();
            UpdateNoSensorsMessage();
            ConfigureAutoRefresh();
        }

        // Shows a message when neither memory module telemetry nor SuperIO sensors are available.
        private void UpdateNoSensorsMessage()
        {
            bool hasAnySensors = moduleViewModels.Count > 0 || sensorGroupViewModels.Count > 0;
            NoSensorsMessage.Visibility = hasAnySensors ? Visibility.Collapsed : Visibility.Visible;
            if (!hasAnySensors)
                StatusText.Text = "No sensors available on this system";
        }

        private void UptimeTimer_Tick(object sender, EventArgs e)
        {
            StatusText.Text = "Running: " + (DateTime.Now - _windowOpenedAt).ToString(@"hh\:mm\:ss");
        }

        private void AppSettings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AppSettings.AutoRefreshInterval) && updateTimer.IsEnabled)
            {
                updateTimer.Interval = TimeSpan.FromMilliseconds(AppSettings.Instance.AutoRefreshInterval);
            }
            else if (e.PropertyName == nameof(AppSettings.AutoRefresh))
            {
                ConfigureAutoRefresh();
            }
        }

        private void ConfigureAutoRefresh()
        {
            if (AppSettings.Instance.AutoRefresh)
                StartAutoRefresh();
            else
                StopAutoRefresh();
        }

        private void StartAutoRefresh()
        {
            _windowOpenedAt = DateTime.Now;
            _uptimeStatusTimer.Start();
            UptimeTimer_Tick(null, null);
            int interval = AppSettings.Instance.AutoRefreshInterval;
            updateTimer.Interval = TimeSpan.FromMilliseconds(interval);
            updateTimer.Start();
        }

        private void StopAutoRefresh()
        {
            updateTimer.Stop();
            _uptimeStatusTimer.Stop();
            _windowOpenedAt = DateTime.Now;
            StatusText.Text = "Auto-refresh off";
        }

        private async Task LoadModulesDataAsync()
        {
            if (memoryConfig == null)
            {
                // Memory module telemetry (SPD/PMIC) is only supported on some platforms (e.g. DDR5).
                // Skip this section entirely instead of showing an error.
                return;
            }

            // Block periodic refresh while the initial SMBUS load is in progress.
            _isRefreshing = true;
            StatusText.Text = "Loading...";

            Tuple<List<ModuleViewModel>, string> result = null;
            Exception loadError = null;

            try
            {
                result = await Task.Run(() => BuildModuleViewModels());
            }
            catch (Exception ex)
            {
                loadError = ex;
            }

            moduleViewModels.Clear();
            if (loadError != null)
            {
                StatusText.Text = $"Error loading modules: {loadError.Message}";
                MessageBox.Show($"Error loading module data:\n{loadError.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                foreach (var vm in result.Item1)
                    moduleViewModels.Add(vm);
                StatusText.Text = result.Item2;
            }

            _isRefreshing = false;
        }

        // Runs entirely on a thread-pool thread
        private Tuple<List<ModuleViewModel>, string> BuildModuleViewModels()
        {
            var vms = new List<ModuleViewModel>();
            Dictionary<byte, Ddr5SpdInfo> spdInfo = null;
            string warning = null;

            try
            {
                var info = memoryConfig.SpdInfo;
                if (info != null && info.Count > 0)
                    spdInfo = info;
            }
            catch (Exception ex)
            {
                warning = $"Warning: Could not read SPD data - {ex.Message}";
            }

            if (spdInfo != null && spdInfo.Count > 0)
            {
                int slotIndex = 0;
                foreach (var spdEntry in spdInfo)
                {
                    var vm = new ModuleViewModel
                    {
                        PartNumber = spdEntry.Value.ModulePartNumber ?? "N/A",
                        Manufacturer = spdEntry.Value.ModuleManufacturer ?? "N/A",
                        Capacity = spdEntry.Value.TotalCapacityMB > 0 ? $"{spdEntry.Value.TotalCapacityMB} MB" : "N/A",
                        Rank = spdEntry.Value.RanksPerChannel > 0 ? $"{spdEntry.Value.RanksPerChannel}R" : "N/A",
                        MemoryChip = !string.IsNullOrEmpty(spdEntry.Value.DramManufacturer)
                            ? $"{spdEntry.Value.DramManufacturer} {VendorUtils.GetDramDieName(spdEntry.Value.DramManufacturer, spdEntry.Value.DramStepping)}"
                            : "N/A"
                    };

                    MemoryModule module = null;
                    if (memoryConfig.Modules != null && slotIndex < memoryConfig.Modules.Count)
                    {
                        module = memoryConfig.Modules[slotIndex];
                        var logoName = VendorUtils.GetMemoryModuleLogo(module);
                        if (!string.IsNullOrEmpty(logoName))
                        {
                            vm.LogoResourceName = logoName;
                            vm.HasLogo = true;
                        }
                    }

                    if (spdEntry.Value.ThermalData != null && spdEntry.Value.ThermalData.IsValid)
                        InitializeThermalTelemetry(vm, spdEntry.Value.ThermalData, slotIndex);

                    if (spdEntry.Value.PmicData != null && spdEntry.Value.PmicData.IsValid)
                    {
                        vm.HasPmic = true;
                        vm.PmicVendor = spdEntry.Value.PmicData.VendorName ?? "N/A";
                        vm.PmicRevision = $"{spdEntry.Value.PmicData.RevisionMajor}.{spdEntry.Value.PmicData.RevisionMinor}";
                        InitializePmicTelemetry(vm, spdEntry.Value.PmicData, slotIndex);
                    }

                    var header = new System.Text.StringBuilder($"DIMM {slotIndex}");
                    if (module != null && !string.IsNullOrEmpty(module.Slot))
                        header.Append($" | {module.Slot}");
                    if (spdEntry.Value.PmicData != null && spdEntry.Value.PmicData.IsValid)
                        header.Append($" | PMIC 0x{spdEntry.Value.PmicData.I2cAddress:X2}");
                    vm.Header = header.ToString();

                    vms.Add(vm);
                    slotIndex++;
                }
                return Tuple.Create(vms, $"Loaded {vms.Count} module(s)");
            }

            if (memoryConfig.Modules != null && memoryConfig.Modules.Count > 0)
            {
                for (int i = 0; i < memoryConfig.Modules.Count; i++)
                {
                    var module = memoryConfig.Modules[i];
                    var header = string.IsNullOrEmpty(module.Slot)
                        ? $"DIMM {i}"
                        : $"DIMM {i} | {module.Slot}";
                    var vm = new ModuleViewModel
                    {
                        Header = header,
                        PartNumber = module.PartNumber ?? "N/A",
                        Manufacturer = module.Manufacturer ?? "N/A",
                        Capacity = module.Capacity != null && module.Capacity.SizeInBytes > 0
                            ? module.Capacity.ToString()
                            : "N/A",
                        Rank = module.Rank.ToString(),
                        MemoryChip = "N/A (DDR4 or no SPD data)",
                        HasPmic = false
                    };

                    var logoName = VendorUtils.GetMemoryModuleLogo(module);
                    if (!string.IsNullOrEmpty(logoName))
                    {
                        vm.LogoResourceName = logoName;
                        vm.HasLogo = true;
                    }

                    vms.Add(vm);
                }
                return Tuple.Create(vms, $"Loaded {vms.Count} module(s) - Limited info (no SPD/PMIC data available)");
            }

            return Tuple.Create(vms, warning ?? "No memory modules detected");
        }

        private void InitializePmicTelemetry(ModuleViewModel vm, Ddr5PmicData pmicData, int slotIndex)
        {
            vm.HasTelemetry = true;

            var hiddenSensors = SensorSettings.Instance.HiddenSensors;
            int hiddenCount = 0;

            void AddPmicItem(string name, double value, string unit)
            {
                var key = GetPmicSensorKey(slotIndex, name);
                if (hiddenSensors.Contains(key))
                {
                    hiddenCount++;
                    vm.HiddenKeys.Add(key);
                    return;
                }

                vm.TelemetryItems.Add(new TelemetryItemViewModel(name, value, unit) { GroupKey = key });
            }

            // VDD (SWA)
            AddPmicItem("VDD (SWA)", pmicData.SwaAdcMv / 1000.0, "V");

            // VDDQ (SWB)
            AddPmicItem("VDDQ (SWB)", pmicData.SwbAdcMv / 1000.0, "V");

            // VPP (SWC)
            AddPmicItem("VPP (SWC)", pmicData.SwcAdcMv / 1000.0, "V");

            // VIN Bulk
            AddPmicItem("VIN Bulk", pmicData.VinBulkMv / 1000.0, "V");

            // 1.8V LDO
            AddPmicItem("VOUT 1.8V", pmicData.Vout18AdcMv / 1000.0, "V");

            // 1.0V LDO
            AddPmicItem("VOUT 1.0V", pmicData.Vout10AdcMv / 1000.0, "V");

            // PMIC Temperature
            if (!string.IsNullOrEmpty(pmicData.PmicTemperature))
            {
                if (double.TryParse(pmicData.PmicTemperature.Replace("°C", "").Trim(), out double tempValue))
                {
                    AddPmicItem("PMIC Temp", tempValue, "°C");
                }
            }

            // Total Power
            //if (pmicData.TelemetryReportsTotalPower)
            {
                AddPmicItem("Total Power", pmicData.TotalW, "W");
            }

            // High Temperature Warning
            var pmicHighTempKey = GetPmicSensorKey(slotIndex, "PMIC High Temp");
            if (hiddenSensors.Contains(pmicHighTempKey))
            {
                hiddenCount++;
                vm.HiddenKeys.Add(pmicHighTempKey);
            }
            else
            {
                var pmicHighTempItem = new TelemetryItemViewModel("PMIC High Temp", pmicData.HighTemperatureWarning) { GroupKey = pmicHighTempKey };
                pmicHighTempItem.UpdateThermalAlarm(pmicData.CriticalTemperatureShutdown, pmicData.HighTemperatureWarning);
                vm.TelemetryItems.Add(pmicHighTempItem);
            }

            vm.HiddenCount = hiddenCount;
        }

        private static string GetPmicSensorKey(int slotIndex, string sensorName) => $"PMIC|{slotIndex}|{sensorName}";

        private void InitializeThermalTelemetry(ModuleViewModel vm, Ddr5ThermalData thermalData, int slotIndex)
        {
            vm.HasTelemetry = true;

            var key = GetPmicSensorKey(slotIndex, "SPD Hub Temp");
            if (SensorSettings.Instance.HiddenSensors.Contains(key))
            {
                vm.HiddenCount++;
                vm.HiddenKeys.Add(key);
                return;
            }

            var item = new TelemetryItemViewModel("SPD Hub Temp", thermalData.TemperatureC, "°C") { GroupKey = key };
            item.UpdateThermalAlarm(thermalData.AlarmCritHigh, thermalData.AlarmHigh);
            vm.TelemetryItems.Add(item);
        }

        internal void RefreshTelemetryGroups()
        {
            if (_isRefreshing || memoryConfig == null)
                return;

            _isRefreshing = true;
            try
            {
                var spdInfo = memoryConfig.SpdInfo;

                if (spdInfo != null && spdInfo.Count > 0)
                {
                    int index = 0;
                    foreach (var spdEntry in spdInfo)
                    {
                        if (index >= moduleViewModels.Count)
                            break;

                        var vm = moduleViewModels[index];
                        if (spdEntry.Value.PmicData != null && spdEntry.Value.PmicData.IsValid)
                            UpdatePmicTelemetry(vm, spdEntry.Value.PmicData);

                        if (spdEntry.Value.ThermalData != null && spdEntry.Value.ThermalData.IsValid)
                            UpdateThermalTelemetry(vm, spdEntry.Value.ThermalData);

                        index++;
                    }
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error refreshing telemetry: {ex.Message}";
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        private void UpdatePmicTelemetry(ModuleViewModel vm, Ddr5PmicData pmicData)
        {
            UpdateTelemetryItem(vm, "VDD (SWA)", pmicData.SwaAdcMv / 1000.0);
            UpdateTelemetryItem(vm, "VDDQ (SWB)", pmicData.SwbAdcMv / 1000.0);
            UpdateTelemetryItem(vm, "VPP (SWC)", pmicData.SwcAdcMv / 1000.0);
            UpdateTelemetryItem(vm, "VIN Bulk", pmicData.VinBulkMv / 1000.0);
            UpdateTelemetryItem(vm, "VOUT 1.8V", pmicData.Vout18AdcMv / 1000.0);
            UpdateTelemetryItem(vm, "VOUT 1.0V", pmicData.Vout10AdcMv / 1000.0);

            if (!string.IsNullOrEmpty(pmicData.PmicTemperature))
            {
                if (double.TryParse(pmicData.PmicTemperature.Replace("°C", "").Trim(), out double tempValue))
                {
                    UpdateTelemetryItem(vm, "PMIC Temp", tempValue);
                }
            }

            //if (pmicData.TelemetryReportsTotalPower)
            {
                UpdateTelemetryItem(vm, "Total Power", pmicData.TotalW);
            }

            UpdateTelemetryItem(vm, "PMIC High Temp", pmicData.HighTemperatureWarning);
            vm.TelemetryItems.FirstOrDefault(i => i.Name == "PMIC High Temp")
                ?.UpdateThermalAlarm(pmicData.CriticalTemperatureShutdown, pmicData.HighTemperatureWarning);
        }

        private void UpdateThermalTelemetry(ModuleViewModel vm, Ddr5ThermalData thermalData)
        {
            var item = vm.TelemetryItems.FirstOrDefault(i => i.Name == "SPD Hub Temp");
            if (item != null)
            {
                item.UpdateValue(thermalData.TemperatureC);
                item.UpdateThermalAlarm(thermalData.AlarmCritHigh, thermalData.AlarmHigh);
            }
        }

        private void UpdateTelemetryItem(ModuleViewModel vm, string name, double value)
        {
            var item = vm.TelemetryItems.FirstOrDefault(i => i.Name == name);
            if (item != null)
            {
                item.UpdateValue(value);
            }
        }

        private void UpdateTelemetryItem(ModuleViewModel vm, string name, bool value)
        {
            var item = vm.TelemetryItems.FirstOrDefault(i => i.Name == name);
            if (item != null)
            {
                item.UpdateValue(value ? 1.0 : 0.0);
            }
        }

        private void RefreshTimer_Tick(object sender, EventArgs e)
        {
            RefreshTelemetryGroups();
            RefreshSensorGroups();
        }

        private void BtnResetStats_Click(object sender, RoutedEventArgs e)
        {
            foreach (var module in moduleViewModels)
            {
                foreach (var item in module.TelemetryItems)
                {
                    item.ResetStats();
                }
            }

            foreach (var group in sensorGroupViewModels)
            {
                foreach (var item in group.TelemetryItems)
                {
                    item.ResetStats();
                }
            }

            _windowOpenedAt = DateTime.Now;
            UptimeTimer_Tick(null, null);
        }

        private void LoadSensorGroups()
        {
            sensorGroupViewModels.Clear();
            sensorTelemetryLinks.Clear();

            var systemInfo = CpuSingleton.Instance?.systemInfo;
            if (systemInfo == null)
                return;

            var hiddenSensors = SensorSettings.Instance.HiddenSensors;

            foreach (var group in systemInfo.SensorGroups)
            {
                var groupVm = new SensorGroupViewModel { Header = group.ChipName };
                int hiddenCount = 0;

                foreach (var sensor in group.Sensors)
                {
                    var key = GetSensorKey(group.ChipName, sensor.Name);
                    if (hiddenSensors.Contains(key))
                    {
                        hiddenCount++;
                        groupVm.HiddenKeys.Add(key);
                        continue;
                    }

                    var unit = GetSensorUnit(sensor.SensorType);
                    var initialValue = GetSensorDisplayValue(sensor);
                    var item = new TelemetryItemViewModel(sensor.Name, initialValue, unit) { GroupKey = key };
                    groupVm.TelemetryItems.Add(item);
                    sensorTelemetryLinks.Add(new SensorTelemetryLink(sensor, item));
                }

                groupVm.HiddenCount = hiddenCount;

                if (groupVm.TelemetryItems.Count > 0 || hiddenCount > 0)
                    sensorGroupViewModels.Add(groupVm);
            }
        }

        private static string GetSensorKey(string chipName, string sensorName) => $"{chipName}|{sensorName}";

        // Hides the sensors currently selected in the DataGrid the context menu was opened on.
        private void HideSensors_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is System.Windows.Controls.MenuItem menuItem) ||
                !(menuItem.Parent is System.Windows.Controls.ContextMenu contextMenu) ||
                !(contextMenu.PlacementTarget is System.Windows.Controls.DataGrid dataGrid))
                return;

            var selectedItems = dataGrid.SelectedItems.OfType<TelemetryItemViewModel>().ToList();
            if (selectedItems.Count == 0)
                return;

            var hiddenSensors = SensorSettings.Instance.HiddenSensors;
            bool changed = false;

            foreach (var item in selectedItems)
            {
                if (string.IsNullOrEmpty(item.GroupKey))
                    continue;

                if (!hiddenSensors.Contains(item.GroupKey))
                {
                    hiddenSensors.Add(item.GroupKey);
                    changed = true;
                }

                foreach (var group in sensorGroupViewModels)
                {
                    if (group.TelemetryItems.Remove(item))
                    {
                        group.HiddenCount++;
                        group.HiddenKeys.Add(item.GroupKey);
                        break;
                    }
                }

                foreach (var module in moduleViewModels)
                {
                    if (module.TelemetryItems.Remove(item))
                    {
                        module.HiddenCount++;
                        module.HiddenKeys.Add(item.GroupKey);
                        break;
                    }
                }

                var link = sensorTelemetryLinks.FirstOrDefault(l => l.Item == item);
                if (link != null)
                    sensorTelemetryLinks.Remove(link);
            }

            // Remove groups that no longer have any visible sensors.
            //for (int i = sensorGroupViewModels.Count - 1; i >= 0; i--)
            //{
            //    if (sensorGroupViewModels[i].TelemetryItems.Count == 0 && sensorGroupViewModels[i].HiddenCount == 0)
            //        sensorGroupViewModels.RemoveAt(i);
            //}

            if (changed)
                SensorSettings.Instance.Save();

            UpdateNoSensorsMessage();
        }

        // Hides every sensor in the DataGrid the context menu was opened on whose current, min and max values are all zero.
        private void HideZeroValueSensors_Click(object sender, RoutedEventArgs e)
        {
            HideSensorsMatching(sender, i => i.IsAllZero);
        }

        // Hides every fan sensor in the DataGrid the context menu was opened on.
        private void HideAllFans_Click(object sender, RoutedEventArgs e)
        {
            HideSensorsMatching(sender, i => i.IconKind == SensorIconKind.Fan);
        }

        // Hides every temperature sensor in the DataGrid the context menu was opened on.
        private void HideAllTemperatures_Click(object sender, RoutedEventArgs e)
        {
            HideSensorsMatching(sender, i => i.IconKind == SensorIconKind.Temperature);
        }

        // Hides every voltage sensor in the DataGrid the context menu was opened on.
        private void HideAllVoltages_Click(object sender, RoutedEventArgs e)
        {
            HideSensorsMatching(sender, i => i.IconKind == SensorIconKind.Voltage);
        }

        // Hides every sensor in the DataGrid the context menu was opened on that matches the given predicate.
        private void HideSensorsMatching(object sender, Func<TelemetryItemViewModel, bool> predicate)
        {
            if (!(sender is System.Windows.Controls.MenuItem menuItem) ||
                !(menuItem.Parent is System.Windows.Controls.ContextMenu contextMenu) ||
                !(contextMenu.PlacementTarget is System.Windows.Controls.DataGrid dataGrid))
                return;

            var matchingItems = dataGrid.ItemsSource?.OfType<TelemetryItemViewModel>()
                .Where(predicate)
                .ToList();

            if (matchingItems == null || matchingItems.Count == 0)
                return;

            var hiddenSensors = SensorSettings.Instance.HiddenSensors;
            bool changed = false;

            foreach (var item in matchingItems)
            {
                if (string.IsNullOrEmpty(item.GroupKey))
                    continue;

                if (!hiddenSensors.Contains(item.GroupKey))
                {
                    hiddenSensors.Add(item.GroupKey);
                    changed = true;
                }

                foreach (var group in sensorGroupViewModels)
                {
                    if (group.TelemetryItems.Remove(item))
                    {
                        group.HiddenCount++;
                        group.HiddenKeys.Add(item.GroupKey);
                        break;
                    }
                }

                foreach (var module in moduleViewModels)
                {
                    if (module.TelemetryItems.Remove(item))
                    {
                        module.HiddenCount++;
                        module.HiddenKeys.Add(item.GroupKey);
                        break;
                    }
                }

                var link = sensorTelemetryLinks.FirstOrDefault(l => l.Item == item);
                if (link != null)
                    sensorTelemetryLinks.Remove(link);
            }

            if (changed)
                SensorSettings.Instance.Save();

            UpdateNoSensorsMessage();
        }

        // Opens the global sensor visibility settings dialog listing every known sensor,
        // grouped by module/chip, with checkboxes to show/hide them.
        private async void BtnSensorSettings_Click(object sender, RoutedEventArgs e)
        {
            var groups = BuildSensorSettingsCatalog();
            var dialog = new SensorSettingsWindow(groups) { Owner = this };

            if (dialog.ShowDialog() == true)
            {
                var hiddenSensors = SensorSettings.Instance.HiddenSensors;
                bool changed = false;

                foreach (var group in groups)
                {
                    foreach (var entryItem in group.Entries)
                    {
                        bool isHidden = !entryItem.IsVisible;
                        bool alreadyHidden = hiddenSensors.Contains(entryItem.Key);

                        if (isHidden && !alreadyHidden)
                        {
                            hiddenSensors.Add(entryItem.Key);
                            changed = true;
                        }
                        else if (!isHidden && alreadyHidden)
                        {
                            hiddenSensors.Remove(entryItem.Key);
                            changed = true;
                        }
                    }
                }

                if (changed)
                    SensorSettings.Instance.Save();

                LoadSensorGroups();
                UpdateSensorGroupsAfterSettingsChange();
                RefreshSensorGroups();
                await UpdateModulesAfterSettingsChangeAsync();
                UpdateNoSensorsMessage();
            }
        }

        private void UpdateSensorGroupsAfterSettingsChange()
        {
            var systemInfo = CpuSingleton.Instance?.systemInfo;
            if (systemInfo == null)
                return;

            var hiddenSensors = SensorSettings.Instance.HiddenSensors;

            foreach (var group in systemInfo.SensorGroups)
            {
                var groupVm = sensorGroupViewModels.FirstOrDefault(g => g.Header == group.ChipName);
                if (groupVm == null)
                {
                    groupVm = new SensorGroupViewModel { Header = group.ChipName };
                    sensorGroupViewModels.Add(groupVm);
                }

                // Drop items that just became hidden.
                for (int i = groupVm.TelemetryItems.Count - 1; i >= 0; i--)
                {
                    var existingItem = groupVm.TelemetryItems[i];
                    if (hiddenSensors.Contains(existingItem.GroupKey))
                    {
                        groupVm.TelemetryItems.RemoveAt(i);
                        var link = sensorTelemetryLinks.FirstOrDefault(l => l.Item == existingItem);
                        if (link != null)
                            sensorTelemetryLinks.Remove(link);
                    }
                }

                groupVm.HiddenKeys.Clear();
                int hiddenCount = 0;

                foreach (var sensor in group.Sensors)
                {
                    var key = GetSensorKey(group.ChipName, sensor.Name);
                    if (hiddenSensors.Contains(key))
                    {
                        hiddenCount++;
                        groupVm.HiddenKeys.Add(key);
                        continue;
                    }

                    // Already visible - keep the existing item so its recorded stats are preserved.
                    if (groupVm.TelemetryItems.Any(i => i.GroupKey == key))
                        continue;

                    var unit = GetSensorUnit(sensor.SensorType);
                    var initialValue = GetSensorDisplayValue(sensor);
                    var newItem = new TelemetryItemViewModel(sensor.Name, initialValue, unit) { GroupKey = key };
                    groupVm.TelemetryItems.Add(newItem);
                    sensorTelemetryLinks.Add(new SensorTelemetryLink(sensor, newItem));
                }

                groupVm.HiddenCount = hiddenCount;
            }
        }

        // Adds/removes module telemetry items to reflect the current hidden set, without recreating
        // unaffected items (which would reset their recorded stats).
        private async Task UpdateModulesAfterSettingsChangeAsync()
        {
            if (memoryConfig == null)
                return;

            _isRefreshing = true;

            Tuple<List<ModuleViewModel>, string> result = null;
            Exception loadError = null;

            try
            {
                result = await Task.Run(() => BuildModuleViewModels());
            }
            catch (Exception ex)
            {
                loadError = ex;
            }

            if (loadError != null)
            {
                StatusText.Text = $"Error loading modules: {loadError.Message}";
                _isRefreshing = false;
                return;
            }

            var freshList = result.Item1;

            if (moduleViewModels.Count != freshList.Count)
            {
                // Module count changed (unexpected here) - fall back to a full replace.
                moduleViewModels.Clear();
                foreach (var vm in freshList)
                    moduleViewModels.Add(vm);
            }
            else
            {
                for (int i = 0; i < freshList.Count; i++)
                    MergeModuleViewModel(moduleViewModels[i], freshList[i]);
            }

            StatusText.Text = result.Item2;
            _isRefreshing = false;
        }

        // Merges freshly-built module telemetry into the existing view model in place, keeping the
        // existing TelemetryItemViewModel instances for sensors that remain visible so their
        // recorded min/max/average values are not reset.
        private static void MergeModuleViewModel(ModuleViewModel existing, ModuleViewModel fresh)
        {
            existing.HiddenCount = fresh.HiddenCount;
            existing.HiddenKeys.Clear();
            existing.HiddenKeys.AddRange(fresh.HiddenKeys);

            for (int i = existing.TelemetryItems.Count - 1; i >= 0; i--)
            {
                var key = existing.TelemetryItems[i].GroupKey;
                if (fresh.TelemetryItems.All(f => f.GroupKey != key))
                    existing.TelemetryItems.RemoveAt(i);
            }

            foreach (var freshItem in fresh.TelemetryItems)
            {
                if (existing.TelemetryItems.All(e => e.GroupKey != freshItem.GroupKey))
                    existing.TelemetryItems.Add(freshItem);
            }
        }

        // Builds the list of every known sensor (visible or hidden), grouped by module/chip,
        // for display in the sensor settings dialog.
        private List<SensorSettingsGroup> BuildSensorSettingsCatalog()
        {
            var groups = new List<SensorSettingsGroup>();

            foreach (var module in moduleViewModels)
            {
                var group = new SensorSettingsGroup { Header = module.Header };

                foreach (var item in module.TelemetryItems)
                {
                    if (!string.IsNullOrEmpty(item.GroupKey))
                        group.Entries.Add(new SensorSettingsEntry(item.GroupKey, item.Name, true));
                }

                foreach (var key in module.HiddenKeys)
                    group.Entries.Add(new SensorSettingsEntry(key, GetSensorNameFromKey(key), false));

                if (group.Entries.Count > 0)
                    groups.Add(group);
            }

            foreach (var sensorGroup in sensorGroupViewModels)
            {
                var group = new SensorSettingsGroup { Header = sensorGroup.Header };

                foreach (var item in sensorGroup.TelemetryItems)
                {
                    if (!string.IsNullOrEmpty(item.GroupKey))
                        group.Entries.Add(new SensorSettingsEntry(item.GroupKey, item.Name, true));
                }

                foreach (var key in sensorGroup.HiddenKeys)
                    group.Entries.Add(new SensorSettingsEntry(key, GetSensorNameFromKey(key), false));

                if (group.Entries.Count > 0)
                    groups.Add(group);
            }

            return groups;
        }

        private static string GetSensorNameFromKey(string key)
        {
            int idx = key.LastIndexOf('|');
            return idx >= 0 && idx < key.Length - 1 ? key.Substring(idx + 1) : key;
        }

        private void RefreshSensorGroups()
        {
            var systemInfo = CpuSingleton.Instance?.systemInfo;
            if (systemInfo == null || sensorTelemetryLinks.Count == 0)
                return;

            try
            {
                //systemInfo.UpdateSensors();

                foreach (var link in sensorTelemetryLinks)
                {
                    link.Item.UpdateValue(GetSensorDisplayValue(link.Sensor));
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error refreshing sensors: {ex.Message}";
            }
        }

        private static double GetSensorDisplayValue(Sensor sensor)
        {
            var value = sensor.Value ?? 0;
            return sensor.SensorType == SensorType.Fan ? Math.Round(value) : value;
        }

        private static string GetSensorUnit(SensorType sensorType)
        {
            switch (sensorType)
            {
                case SensorType.Voltage: return "V";
                case SensorType.Current: return "A";
                case SensorType.Power: return "W";
                case SensorType.Clock: return "MHz";
                case SensorType.Temperature: return "°C";
                case SensorType.Load: return "%";
                case SensorType.Frequency: return "Hz";
                case SensorType.Fan: return "RPM";
                case SensorType.Flow: return "L/h";
                case SensorType.Control: return "%";
                case SensorType.Level: return "%";
                case SensorType.Factor: return "";
                case SensorType.Data: return "GB";
                case SensorType.SmallData: return "MB";
                case SensorType.Throughput: return "B/s";
                case SensorType.TimeSpan: return "s";
                case SensorType.Timing: return "ns";
                case SensorType.Energy: return "mWh";
                case SensorType.Noise: return "dBA";
                case SensorType.Conductivity: return "µS/cm";
                case SensorType.Humidity: return "%";
                default: return "";
            }
        }

        private void ToggleAutoOpen_Click(object sender, RoutedEventArgs e)
        {
            AppSettings.Instance.AutoOpenTelemetry = ToggleAutoOpen.IsChecked == true;
            AppSettings.Instance.Save();
        }

        private void DataGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Handled)
                return;

            e.Handled = true;

            var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = MouseWheelEvent,
                Source = sender
            };

            if (sender is FrameworkElement element && element.Parent is UIElement parent)
                parent.RaiseEvent(eventArg);
        }

        // Clicking an already-selected row deselects it instead of leaving it selected.
        private void SensorRow_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!(sender is System.Windows.Controls.DataGridRow row))
                return;

            if (row.IsSelected && Keyboard.Modifiers == ModifierKeys.None)
            {
                row.IsSelected = false;
                e.Handled = true;
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            updateTimer?.Stop();
            _uptimeStatusTimer?.Stop();
            AppSettings.Instance.PropertyChanged -= AppSettings_PropertyChanged;
            sensorTelemetryLinks.Clear();
            moduleViewModels.Clear();
            sensorGroupViewModels.Clear();

            AppSettings appSettings = AppSettings.Instance;

            // Save window position and size if enabled
            if (appSettings.SaveWindowPosition)
            {
                appSettings.SensorsWindowLeft = Left;
                appSettings.SensorsWindowTop = Top;
                appSettings.SensorsWindowHeight = Height;
                appSettings.SensorsWindowWidth = Width;
                appSettings.Save();
            }
        }
    }

    public class ModuleViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private string header;
        private string partNumber;
        private string manufacturer;
        private string capacity;
        private string rank;
        private string memoryChip;
        private string pmicVendor;
        private string pmicRevision;
        private bool hasPmic;
        private bool hasTelemetry;
        private bool hasLogo;
        private string logoResourceName;
        private int hiddenCount;

        public string Header
        {
            get => header;
            set { header = value; OnPropertyChanged(nameof(Header)); OnPropertyChanged(nameof(HeaderDisplay)); }
        }

        public int HiddenCount
        {
            get => hiddenCount;
            set { hiddenCount = value; OnPropertyChanged(nameof(HiddenCount)); OnPropertyChanged(nameof(HeaderDisplay)); }
        }

        public string HeaderDisplay => HiddenCount > 0 ? $"{Header} ({HiddenCount} hidden)" : Header;

        public string PartNumber
        {
            get => partNumber;
            set { partNumber = value; OnPropertyChanged(nameof(PartNumber)); }
        }

        public string Manufacturer
        {
            get => manufacturer;
            set { manufacturer = value; OnPropertyChanged(nameof(Manufacturer)); }
        }

        public string Capacity
        {
            get => capacity;
            set { capacity = value; OnPropertyChanged(nameof(Capacity)); }
        }

        public string Rank
        {
            get => rank;
            set { rank = value; OnPropertyChanged(nameof(Rank)); }
        }

        public string MemoryChip
        {
            get => memoryChip;
            set { memoryChip = value; OnPropertyChanged(nameof(MemoryChip)); }
        }

        public string PmicVendor
        {
            get => pmicVendor;
            set { pmicVendor = value; OnPropertyChanged(nameof(PmicVendor)); }
        }

        public string PmicRevision
        {
            get => pmicRevision;
            set { pmicRevision = value; OnPropertyChanged(nameof(PmicRevision)); }
        }

        public bool HasPmic
        {
            get => hasPmic;
            set { hasPmic = value; OnPropertyChanged(nameof(HasPmic)); }
        }

        public bool HasTelemetry
        {
            get => hasTelemetry;
            set
            {
                hasTelemetry = value;
                OnPropertyChanged(nameof(HasTelemetry));
                OnPropertyChanged(nameof(HasNoTelemetry));
            }
        }

        public bool HasNoTelemetry => !HasTelemetry;

        public bool HasLogo
        {
            get => hasLogo;
            set { hasLogo = value; OnPropertyChanged(nameof(HasLogo)); }
        }

        public string LogoResourceName
        {
            get => logoResourceName;
            set { logoResourceName = value; OnPropertyChanged(nameof(LogoResourceName)); }
        }

        public ObservableCollection<TelemetryItemViewModel> TelemetryItems { get; } = new ObservableCollection<TelemetryItemViewModel>();

        public List<string> HiddenKeys { get; } = new List<string>();

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class SensorTelemetryLink
    {
        public SensorTelemetryLink(Sensor sensor, TelemetryItemViewModel item)
        {
            Sensor = sensor;
            Item = item;
        }

        public Sensor Sensor { get; }
        public TelemetryItemViewModel Item { get; }
    }

    public class SensorGroupViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private string header;
        private int hiddenCount;

        public string Header
        {
            get => header;
            set { header = value; OnPropertyChanged(nameof(Header)); OnPropertyChanged(nameof(HeaderDisplay)); }
        }

        public int HiddenCount
        {
            get => hiddenCount;
            set { hiddenCount = value; OnPropertyChanged(nameof(HiddenCount)); OnPropertyChanged(nameof(HeaderDisplay)); }
        }

        public string HeaderDisplay => HiddenCount > 0 ? $"{Header} ({HiddenCount} hidden)" : Header;

        public ObservableCollection<TelemetryItemViewModel> TelemetryItems { get; } = new ObservableCollection<TelemetryItemViewModel>();

        public List<string> HiddenKeys { get; } = new List<string>();

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public enum ThermalAlarmLevel
    {
        None,
        High,
        CriticalHigh
    }

    public enum SensorIconKind
    {
        Generic,
        Voltage,
        Temperature,
        Power,
        Fan
    }

    public class TelemetryItemViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private double currentValue;
        private double minValue = double.MaxValue;
        private double maxValue = double.MinValue;
        private double sum = 0;
        private int count = 0;

        private readonly string unit;
        private readonly bool _isBoolean;

        // Live alarm state
        private ThermalAlarmLevel currentAlarmLevel;

        // Alarm states captured when values were recorded
        private ThermalAlarmLevel minAlarmLevel;
        private ThermalAlarmLevel maxAlarmLevel;

        public string Name { get; }

        // Identifies the sensor's group + name for persisting hide/unhide state.
        public string GroupKey { get; set; }

        public SensorIconKind IconKind => GetIconKind(unit);

        private static SensorIconKind GetIconKind(string unit)
        {
            switch (unit)
            {
                case "V": return SensorIconKind.Voltage;
                case "°C": return SensorIconKind.Temperature;
                case "W": return SensorIconKind.Power;
                case "RPM": return SensorIconKind.Fan;
                default: return SensorIconKind.Generic;
            }
        }

        public ThermalAlarmLevel CurrentAlarmLevel
        {
            get => currentAlarmLevel;
            private set
            {
                currentAlarmLevel = value;
                OnPropertyChanged(nameof(CurrentAlarmLevel));
            }
        }

        public ThermalAlarmLevel MinAlarmLevel
        {
            get => minAlarmLevel;
            private set
            {
                minAlarmLevel = value;
                OnPropertyChanged(nameof(MinAlarmLevel));
            }
        }

        public ThermalAlarmLevel MaxAlarmLevel
        {
            get => maxAlarmLevel;
            private set
            {
                maxAlarmLevel = value;
                OnPropertyChanged(nameof(MaxAlarmLevel));
            }
        }

        public string Current => FormatValue(currentValue);
        public string Min => minValue != double.MaxValue ? FormatValue(minValue) : "-";
        public string Max => maxValue != double.MinValue ? FormatValue(maxValue) : "-";
        public string Average => count > 0 ? FormatValue(sum / count) : "-";

        // True when current, min and max values are all zero (or min/max have never been recorded).
        public bool IsAllZero =>
            !_isBoolean &&
            currentValue == 0 &&
            (minValue == double.MaxValue || minValue == 0) &&
            (maxValue == double.MinValue || maxValue == 0);

        public TelemetryItemViewModel(string name, double initialValue, string unit = "")
        {
            Name = name;
            this.unit = unit;
            UpdateValue(initialValue);
        }

        public TelemetryItemViewModel(string name, bool initialValue)
        {
            Name = name;
            this.unit = "";
            _isBoolean = true;
            UpdateValue(initialValue ? 1.0 : 0.0);
        }

        public void UpdateValue(double value)
        {
            currentValue = value;

            if (value < minValue)
            {
                minValue = value;

                // Preserve alarm state at recorded minimum
                MinAlarmLevel = CurrentAlarmLevel;
            }

            if (value > maxValue)
            {
                maxValue = value;

                // Preserve alarm state at recorded maximum
                MaxAlarmLevel = CurrentAlarmLevel;
            }

            sum += value;
            count++;

            OnPropertyChanged(nameof(Current));
            OnPropertyChanged(nameof(Min));
            OnPropertyChanged(nameof(Max));
            OnPropertyChanged(nameof(Average));
        }

        public void ResetStats()
        {
            minValue = currentValue;
            maxValue = currentValue;
            sum = currentValue;
            count = 1;

            MinAlarmLevel = CurrentAlarmLevel;
            MaxAlarmLevel = CurrentAlarmLevel;

            OnPropertyChanged(nameof(Min));
            OnPropertyChanged(nameof(Max));
            OnPropertyChanged(nameof(Average));
        }

        public void UpdateThermalAlarm(bool critHigh, bool high)
        {
            if (critHigh)
                CurrentAlarmLevel = ThermalAlarmLevel.CriticalHigh;
            else if (high)
                CurrentAlarmLevel = ThermalAlarmLevel.High;
            else
                CurrentAlarmLevel = ThermalAlarmLevel.None;
        }

        private string FormatValue(double value)
        {
            if (_isBoolean)
                return value >= 0.5 ? "Yes" : "No";

            string format = unit == "°C" ? "F2" : unit == "RPM" ? "F0" : "F3";
            return $"{value.ToString(format, CultureInfo.InvariantCulture)} {unit}";
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
