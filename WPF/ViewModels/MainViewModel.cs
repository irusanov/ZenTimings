using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Xml.Serialization;
using ZenStates.Core;
using ZenStates.Core.DRAM;
using ZenTimings.Plugin;

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

        private static string AGESA_SEARCHING = "Searching for AGESA version...";

        private readonly string SmuVersion;

        private BaseDramTimings _timings;
        public BaseDramTimings Timings
        {
            get => _timings;
            set { _timings = value; OnPropertyChanged(); }
        }
        public AppSettings Settings { get; }
        public List<IPlugin> Plugins { get; }

        public string CpuName { get; } = string.Empty;

        private string _motherboardInfo = string.Empty;
        public string MotherboardInfo
        {
            get => _motherboardInfo;
            set { _motherboardInfo = value; OnPropertyChanged(); }
        }

        private string _agesaVersion = AGESA_SEARCHING;
        public string AgesaVersion
        {
            get => _agesaVersion;
            set
            {
                if (string.IsNullOrEmpty(value) || value == AppSettings.AGESA_UNKNOWN)
                {
                    MotherboardInfo = $@"{CpuSingleton.Instance.systemInfo.MbName} | BIOS {CpuSingleton.Instance.systemInfo.BiosVersion} ({SmuVersion})";
                    _agesaVersion = null;
                }
                else
                {
                    MotherboardInfo = $@"{CpuSingleton.Instance.systemInfo.MbName} | BIOS {CpuSingleton.Instance.systemInfo.BiosVersion}";
                    _agesaVersion = $"AGESA {value} (SMU {SmuVersion})";
                }
                IsAgesaVersionVisible = !string.IsNullOrEmpty(_agesaVersion);
                OnPropertyChanged();
            }
        }

        private bool _isAgesaVersionVisible;
        public bool IsAgesaVersionVisible
        {
            get => _isAgesaVersionVisible;
            set { _isAgesaVersionVisible = value; OnPropertyChanged(); }
        }

        public bool IsSearchingForAgesaVersion => _agesaVersion == AGESA_SEARCHING;

        public Capacity TotalCapacity { get; }

        private float _memoryFrequency;
        public float MemoryFrequency
        {
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
        public Cpu.CodeName CodeName { get; }
        public bool WMIPresent { get; }
        public bool IsMotherboardLogoVisible { get; }
        public string MotherboardLogoTooltip { get; }

        public MainViewModel(
            BaseDramTimings timings,
            MemType memoryType,
            bool compatMode,
            AppSettings settings,
            List<IPlugin> plugins,
            string motherboardLogoName)
        {
            Timings = timings;
            Settings = settings;
            Plugins = plugins;

            CpuName = VendorUtils.GetCpuNameString(CpuSingleton.Instance.systemInfo);
            SmuVersion = CpuSingleton.Instance.systemInfo.GetSmuVersionString();

            TotalCapacity = CpuSingleton.Instance.memoryConfig.TotalCapacity;
            MemoryType = memoryType;

            PowerTable = CpuSingleton.Instance.powerTable;
            CodeName = CpuSingleton.Instance.info.codeName;

            WMIPresent = (!compatMode && memoryType == MemType.DDR4)
                         || memoryType == MemType.LPDDR4;

            IsMotherboardLogoVisible = motherboardLogoName != null;
            MotherboardLogoTooltip = motherboardLogoName != null
                ? $"Click to visit {CpuSingleton.Instance.systemInfo.MbName} page"
                : string.Empty;
        }

        bool IsMismatch(
            PropertyInfo prop,
            List<KeyValuePair<uint, BaseDramTimings>> timings)
        {
            object first = null;
            bool firstSet = false;

            foreach (var t in timings)
            {
                object value;
                try
                {
                    value = prop.GetValue(t.Value);
                }
                catch
                {
                    continue;
                }

                if (!firstSet)
                {
                    first = value;
                    firstSet = true;
                }
                else if (!Equals(first, value))
                {
                    return true;
                }
            }
            return false;
        }

        public string GetXML()
        {
            XmlSerializer x = new XmlSerializer(this.GetType());
            using (StringWriter textWriter = new StringWriter())
            {
                x.Serialize(textWriter, this);
                return textWriter.ToString();
            }
        }

        public string GetHTML()
        {
            var cpu = CpuSingleton.Instance;
            var type = cpu.systemInfo.GetType();
            var properties = type.GetProperties();
            string appVersion = $"{System.Windows.Forms.Application.ProductName} {System.Windows.Forms.Application.ProductVersion}";

            string html = @"<!DOCTYPE html>
            <html>
            <head>
            <title></title>
            <style>
            body {
                font-family: Segoe UI, Tahoma, sans-serif;
                background: #f7f9fb;
                color: #1f2937;
            }

            h2 {
                color: #2563eb;
                border-bottom: 2px solid #e5e7eb;
                padding-bottom: 4px;
            }

            table {
                border-collapse: collapse;
                width: auto;
                margin-bottom: 20px;
                background: #ffffff;
            }

            th, td {
                border: 1px solid #e5e7eb;
                padding: 6px 8px;
                text-align: center;
                font-size: 13px;
            }

            th {
                background: #e8f0fe;
                color: #1d4ed8;
                cursor: pointer;
                user-select: none;
            }

            td:first-child {
                text-align: left;
                font-weight: 500;
            }

            tr.mismatch td {
                background: #fff1f2;
            }

            tr.primary td:first-child {
                color: #0f172a;
                font-weight: 700;
            }

            tr.secondary td:first-child {
                color: #475569;
            }
            </style>
            </head>
            <body>";
            html += $@"<h1>{appVersion}</h1>";
            html += $@"<p>Core Version: {cpu.Version}</p>";
            html += $@"<p>PawnIO Version: {DriverHelper.Version}</p>";

            html += "<h2>System Info</h2>";
            html += "<table border=\"1\" cellspacing=\"0\" cellpadding=\"4\">";

            foreach (var property in properties)
            {
                if (property.Name == "CpuId" || property.Name == "PatchLevel" || property.Name == "SmuTableVersion")
                    html += $"<tr><td>{property.Name}</td><td>{property.GetValue(cpu.systemInfo, null):X8}</td></tr>";
                else if (property.Name == "SmuVersion")
                    html += $"<tr><td>{property.Name}</td><td>{cpu.systemInfo.GetSmuVersionString()}</td></tr>";
                else if (property.Name == "Model" || property.Name == "ExtendedModel" || property.Name == "BaseModel")
                    html += $"<tr><td>{property.Name}</td><td>{property.GetValue(cpu.systemInfo, null)} (0x{property.GetValue(cpu.systemInfo, null):X})</td></tr>";
                else if (property.Name != "SMBios")
                    html += $"<tr><td>{property.Name}</td><td>{property.GetValue(cpu.systemInfo, null)}</td></tr>";
            }

            html += "</table>";


            var memConfigs = cpu.GetMemoryConfig();
            var allTimings = memConfigs.Timings;
            var props = allTimings[0].Value.GetType().GetProperties();

            // Filter timings to only include unique DctOffset values
            var uniqueTimings = allTimings
                .GroupBy(t => t.Key)
                .Select(g => g.First())
                .ToList();

            var timingProperties = uniqueTimings[0].Value
                .GetType()
                .GetProperties()
                .Where(p => p.GetIndexParameters().Length == 0)
                .ToList();

            var primaryTimings = new HashSet<string>
            {
                "tCL", "tRCD", "tRP", "tRAS", "tRC",
                "tRRDS", "tRRDL", "tFAW",
                "tCWL", "tWR"
            };


            html += "<h2>Memory Timings</h2>";
            html += "<table id='timingsTable' data-sort-col='' data-sort-dir=''>";
            html += "<tr><th onclick='sortTable(0)'>Timing</th>";

            foreach (var timing in uniqueTimings)
            {
                html += $"<th onclick='sortTable({uniqueTimings.IndexOf(timing) + 1})'>DCT {timing.Key >> 20}</th>";
            }

            html += "</tr>";

            foreach (var prop in timingProperties)
            {
                bool isPrimary = primaryTimings.Contains(prop.Name);
                bool mismatch = IsMismatch(prop, uniqueTimings);

                string rowClass = (isPrimary ? "primary" : "secondary") +
                                  (mismatch ? " mismatch" : "");

                html += $"<tr class='{rowClass}'>";
                html += $"<td>{prop.Name}</td>";

                foreach (var timing in uniqueTimings)
                {
                    html += $"<td>{prop.GetValue(timing.Value)}</td>";
                }

                html += "</tr>";
            }

            html += "</table>";

            html += "</body></html>";

            return html;
        }
    }
}
