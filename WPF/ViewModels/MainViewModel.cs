using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using ZenStates.Core;
using ZenStates.Core.DRAM;
using ZenTimings.Plugin;
using static ZenStates.Core.Cpu;

namespace ZenTimings.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            });
        }

        private BaseDramTimings _timings;
        public BaseDramTimings Timings
        {
            get => _timings;
            set { _timings = value; OnPropertyChanged(); }
        }
        public AppSettings Settings { get; }
        public List<IPlugin> Plugins { get; }
        private string _agesaVersion = "Searching for AGESA version...";
        public string AgesaVersion
        {
            get => _agesaVersion;
            set { _agesaVersion = value; OnPropertyChanged(); }
        }
        public Capacity TotalCapacity { get; }

        private float _memoryFrequency;
        public float MemoryFrequency {
            get => _memoryFrequency;
            set
            {
                _memoryFrequency = value;
                MemoryFrequencyString = $"{Math.Floor(MemoryFrequency)} MT/s";
                OnPropertyChanged();
            }
        }

        private string _memoryFrequencyString;
        public string MemoryFrequencyString
        {
            get => _memoryFrequencyString;
            set { _memoryFrequencyString = value; OnPropertyChanged(); }
        }
        public MemType MemoryType { get; }

        public PowerTable PowerTable { get; }
        public CodeName CodeName { get; }
        public bool WMIPresent { get; }
        public bool IsMotherboardLogoVisible { get; }
        public string MotherboardLogoTooltip { get; }

        public MainViewModel(
            BaseDramTimings timings,
            Cpu cpu,
            MemType memoryType,
            bool compatMode,
            AppSettings settings,
            List<IPlugin> plugins,
            string motherboardLogoName)
        {
            Timings = timings;
            Settings = settings;
            Plugins = plugins;

            TotalCapacity = cpu.memoryConfig.TotalCapacity;
            MemoryType = memoryType;

            PowerTable = cpu.powerTable;
            CodeName = cpu.info.codeName;

            WMIPresent = (!compatMode && memoryType == MemType.DDR4)
                         || memoryType == MemType.LPDDR4;

            IsMotherboardLogoVisible = motherboardLogoName != null;
            MotherboardLogoTooltip = motherboardLogoName != null
                ? $"Click to visit {cpu.systemInfo.MbName} page"
                : string.Empty;
        }
    }

}
