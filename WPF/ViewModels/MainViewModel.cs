using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using ZenStates.Core;
using ZenStates.Core.Hardware;
using ZenStates.Core.Hardware.Aod;
using ZenStates.Core.Hardware.Apob;
using ZenStates.Core.Hardware.DRAM;
using ZenStates.Core.Hardware.DRAM.DDR5.Pmic;
using ZenStates.Core.Hardware.Mock;
using ZenTimings.Common;
using ZenTimings.Export;
using ZenTimings.Helpers;
using ZenTimings.Plugin;
using ZenTimings.Settings;
using ZenTimings.Utils;
using static ZenTimings.Settings.AppSettings;

namespace ZenTimings.ViewModels
{
    public class MainViewModel : ObservableObject
    {
        private static readonly string AGESA_SEARCHING = "Searching for AGESA version...";

        public enum VoltageRail
        {
            Vsoc,
            Vddio,
            Vmisc
        }

        // Non-null only when this view model is showing a debug report instead of live hardware
        // (see the MockSystemData-accepting constructor overload below). Every place that would
        // otherwise reach for CpuSingleton.Instance checks this first.
        private readonly MockSystemData mockData;

        private readonly string SmuVersion;

        private BaseDramTimings _timings;
        public BaseDramTimings Timings
        {
            get => _timings;
            set
            {
                _timings = value;
                // Frequency comes from the live power table; a debug report sets its own speed instead.
                if (mockData == null)
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
            set => SetProperty(ref _motherboardInfo, value);
        }

        private string _agesaVersion = AGESA_SEARCHING;
        public string AgesaVersion
        {
            get => _agesaVersion;
            set
            {
                string mbName = mockData != null ? mockData.MbName : CpuSingleton.Instance.systemInfo.MbName;
                string biosVersion = mockData != null ? mockData.BiosVersion : CpuSingleton.Instance.systemInfo.BiosVersion;

                if (string.IsNullOrEmpty(value) || value == AppSettings.AGESA_UNKNOWN || value == AGESA_SEARCHING)
                {
                    MotherboardInfo = $@"{mbName} | BIOS {biosVersion} ({SmuVersion})";
                    _agesaVersion = value == AGESA_SEARCHING ? AGESA_SEARCHING : null;
                }
                else
                {
                    MotherboardInfo = $@"{mbName} | BIOS {biosVersion}";
                    _agesaVersion = $"AGESA {value} (SMU {SmuVersion})";
                }
                IsAgesaVersionVisible = !string.IsNullOrEmpty(_agesaVersion);
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsSearchingForAgesaVersion));
            }
        }

        private bool _isAgesaVersionVisible;
        public bool IsAgesaVersionVisible
        {
            get => _isAgesaVersionVisible;
            set => SetProperty(ref _isAgesaVersionVisible, value);
        }

        public bool IsSearchingForAgesaVersion => _agesaVersion == AGESA_SEARCHING;

        public Capacity TotalCapacity { get; }

        private float _memoryFrequency;
        public float MemoryFrequency
        {
            get => _memoryFrequency;
            set
            {
                if (SetProperty(ref _memoryFrequency, value))
                    MemoryFrequencyString = $"{Math.Floor(value)} MT/s";
            }
        }

        private string _memoryFrequencyString;
        public string MemoryFrequencyString
        {
            get => _memoryFrequencyString;
            set => SetProperty(ref _memoryFrequencyString, value);
        }
        public MemType MemoryType { get; }
        public bool IsDimmTelemetryAvailable => Settings.AdvancedMode && MemoryType == MemType.DDR5;
        public bool ECC { get; set; }
        private bool IsVmiscSupported => CpuFamily >= Cpu.Family.FAMILY_19H;
        public PowerTable PowerTable { get; }
        public Cpu.CodeName CodeName { get; }
        public Cpu.Family CpuFamily { get; }
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

                // A debug report carries its own topology; one from an older build has none, and the live
                // machine's would be wrong for it.
                Cpu.CpuTopology topology = mockData != null ? mockData.CpuInfo.topology : CpuSingleton.Instance.info.topology;
                return topology.cores > 0 ? $"{name} ({topology.cores}C/{topology.logicalCores}T)" : name;
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

        private ApobData _apobMainData;
        public ApobData ApobMainData
        {
            get => _apobMainData;
            set => SetProperty(ref _apobMainData, value);
        }

        private ApobData _apobExtendedData;
        public ApobData ApobExtendedData
        {
            get => _apobExtendedData;
            set => SetProperty(ref _apobExtendedData, value);
        }


        private ApobDataView _apobData;

        public ApobDataView ApobData
        {
            get => _apobData;
            set => SetProperty(ref _apobData, value);
        }

        //public ApobData ApobData
        //{
        //    get
        //    {
        //        if (ApobExtendedData != null 
        //            && ApobExtendedData.ProcOdt != null 
        //            && ApobExtendedData.ProcOdt.RawValue.Equals(ApobMainData.ProcOdt.RawValue))
        //            return ApobExtendedData;
        //        return ApobMainData;
        //    }
        //}

        private CcdlData _ccdlData;
        public CcdlData CcdlData
        {
            get => _ccdlData;
            set => SetProperty(ref _ccdlData, value);
        }

        private float _swaAdcV;
        public float SwaAdcV
        {
            get => _swaAdcV;
            set => SetProperty(ref _swaAdcV, value);
        }

        private float _swbAdcV;
        public float SwbAdcV
        {
            get => _swbAdcV;
            set => SetProperty(ref _swbAdcV, value);
        }

        private float _vppAdcV;
        public float VppAdcV
        {
            get => _vppAdcV;
            set => SetProperty(ref _vppAdcV, value);
        }

        private float _apuVddio;
        public float ApuVddio
        {
            get => _apuVddio;
            set => SetProperty(ref _apuVddio, value);
        }

        private float _vsoc;
        public float Vsoc
        {
            get => _vsoc;
            set => SetProperty(ref _vsoc, value);
        }

        private float _vmisc;
        public float Vmisc
        {
            get => _vmisc;
            set => SetProperty(ref _vmisc, value);
        }

        // Row labels naming the source each rail's value came from, e.g. "VSOC (SMU)" or "VDDIO (AOD)".
        // They keep the plain name while a rail has no reading.
        private string _vsocLabel = "VSOC";
        public string VsocLabel
        {
            get => _vsocLabel;
            set => SetProperty(ref _vsocLabel, value);
        }

        private string _vddioLabel = "CPU VDDIO";
        public string VddioLabel
        {
            get => _vddioLabel;
            set => SetProperty(ref _vddioLabel, value);
        }

        private string _vmiscLabel = "VDD MISC";
        public string VmiscLabel
        {
            get => _vmiscLabel;
            set => SetProperty(ref _vmiscLabel, value);
        }

        // The decoded AOD table: rebuilt from the debug report's raw dump in a mock window, read from
        // the live machine otherwise. Everything below that needs AOD goes through here.
        private AodData AodData =>
            mockData != null ? mockData.AodData : CpuSingleton.Instance?.info.aod?.Table?.Data;

        private Ddr5PmicData _ddr5PmicData;
        public Ddr5PmicData PmicData
        {
            get => _ddr5PmicData;
            set
            {
                if (value == null) return;

                _ddr5PmicData = value;

                if (value.SwaAdcMv > 0)
                    SwaAdcV = value.SwaAdcMv / 1000.0f;

                if (value.SwbAdcMv > 0)
                    SwbAdcV = value.SwbAdcMv / 1000.0f;

                if (value.SwcAdcMv > 0)
                    VppAdcV = value.SwcAdcMv / 1000.0f;
            }
        }

        public MainViewModel CreateChannelViewModel(BaseDramTimings timings)
        {
            return CreateChannelViewModel(timings, PmicData);
        }

        public MainViewModel CreateChannelViewModel(BaseDramTimings timings, Ddr5PmicData pmicData)
        {
            return new MainViewModel(
                timings,
                MemoryType,
                false,
                Settings,
                Plugins,
                null,
                mockData != null ? mockData.AgesaVersion : null,
                pmicData,
                mockData);
        }

        public MainViewModel(
            BaseDramTimings timings,
            MemType memoryType,
            bool compatMode,
            AppSettings settings,
            List<IPlugin> plugins,
            string motherboardLogoName,
            string agesaVersion,
            Ddr5PmicData pmicData,
            MockSystemData mockData = null)
        {
            this.mockData = mockData;
            Timings = timings;
            Settings = settings;
            Plugins = plugins;

            if (mockData != null)
            {
                // Debug-report-driven ("mock") window: everything comes from the parsed report,
                // never from the live machine's CpuSingleton.
                CpuName = mockData.CpuName ?? "Unknown CPU";
                SmuVersion = mockData.SmuVersion ?? "Unknown";
                TotalCapacity = mockData.TotalCapacity;
                CodeName = mockData.CpuInfo.codeName;
                CpuFamily = mockData.CpuInfo.family;
                PowerTable = mockData.PowerTable;
                MemoryFrequencyString = $"{(PowerTable.MCLK * 2)} MT/s";
            }
            else
            {
                CpuName = VendorUtils.GetCpuNameString(CpuSingleton.Instance.systemInfo);
                SmuVersion = CpuSingleton.Instance?.systemInfo?.SmuVersion.ToString() ?? "Unknown";
                TotalCapacity = CpuSingleton.Instance.GetMemoryConfig().TotalCapacity;
                CodeName = CpuSingleton.Instance.info.codeName;
                CpuFamily = CpuSingleton.Instance.info.family;
                PowerTable = CpuSingleton.Instance?.powerTable;
            }

            MemoryType = memoryType;

            // APOB - either the mock report's own instance, or the live one
            Apob apob = mockData != null ? mockData.Apob : CpuSingleton.Instance.info.apob;
            if (apob != null && apob.IsValid)
            {
                ApobMainData = apob.Data;
                ApobExtendedData = apob.ExtendedData;
                CcdlData = apob.CcdlData;
                ApobData = new ApobDataView(ApobMainData, ApobExtendedData);
            }

            //AgesaVersion = AGESA_SEARCHING;
            AgesaVersion = mockData != null ? (mockData.AgesaVersion ?? agesaVersion) : agesaVersion;

            WMIPresent = mockData == null &&
                         ((!compatMode && memoryType == MemType.DDR4) || memoryType == MemType.LPDDR4);

            IsMotherboardLogoVisible = motherboardLogoName != null;
            MotherboardLogoTooltip = motherboardLogoName != null
                ? $"Click to visit {(mockData != null ? mockData.MbName : CpuSingleton.Instance.systemInfo.MbName)} page"
                : string.Empty;

            if (memoryType == MemType.DDR5 || memoryType == MemType.LPDDR5)
            {
                if (pmicData != null && pmicData.IsValid)
                {
                    PmicData = pmicData;
                }
                else
                {
                    // Fall back to the AOD table's programmed rails when the PMIC can't be read - live
                    // over SMBus, or from a report without SPD dumps.
                    var aodData = AodData;
                    if (aodData != null)
                    {
                        SwaAdcV = aodData?.MemVddio != null ? aodData.MemVddio.RawValue / 1000.0f : 0;
                        SwbAdcV = aodData?.MemVddq != null ? aodData.MemVddq.RawValue / 1000.0f : 0;
                        VppAdcV = aodData?.MemVpp != null ? aodData.MemVpp.RawValue / 1000.0f : 0;
                    }
                }
            }

            // ECC: not captured by the debug report today, so left at its default (false) for mock data
            ECC = mockData == null && SystemInfo.SMBios.MemoryDevices.Any(d => d.HasEcc);

            // Live or mock - RefreshSensors picks the sources each can offer.
            RefreshSensors();
        }

        private static readonly string[] ApuVddioSensorNames = { "CPU VDDIO", "VDIMM", "VDDIO", "CPU VDDIO Memory" };
        private static readonly string[] VsocSensorNames = { "CPU NB/SoC", "Vcore SoC", "VSOC", "VDDCR_SOC", "CPU SoC", "Northbridge/SoC" };
        private static readonly string[] VmiscSensorNames = { "CPU MISC", "VMISC", "Vcore Misc", "VDD Misc" };

        private bool _sensorsDetected;
        private Sensor _apuVddioSensor;
        private Sensor _vsocSensor;
        private Sensor _vmiscSensor;

        // SuperIO sensors: replayed from the debug report's register dumps in a mock window, live otherwise.
        private IEnumerable<SuperIoSensorGroup> SensorGroups =>
            mockData != null ? mockData.SensorGroups : CpuSingleton.Instance?.systemInfo?.SensorGroups;

        // Locates the relevant sensors once and caches them so subsequent refreshes don't need to search by name again.
        private void DetectSensors()
        {
            var sensors = SensorGroups?
                .SelectMany(g => g.Sensors)
                .ToList();

            _apuVddioSensor = sensors?.FirstOrDefault(s => ApuVddioSensorNames.Contains(s.Name, StringComparer.OrdinalIgnoreCase));
            _vsocSensor = sensors?.FirstOrDefault(s => VsocSensorNames.Contains(s.Name, StringComparer.OrdinalIgnoreCase));
            _vmiscSensor = sensors?.FirstOrDefault(s => VmiscSensorNames.Contains(s.Name, StringComparer.OrdinalIgnoreCase));

            _sensorsDetected = true;
        }

        public IReadOnlyList<VoltageSensorSource> GetAvailableVoltageSources(VoltageRail rail)
        {
            if (!_sensorsDetected)
                DetectSensors();

            var sources = new List<VoltageSensorSource>();

            if (rail == VoltageRail.Vmisc && !IsVmiscSupported)
                return sources;

            if ((rail == VoltageRail.Vsoc && _vsocSensor != null) ||
                (rail == VoltageRail.Vddio && _apuVddioSensor != null) ||
                (rail == VoltageRail.Vmisc && _vmiscSensor != null))
            {
                sources.Add(VoltageSensorSource.SuperIo);
            }

            if ((rail == VoltageRail.Vsoc && PowerTable?.VDDCR_SOC > 0) ||
                (rail == VoltageRail.Vmisc && PowerTable?.VDD_MISC > 0))
            {
                sources.Add(VoltageSensorSource.Smu);
            }

            if (rail == VoltageRail.Vddio && AodData?.ApuVddio != null)
            {
                sources.Add(VoltageSensorSource.Aod);
            }

            return sources;
        }

        private VoltageSensorSource GetSelectedVoltageSource(VoltageRail rail)
        {
            var availableSources = GetAvailableVoltageSources(rail);
            VoltageSensorSource selectedSource;
            switch (rail)
            {
                case VoltageRail.Vsoc:
                    selectedSource = Settings.VsocSensorSource;
                    break;
                case VoltageRail.Vddio:
                    selectedSource = Settings.VddioSensorSource;
                    break;
                default:
                    selectedSource = Settings.VmiscSensorSource;
                    break;
            }

            return availableSources.Contains(selectedSource) ? selectedSource : availableSources.FirstOrDefault();
        }

        // Reads a single source for one rail. Returns 0 when that source has nothing to report right now.
        private float ReadVoltageFrom(VoltageRail rail, VoltageSensorSource source)
        {
            switch (source)
            {
                case VoltageSensorSource.SuperIo:
                    switch (rail)
                    {
                        case VoltageRail.Vsoc:
                            return _vsocSensor?.Value ?? 0;
                        case VoltageRail.Vddio:
                            return _apuVddioSensor?.Value ?? 0;
                        default:
                            return _vmiscSensor?.Value ?? 0;
                    }
                case VoltageSensorSource.Smu:
                    switch (rail)
                    {
                        case VoltageRail.Vsoc:
                            return PowerTable?.VDDCR_SOC ?? 0;
                        case VoltageRail.Vmisc:
                            return PowerTable?.VDD_MISC ?? 0;
                        default:
                            return 0;
                    }
                case VoltageSensorSource.Aod:
                    if (rail != VoltageRail.Vddio)
                        return 0;

                    var aodData = AodData;
                    return aodData?.ApuVddio == null ? 0 : aodData.ApuVddio.RawValue / 1000.0f;
                default:
                    return 0;
            }
        }

        // Reads the selected source, falling back to the other available ones. usedSource is the one
        // that produced the value, or null when none had a reading.
        private float ReadVoltage(VoltageRail rail, out VoltageSensorSource? usedSource)
        {
            usedSource = null;

            var availableSources = GetAvailableVoltageSources(rail);
            if (availableSources.Count == 0)
                return 0;

            var selectedSource = GetSelectedVoltageSource(rail);
            float value = ReadVoltageFrom(rail, selectedSource);
            if (value > 0)
            {
                usedSource = selectedSource;
                return value;
            }

            foreach (var source in availableSources)
            {
                if (source == selectedSource)
                    continue;

                value = ReadVoltageFrom(rail, source);
                if (value > 0)
                {
                    usedSource = source;
                    return value;
                }
            }

            return 0;
        }

        private static string VoltageLabel(string name, string plainLabel, VoltageSensorSource? source)
        {
            if (!source.HasValue)
                return plainLabel;

            switch (source.Value)
            {
                case VoltageSensorSource.SuperIo: return name + " (SIO)";
                case VoltageSensorSource.Smu: return name + " (SMU)";
                case VoltageSensorSource.Aod: return name + " (AOD)";
                default: return plainLabel;
            }
        }

        // Call after CpuSingleton.Instance.systemInfo.UpdateSensors() to refresh the live sensor readings.
        public void RefreshSensors()
        {
            // A mock window's sources are the ones the report rebuilds: its SuperIO dump, power table
            // (SMU) and AOD table. Their values are the captured ones, so refreshing them is harmless.
            if (!_sensorsDetected)
                DetectSensors();

            VoltageSensorSource? source;

            ApuVddio = ReadVoltage(VoltageRail.Vddio, out source);
            VddioLabel = VoltageLabel("VDDIO", "CPU VDDIO", source);

            Vsoc = ReadVoltage(VoltageRail.Vsoc, out source);
            VsocLabel = VoltageLabel("VSOC", "VSOC", source);

            Vmisc = ReadVoltage(VoltageRail.Vmisc, out source);
            VmiscLabel = VoltageLabel("MISC", "VDD MISC", source);
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

            tr.primary td {
                background: #fffbeb;
            }

            tr.primary td:first-child {
                background: #fef3c7;
                color: #b45309;
                font-weight: 700;
            }

            tr.secondary td:first-child {
                color: #475569;
            }

            tr.mismatch td {
                background: #fff1f2;
            }

            tr.mismatch td:first-child {
                background: #ffe4e6;
                color: #be123c;
                font-weight: 700;
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
                    html += $"<tr><td>{property.Name}</td><td>{cpu.systemInfo.SmuVersion}</td></tr>";
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

            var primaryTimings = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "CL", "RCDWR", "RCDRD", "RP", "RAS", "RC",
                "RRDS", "RRDL", "FAW",
                "CWL", "WR"
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
                bool isPrimary = primaryTimings.Contains(prop.Name);
                string rowClass = mismatch ? "mismatch" : isPrimary ? "primary" : "secondary";

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

        public string GetJSON(SnapshotSource source, SnapshotOptions options)
        {
            return SnapshotWriter.ToJson(SnapshotBuilder.Build(source, options));
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