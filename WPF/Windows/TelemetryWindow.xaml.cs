using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ZenStates.Core;
using ZenStates.Core.DRAM;

namespace ZenTimings.Windows
{
    public partial class TelemetryWindow : ThemedAdonisWindow
    {
        private DispatcherTimer updateTimer;
        private DispatcherTimer _uptimeTimer;
        private DateTime _windowOpenedAt;
        private readonly MemoryConfig memoryConfig;
        private readonly ObservableCollection<ModuleViewModel> moduleViewModels = new ObservableCollection<ModuleViewModel>();
        private bool _isRefreshing;

        public TelemetryWindow()
        {
            InitializeComponent();
            memoryConfig = CpuSingleton.Instance.memoryConfig;

            updateTimer = new DispatcherTimer();
            updateTimer.Tick += RefreshTimer_Tick;

            _uptimeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _uptimeTimer.Tick += UptimeTimer_Tick;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ModulesContainer.ItemsSource = moduleViewModels;
            AppSettings.Instance.PropertyChanged += AppSettings_PropertyChanged;
            await LoadModulesDataAsync();
            ChkAutoRefresh.IsChecked = AppSettings.Instance.TelemetryAutoRefresh;
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
        }

        private async Task LoadModulesDataAsync()
        {
            if (memoryConfig == null)
            {
                StatusText.Text = "Memory configuration not available";
                return;
            }

            // Block periodic refresh while the initial SMBUS load is in progress.
            _isRefreshing = true;
            BtnRefresh.IsEnabled = false;
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

            BtnRefresh.IsEnabled = true;
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
                        InitializeThermalTelemetry(vm, spdEntry.Value.ThermalData);

                    if (spdEntry.Value.PmicData != null && spdEntry.Value.PmicData.IsValid)
                    {
                        vm.HasPmic = true;
                        vm.PmicVendor = spdEntry.Value.PmicData.VendorName ?? "N/A";
                        vm.PmicRevision = $"{spdEntry.Value.PmicData.RevisionMajor}.{spdEntry.Value.PmicData.RevisionMinor}";
                        InitializePmicTelemetry(vm, spdEntry.Value.PmicData);
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

        private void InitializePmicTelemetry(ModuleViewModel vm, Ddr5PmicData pmicData)
        {
            vm.HasTelemetry = true;

            // VDD (SWA)
            vm.TelemetryItems.Add(new TelemetryItemViewModel("VDD (SWA)", pmicData.SwaAdcMv / 1000.0, "V"));
            
            // VDDQ (SWB)
            vm.TelemetryItems.Add(new TelemetryItemViewModel("VDDQ (SWB)", pmicData.SwbAdcMv / 1000.0, "V"));
            
            // VPP (SWC)
            vm.TelemetryItems.Add(new TelemetryItemViewModel("VPP (SWC)", pmicData.SwcAdcMv / 1000.0, "V"));
            
            // VIN Bulk
            vm.TelemetryItems.Add(new TelemetryItemViewModel("VIN Bulk", pmicData.VinBulkMv / 1000.0, "V"));
            
            // 1.8V LDO
            vm.TelemetryItems.Add(new TelemetryItemViewModel("VOUT 1.8V", pmicData.Vout18AdcMv / 1000.0, "V"));
            
            // 1.0V LDO
            vm.TelemetryItems.Add(new TelemetryItemViewModel("VOUT 1.0V", pmicData.Vout10AdcMv / 1000.0, "V"));

            // PMIC Temperature
            if (!string.IsNullOrEmpty(pmicData.PmicTemperature))
            {
                if (double.TryParse(pmicData.PmicTemperature.Replace("°C", "").Trim(), out double tempValue))
                {
                    vm.TelemetryItems.Add(new TelemetryItemViewModel("PMIC Temp", tempValue, "°C"));
                }
            }

            // Total Power
            //if (pmicData.TelemetryReportsTotalPower)
            {
                vm.TelemetryItems.Add(new TelemetryItemViewModel("Total Power", pmicData.TotalW, "W"));
            }

            // High Temperature Warning
            var pmicHighTempItem = new TelemetryItemViewModel("PMIC High Temp", pmicData.HighTemperatureWarning);
            pmicHighTempItem.UpdateThermalAlarm(pmicData.CriticalTemperatureShutdown, pmicData.HighTemperatureWarning);
            vm.TelemetryItems.Add(pmicHighTempItem);
        }

        private void InitializeThermalTelemetry(ModuleViewModel vm, Ddr5ThermalData thermalData)
        {
            vm.HasTelemetry = true;
            var item = new TelemetryItemViewModel("SPD Hub Temp", thermalData.TemperatureC, "°C");
            item.UpdateThermalAlarm(thermalData.AlarmCritHigh, thermalData.AlarmHigh);
            vm.TelemetryItems.Add(item);
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshTelemetry();
        }

        private async void RefreshTelemetry()
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

        private void ChkAutoRefresh_Checked(object sender, RoutedEventArgs e)
        {
            _windowOpenedAt = DateTime.Now;
            _uptimeTimer.Start();
            UptimeTimer_Tick(null, null);
            int interval = AppSettings.Instance.AutoRefreshInterval;
            updateTimer.Interval = TimeSpan.FromMilliseconds(interval);
            updateTimer.Start();
        }

        private void ChkAutoRefresh_Unchecked(object sender, RoutedEventArgs e)
        {
            updateTimer.Stop();
            _uptimeTimer.Stop();
            _windowOpenedAt = DateTime.Now;
            StatusText.Text = "Auto-refresh off";
        }

        private void RefreshTimer_Tick(object sender, EventArgs e)
        {
            RefreshTelemetry();
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

            _windowOpenedAt = DateTime.Now;
            UptimeTimer_Tick(null, null);
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            updateTimer?.Stop();
            _uptimeTimer?.Stop();
            AppSettings.Instance.PropertyChanged -= AppSettings_PropertyChanged;

            AppSettings appSettings = AppSettings.Instance;
            appSettings.TelemetryAutoRefresh = ChkAutoRefresh.IsChecked == true;

            // Save window position and size if enabled
            if (appSettings.SaveWindowPosition)
            {
                appSettings.TelemetryWindowLeft = Left;
                appSettings.TelemetryWindowTop = Top;
                appSettings.TelemetryWindowHeight = Height;
                appSettings.TelemetryWindowWidth = Width;
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

        public string Header
        {
            get => header;
            set { header = value; OnPropertyChanged(nameof(Header)); }
        }

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
        private ThermalAlarmLevel alarmLevel;

        public string Name { get; }

        public ThermalAlarmLevel AlarmLevel
        {
            get => alarmLevel;
            private set { alarmLevel = value; OnPropertyChanged(nameof(AlarmLevel)); }
        }

        public string Current => FormatValue(currentValue);
        public string Min => minValue != double.MaxValue ? FormatValue(minValue) : "-";
        public string Max => maxValue != double.MinValue ? FormatValue(maxValue) : "-";
        public string Average => count > 0 ? FormatValue(sum / count) : "-";

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
                minValue = value;
            
            if (value > maxValue)
                maxValue = value;
            
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

            OnPropertyChanged(nameof(Min));
            OnPropertyChanged(nameof(Max));
            OnPropertyChanged(nameof(Average));
        }

        public void UpdateThermalAlarm(bool critHigh, bool high)
        {
            if (critHigh)
                AlarmLevel = ThermalAlarmLevel.CriticalHigh;
            else if (high)
                AlarmLevel = ThermalAlarmLevel.High;
            else
                AlarmLevel = ThermalAlarmLevel.None;
        }

        private string FormatValue(double value)
        {
            if (_isBoolean) return value >= 0.5 ? "Yes" : "No";
            string format = unit == "°C" ? "F2" : "F3";
            return $"{value.ToString(format)} {unit}";
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
