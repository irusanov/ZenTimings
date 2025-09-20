using System;
using System.Collections.Generic;
using System.ComponentModel;
using ZenStates.Core;
using ZenStates.Core.DRAM;
using ZenTimings.Plugin;

namespace ZenTimings
{
    public readonly struct MemoryFrequencyProp
    {
        private readonly float value;

        public MemoryFrequencyProp(float value)
        {
            this.value = value;
        }

        public override string ToString() => $"{Math.Floor(value)} MT/s";

        public static implicit operator MemoryFrequencyProp(float value) => new MemoryFrequencyProp(value);
        public static implicit operator float(MemoryFrequencyProp flag) => flag.value;
    }

    internal class ViewData: INotifyPropertyChanged
    {
        private BaseDramTimings _timings;
        public BaseDramTimings Timings
        {
            get => _timings;
            set
            {
                if (_timings != value)
                {
                    _timings = value;
                    OnPropertyChanged(nameof(Timings));
                }
            }
        }

        private Capacity _totalCapacity;
        public Capacity TotalCapacity
        {
            get => _totalCapacity;
            set
            {
                if (_totalCapacity != value)
                {
                    _totalCapacity = value;
                    OnPropertyChanged(nameof(TotalCapacity));
                }
            }
        }

        private MemoryFrequencyProp _memoryFrequency;
        public MemoryFrequencyProp MemoryFrequency
        {
            get => _memoryFrequency;
            set
            {
                if (_memoryFrequency != value)
                {
                    _memoryFrequency = value;
                    OnPropertyChanged(nameof(MemoryFrequency));
                }
            }
        }

        private PowerTable _powerTable;
        public PowerTable PowerTable
        {
            get => _powerTable;
            set
            {
                if (_powerTable != value)
                {
                    _powerTable = value;
                    OnPropertyChanged(nameof(PowerTable));
                }
            }
        }

        private Cpu.CodeName _codeName;
        public Cpu.CodeName CodeName
        {
            get => _codeName;
            set
            {
                if (_codeName != value)
                {
                    _codeName = value;
                    OnPropertyChanged(nameof(CodeName));
                }
            }
        }

        private bool _wmIPresent;
        public bool WMIPresent
        {
            get => _wmIPresent;
            set
            {
                if (_wmIPresent != value)
                {
                    _wmIPresent = value;
                    OnPropertyChanged(nameof(WMIPresent));
                }
            }
        }

        private AppSettings _settings;
        public AppSettings Settings
        {
            get => _settings;
            set
            {
                if (_settings != value)
                {
                    _settings = value;
                    OnPropertyChanged(nameof(Settings));
                }
            }
        }

        private List<IPlugin> _plugins;
        public List<IPlugin> Plugins
        {
            get => _plugins;
            set
            {
                if (_plugins != value)
                {
                    _plugins = value;
                    OnPropertyChanged(nameof(Plugins));
                }
            }
        }

        private bool _isRogMotherboard;
        public bool IsRogMotherboard
        {
            get => _isRogMotherboard;
            set
            {
                if (_isRogMotherboard != value)
                {
                    _isRogMotherboard = value;
                    OnPropertyChanged(nameof(IsRogMotherboard));
                }
            }
        }

        private string _rogLogoTooltip;
        public string RogLogoTooltip
        {
            get => _rogLogoTooltip;
            set
            {
                if (_rogLogoTooltip != value)
                {
                    _rogLogoTooltip = value;
                    OnPropertyChanged(nameof(RogLogoTooltip));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
