using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
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
            set
            {
                _timings = value;
                MemoryFrequency = value.Frequency;
                OnPropertyChanged();
            }
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
                if (string.IsNullOrEmpty(value) || value == AppSettings.AGESA_UNKNOWN || value == AGESA_SEARCHING)
                {
                    MotherboardInfo = $@"{CpuSingleton.Instance.systemInfo.MbName} | BIOS {CpuSingleton.Instance.systemInfo.BiosVersion} ({SmuVersion})";
                    _agesaVersion = value == AGESA_SEARCHING ? AGESA_SEARCHING : null;
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
        public bool IsRfcEnabled => Timings.RefreshMode == BankRefreshMode.NORMAL;
        public bool IsRfc2Enabled => Timings.RefreshMode != BankRefreshMode.NORMAL;
        public bool IsRfcsbEnabled => Timings.RefreshMode == BankRefreshMode.MIXED;

        // DDR4 doesn't have separate RFCsb, but we can still indicate if it's using normal refresh or FGR
        public bool IsDdr4RfcEnabled => (Timings as Ddr4Timings)?.RefreshMode == BankRefreshMode.NORMAL;
        public bool IsDdr4Rfc2Enabled => (Timings as Ddr4Timings)?.RefreshMode == BankRefreshMode.FGR && Timings.FGR == 2;
        public bool IsDdr4Rfc4Enabled => (Timings as Ddr4Timings)?.RefreshMode == BankRefreshMode.FGR && Timings.FGR == 4;

        public string CpuNameShortWithCores
        {
            get
            {
                string name = CpuName;
                var match = Regex.Match(
                    name,
                    @"\s+(?:\d+\s*-\s*Core\s+Processor|(?:with|w/)\s+Radeon)",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled
                );

                if (match.Success)
                {
                    name = name.Substring(0, match.Index).Trim();
                }

                string cores = $"({CpuSingleton.Instance.info.topology.cores}C/{CpuSingleton.Instance.info.topology.logicalCores}T)";
                return $"{name} {cores}";
            }
        }

        //private uint _selectedDctOffset = 0;
        //public uint SelectedDctOffset {
        //    get => _selectedDctOffset;
        //    set
        //    {
        //        _selectedDctOffset = value;
        //        if (_channelsApobData != null)
        //        {
        //            ApobData = _channelsApobData[_selectedDctOffset >> 20];
        //        }
        //    }
        //}

        //private readonly ApobData[] _channelsApobData;

        private ApobData _apobData;
        public ApobData ApobData
        {
            get => _apobData;
            set { _apobData = value; OnPropertyChanged(); }
        }

        private float _swaAdcV;
        public float SwaAdcV
        {
            get => _swaAdcV;
            set
            {
                _swaAdcV = value;
                OnPropertyChanged("SwaAdcV");
            }
        }

        private float _swbAdcV;
        public float SwbAdcV
        {
            get => _swbAdcV;
            set
            {
                _swbAdcV = value;
                OnPropertyChanged("SwbAdcV");
            }
        }

        private float _vppAdcV;
        public float VppAdcV
        {
            get => _vppAdcV;
            set
            {
                _vppAdcV = value;
                OnPropertyChanged("VppAdcV");
            }
        }

        private Ddr5PmicData _ddr5PmicData;
        public Ddr5PmicData PmicData
        {
            get => _ddr5PmicData;
            set
            {
                _ddr5PmicData = value;

                SwaAdcV = PmicData.SwaAdcMv / 1000.000f;
                SwbAdcV = PmicData.SwbAdcMv / 1000.000f;
                VppAdcV = PmicData.SwcAdcMv / 1000.000f;
                OnPropertyChanged("PmicData");
            }
        }

        public MainViewModel(
            BaseDramTimings timings,
            MemType memoryType,
            bool compatMode,
            AppSettings settings,
            List<IPlugin> plugins,
            string motherboardLogoName,
            string agesaVersion,
            Ddr5PmicData pmicData)
        {
            Timings = timings;
            Settings = settings;
            Plugins = plugins;

            CpuName = VendorUtils.GetCpuNameString(CpuSingleton.Instance.systemInfo);
            SmuVersion = CpuSingleton.Instance.systemInfo.GetSmuVersionString();

            TotalCapacity = CpuSingleton.Instance.GetMemoryConfig().TotalCapacity;
            MemoryType = memoryType;

            PowerTable = CpuSingleton.Instance.powerTable;
            CodeName = CpuSingleton.Instance.info.codeName;

            //_channelsApobData = CpuSingleton.Instance.info.apob.Data;
            //SelectedDctOffset = CpuSingleton.Instance.memoryConfig.Modules.FirstOrDefault()?.DctOffset ?? 0;
            ApobData = CpuSingleton.Instance.info.apob.Data;

            //AgesaVersion = AGESA_SEARCHING;
            AgesaVersion = agesaVersion;

            WMIPresent = (!compatMode && memoryType == MemType.DDR4)
                         || memoryType == MemType.LPDDR4;

            IsMotherboardLogoVisible = motherboardLogoName != null;
            MotherboardLogoTooltip = motherboardLogoName != null
                ? $"Click to visit {CpuSingleton.Instance.systemInfo.MbName} page"
                : string.Empty;

            PmicData = pmicData;
            //pmicData.
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
            html += $@"<div>Core Version: {cpu.Version}</div>";
            html += $@"<div>PawnIO Version: {DriverHelper.Version}</div>";
            html += $@"<div>Date: {DateTime.Now:dd MMMM yyyy HH:mm:ss}</div>";

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

            // Timings
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
                bool mismatch = IsMismatch(prop, uniqueTimings);
                string rowClass = mismatch ? " mismatch" : "";

                html += $"<tr class='{rowClass}'>";
                html += $"<td>{prop.Name}</td>";

                foreach (var timing in uniqueTimings)
                {
                    html += $"<td>{prop.GetValue(timing.Value)}</td>";
                }

                html += "</tr>";
            }
            html += "</table>";

            // PMT
            html += "<h2>PMT</h2>";
            html += "<table id='pmtTable' data-sort-col='' data-sort-dir=''>";

            type = cpu.powerTable.GetType();
            properties = type.GetProperties();

            foreach (var property in properties)
            {

                if (property.Name == "TableVersion")
                    html += $"<tr><td>{property.Name}</td><td>{property.GetValue(cpu.powerTable, null):X8}</td></tr>";
                else if (property.Name != "Table")
                    html += $"<tr><td>{property.Name}</td><td>{property.GetValue(cpu.powerTable, null)}</td></tr>";
            }
            html += "</table>";


            // AOD
            html += "<h2>AOD</h2>";
            html += "<table id='pmtTable' data-sort-col='' data-sort-dir=''>";

            type = cpu.info.aod.Table.Data.GetType();
            properties = type.GetProperties();

            foreach (var property in properties)
            {

                if (!property.Name.ToLowerInvariant().StartsWith("t"))
                    html += $"<tr><td>{property.Name}</td><td>{property.GetValue(cpu.info.aod.Table.Data, null)}</td></tr>";
            }
            html += "</table>";

            html += "</body></html>";

            return html;
        }

        public string GetJSON()
        {
            var cpu = CpuSingleton.Instance;
            var systemInfo = cpu.systemInfo;
            string appVersion = $"{System.Windows.Forms.Application.ProductName} {System.Windows.Forms.Application.ProductVersion}";

            var memConfigs = cpu.GetMemoryConfig();
            var allTimings = memConfigs.Timings;
            var uniqueTimings = allTimings
                .GroupBy(t => t.Key)
                .Select(g => g.First())
                .ToList();

            var jsonData = new
            {
                Application = new
                {
                    Version = appVersion,
                    CoreVersion = cpu.Version,
                    PawnIOVersion = DriverHelper.Version,
                    ExportDate = DateTime.Now.ToString("dd MMMM yyyy HH:mm:ss")
                },
                SystemInfo = new
                {
                    CpuName,
                    MotherboardName = systemInfo.MbName,
                    systemInfo.BiosVersion,
                    SmuVersion,
                    CpuId = $"{systemInfo.CpuId:X8}",
                    PatchLevel = $"{systemInfo.PatchLevel:X8}"
                },
                MemoryConfiguration = new
                {
                    Type = MemoryType.ToString(),
                    Frequency = MemoryFrequency,
                    TotalCapacity = TotalCapacity?.ToString() ?? "Unknown"
                },
                MemoryTimings = uniqueTimings.Select(timing => new
                {
                    DCT = timing.Key >> 20,
                    Timings = GetTimingDictionary(timing.Value)
                }).ToList(),
                PowerTable = GetPowerTableDictionary(cpu.powerTable),
                AOD = GetAODDictionary(cpu.info.aod.Table.Data)
            };

            return null;
            //return System.Text.Json.JsonSerializer.Serialize(jsonData, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }

        private Dictionary<string, object> GetTimingDictionary(BaseDramTimings timings)
        {
            var dict = new Dictionary<string, object>();
            var props = timings.GetType().GetProperties();

            foreach (var prop in props)
            {
                if (prop.GetIndexParameters().Length == 0)
                {
                    dict[prop.Name] = prop.GetValue(timings) ?? "N/A";
                }
            }

            return dict;
        }

        private Dictionary<string, object> GetPowerTableDictionary(PowerTable powerTable)
        {
            var dict = new Dictionary<string, object>();
            var props = powerTable.GetType().GetProperties();

            foreach (var prop in props)
            {
                if (prop.Name != "Table")
                {
                    dict[prop.Name] = prop.GetValue(powerTable) ?? "N/A";
                }
            }

            return dict;
        }

        private Dictionary<string, object> GetAODDictionary(object aodData)
        {
            var dict = new Dictionary<string, object>();
            var props = aodData.GetType().GetProperties();

            foreach (var prop in props)
            {
                if (!prop.Name.ToLowerInvariant().StartsWith("t"))
                {
                    dict[prop.Name] = prop.GetValue(aodData) ?? "N/A";
                }
            }

            return dict;
        }
    }
}
