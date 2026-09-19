using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ZenStates.Core;
using ZenStates.Core.Hardware;
using ZenStates.Core.Hardware.DRAM;
using ZenStates.Core.Hardware.DRAM.DDR5.Spd;
using ZenTimings.Helpers;
using ZenTimings.Settings;
using CpuSingleton = ZenTimings.Common.CpuSingleton;

namespace ZenTimings.Export
{
    /// <summary>
    /// Builds a snapshot tree from the values the application already holds in memory.
    /// Nothing here triggers a hardware read, the data is as fresh as the last auto refresh.
    /// </summary>
    public sealed partial class SnapshotBuilder
    {
        public const string SchemaName = "zentimings.snapshot";
        public const string SchemaVersion = "1.0";

        private static string osName;

        private readonly SnapshotSource source;
        private readonly SnapshotOptions options;
        private readonly List<object> unavailable = new List<object>();
        private readonly List<object> skippedSections = new List<object>();
        private MemType memoryType = MemType.UNKNOWN;

        private SnapshotBuilder(SnapshotSource source, SnapshotOptions options)
        {
            this.source = source ?? new SnapshotSource();

            // A private copy, the sections are narrowed down to the ones that apply to the installed memory
            options = options ?? new SnapshotOptions();
            this.options = new SnapshotOptions
            {
                Sections = options.Sections,
                IncludeSerialNumbers = options.IncludeSerialNumbers,
                IncludeLegend = options.IncludeLegend,
            };
        }

        /// <summary>
        /// The application has a data source for these sections with one memory generation only:
        /// SPD, PMIC and DIMM sensors are read for DDR5 / LPDDR5, the register fields exist in Ddr5Timings only,
        /// the AOD and APOB layouts in the core describe DDR5 platforms, and the BIOS memory controller table
        /// is requested for DDR4 / LPDDR4 only.
        /// </summary>
        public static bool AppliesTo(SnapshotSections section, MemType type)
        {
            if (type == MemType.UNKNOWN)
                return true;

            bool ddr5 = type == MemType.DDR5 || type == MemType.LPDDR5;
            switch (section)
            {
                case SnapshotSections.Spd:
                case SnapshotSections.Registers:
                case SnapshotSections.Aod:
                case SnapshotSections.Apob:
                case SnapshotSections.DimmTelemetry:
                    return ddr5;
                case SnapshotSections.BiosController:
                    return !ddr5;
                default:
                    return true;
            }
        }

        public static SnapshotObject Build(SnapshotSource source, SnapshotOptions options)
        {
            return new SnapshotBuilder(source, options).Build();
        }

        private SnapshotObject Build()
        {
            Cpu cpu = CpuSingleton.Instance;
            var root = new SnapshotObject();

            memoryType = cpu.GetMemoryConfig()?.Type ?? MemType.UNKNOWN;
            foreach (SnapshotSections section in SingleSections)
            {
                if (options.Has(section) && !AppliesTo(section, memoryType))
                {
                    options.Sections &= ~section;
                    skippedSections.Add(section.ToString());
                }
            }

            root.Add("schema", SchemaName);
            root.Add("schema_version", SchemaVersion);
            root.Add("generated_at", FormatUtc(DateTime.UtcNow));
            root.Add("app", BuildApp(cpu));
            root.Add("export", BuildExportInfo());
            if (options.IncludeLegend)
                root.Add("legend", BuildLegend());

            SnapshotObject staticData = GetStaticData(cpu);
            if (staticData.Count > 0)
                root.Add("static", staticData);

            var config = new SnapshotObject();
            if (options.Has(SnapshotSections.Timings) || options.Has(SnapshotSections.Registers))
                AddSection(config, "config", "timings", () => BuildTimings(cpu));
            if (options.Has(SnapshotSections.Aod))
                AddSection(config, "config", "aod", () => BuildAod(cpu));
            if (options.Has(SnapshotSections.Apob))
                AddSection(config, "config", "apob", () => BuildApob(cpu));
            if (options.Has(SnapshotSections.BiosController))
                AddSection(config, "config", "ddr4_bios_controller", () => BuildBiosController(cpu));
            if (config.Count > 0)
                root.Add("config", config);

            var readings = new SnapshotObject();
            readings.Add("sampled_at", source.LastRefreshUtc.HasValue ? FormatUtc(source.LastRefreshUtc.Value) : null);
            readings.Add("stale", !source.AutoRefreshActive);
            int readingsHeader = readings.Count;

            if (options.Has(SnapshotSections.PowerTable))
                AddSection(readings, "readings", "power_table", () => BuildPowerTable(cpu));
            if (options.Has(SnapshotSections.Sensors))
                AddSection(readings, "readings", "sensors", () => BuildSensors(cpu));
            if (options.Has(SnapshotSections.DimmTelemetry))
                AddSection(readings, "readings", "dimm", () => BuildDimmTelemetry(cpu));
            if (options.Has(SnapshotSections.AsusWmi))
                AddSection(readings, "readings", "asus_wmi", () => BuildAsusWmi());

            if (readings.Count > readingsHeader)
            {
                root.Add("readings", readings);
                if (!source.AutoRefreshActive)
                    Missing("readings", "stale", "auto refresh is off, values are from the last refresh");
            }

            root.Add("unavailable", unavailable);
            return root;
        }

        private sealed class StaticCache
        {
            public string Key;
            public SnapshotObject System;
            public SnapshotObject Memory;
            public List<object> Unavailable;
        }

        private static volatile StaticCache staticCache;

        // System and module info is read once at startup and never changes, so the built nodes are reused.
        // SPD is not cached: the SPD window and the telemetry refresh update those objects in place.
        private SnapshotObject GetStaticData(Cpu cpu)
        {
            var staticData = new SnapshotObject();
            bool system = options.Has(SnapshotSections.System);
            bool memory = options.Has(SnapshotSections.Modules);

            if (system || memory)
            {
                string key = $"{system}|{memory}|{options.IncludeSerialNumbers}|{cpu.systemInfo.AgesaVersion}";
                StaticCache cache = staticCache;
                if (cache == null || cache.Key != key)
                {
                    int start = unavailable.Count;
                    var built = new SnapshotObject();
                    if (system)
                        AddSection(built, "static", "system", () => BuildSystem(cpu));
                    if (memory)
                        AddSection(built, "static", "memory", () => BuildMemory(cpu));

                    built.TryGet("system", out object systemNode);
                    built.TryGet("memory", out object memoryNode);
                    cache = new StaticCache
                    {
                        Key = key,
                        System = systemNode as SnapshotObject,
                        Memory = memoryNode as SnapshotObject,
                        Unavailable = unavailable.GetRange(start, unavailable.Count - start),
                    };
                    unavailable.RemoveRange(start, unavailable.Count - start);

                    // A failed section is built again next time
                    if ((!system || cache.System != null) && (!memory || cache.Memory != null))
                        staticCache = cache;
                }

                if (system)
                    staticData.Add("system", cache.System);
                if (memory)
                    staticData.Add("memory", cache.Memory);
                unavailable.AddRange(cache.Unavailable);
            }

            if (options.Has(SnapshotSections.Spd))
                AddSection(staticData, "static", "spd", () => BuildSpd(cpu));

            return staticData;
        }

        private void AddSection(SnapshotObject parent, string parentPath, string key, Func<object> build)
        {
            object value = null;
            try
            {
                value = build();
            }
            catch (Exception ex)
            {
                Missing($"{parentPath}.{key}", "read_failed", ex.Message);
            }

            parent.Add(key, value);
        }

        private void Missing(string path, string reason, string detail)
        {
            unavailable.Add(new SnapshotObject()
                .Add("path", path)
                .Add("reason", reason)
                .Add("detail", detail));
        }

        private static string FormatUtc(DateTime value)
        {
            return value.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        }

        private SnapshotObject BuildApp(Cpu cpu)
        {
            AppSettings settings = AppSettings.Instance;
            return new SnapshotObject()
                .Add("name", System.Windows.Forms.Application.ProductName)
                .Add("version", System.Windows.Forms.Application.ProductVersion)
                .Add("core_version", cpu.Version?.ToString())
                .Add("pawnio_version", Text($"{DriverHelper.Version}"))
                .Add("advanced_mode", settings.AdvancedMode)
                .Add("auto_refresh", source.AutoRefreshActive)
                .Add("auto_refresh_interval_ms", settings.AutoRefreshInterval);
        }

        private SnapshotObject BuildExportInfo()
        {
            var sections = new List<object>();
            foreach (SnapshotSections section in SingleSections)
            {
                if (options.Has(section))
                    sections.Add(section.ToString());
            }

            var info = new SnapshotObject().Add("sections", sections);
            if (skippedSections.Count > 0)
                info.Add("not_applicable_to_" + memoryType.ToString().ToLowerInvariant(), skippedSections);

            return info
                .Add("serial_numbers", options.IncludeSerialNumbers ? "included" : "omitted")
                .Add("raw_dumps", "omitted");
        }

        public static IEnumerable<SnapshotSections> SingleSections
        {
            get
            {
                foreach (SnapshotSections section in Enum.GetValues(typeof(SnapshotSections)))
                {
                    int value = (int)section;
                    if (value != 0 && (value & (value - 1)) == 0)
                        yield return section;
                }
            }
        }

        private SnapshotObject BuildSystem(Cpu cpu)
        {
            SystemInfo si = cpu.systemInfo;
            var smbios = SystemInfo.SMBios;

            if (osName == null)
                osName = new Microsoft.VisualBasic.Devices.ComputerInfo().OSFullName ?? string.Empty;

            var cpuInfo = new SnapshotObject()
                .Add("name", Text(si.CpuName))
                .Add("vendor", Text(si.Vendor))
                .Add("code_name", Text(si.CodeName))
                .Add("cpuid_hex", ((uint)si.CpuId).ToString("X8"))
                .Add("family", si.Family.ToString())
                .Add("base_model", si.BaseModel)
                .Add("extended_model", si.ExtendedModel)
                .Add("model", si.Model)
                .Add("stepping", si.Stepping)
                .Add("patch_level_hex", si.PatchLevel.ToString("X8"))
                .Add("package_type", si.PackageType)
                .Add("fused_core_count", si.FusedCoreCount)
                .Add("physical_core_count", si.PhysicalCoreCount)
                .Add("threads", si.Threads)
                .Add("smt", si.SMT)
                .Add("nodes_per_processor", si.NodesPerProcessor)
                .Add("ccd_count", si.CCDCount)
                .Add("ccx_count", si.CCXCount)
                .Add("cores_per_ccx", si.CoresPerCCX);

            var board = new SnapshotObject()
                .Add("vendor", Text(si.MbVendor))
                .Add("name", Text(si.MbName))
                .Add("version", Text(smbios?.Board?.Version));

            // The board itself has no date in SMBIOS, the BIOS build date is the closest thing
            var bios = new SnapshotObject()
                .Add("vendor", Text(smbios?.Bios?.Vendor))
                .Add("version", Text(si.BiosVersion))
                .Add("date", smbios?.Bios?.Date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                .Add("agesa_version", Text(si.AgesaVersion));

            var smu = new SnapshotObject()
                .Add("version", si.SmuVersion.ToString())
                .Add("table_version_hex", si.SmuTableVersion.ToString("X8"))
                .Add("type", Text(si.SmuType));

            return new SnapshotObject()
                .Add("os", new SnapshotObject().Add("name", Text(osName)))
                .Add("cpu", cpuInfo)
                .Add("board", board)
                .Add("bios", bios)
                .Add("smu", smu);
        }

        private SnapshotObject BuildMemory(Cpu cpu)
        {
            MemoryConfig mc = cpu.GetMemoryConfig();
            var smbios = SystemInfo.SMBios;
            var devices = smbios?.MemoryDevices?.Where(d => d.Size > 0).ToList();
            var modules = new List<object>();

            for (int i = 0; i < mc.Modules.Count; i++)
            {
                var module = mc.Modules[i];
                var device = devices != null && i < devices.Count ? devices[i] : null;
                var item = new SnapshotObject()
                    .Add("index", i)
                    .Add("slot", Text(module.Slot))
                    .Add("bank_label", Text(module.BankLabel))
                    .Add("device_locator", Text(module.DeviceLocator))
                    .Add("dct", module.DctOffset >> 20)
                    .Add("manufacturer", Text(module.Manufacturer))
                    .Add("part_number", Text(module.PartNumber))
                    .Add("capacity_bytes", module.Capacity?.SizeInBytes)
                    .Add("rank", module.Rank.ToString())
                    .Add("rated_speed_mts", module.ClockSpeed)
                    .Add("configured_speed_mts", device?.ConfiguredSpeed)
                    .Add("address_columns", module.AddressConfig.NumCol)
                    .Add("address_rows", module.AddressConfig.NumRow)
                    .Add("address_config", Text(module.AddressConfig.ToString()));

                AddSerial(item, "serial_number", device?.SerialNumber, $"static.memory.modules[{i}].serial_number");
                modules.Add(item);
            }

            return new SnapshotObject()
                .Add("type", mc.Type.ToString())
                .Add("total_capacity_bytes", mc.TotalCapacity?.SizeInBytes)
                .Add("ecc", smbios?.MemoryDevices?.Any(d => d.HasEcc))
                .Add("modules", modules);
        }

        private void AddSerial(SnapshotObject target, string key, string serial, string path)
        {
            if (options.IncludeSerialNumbers)
            {
                target.Add(key, Text(serial));
                return;
            }

            target.Add(key, null);
            Missing(path, "redacted", "serial numbers are not exported unless explicitly enabled");
        }

        private static List<KeyValuePair<byte, Ddr5SpdInfo>> GetSpdEntries(Cpu cpu)
        {
            var spd = cpu.GetMemoryConfig()?.SpdInfo;
            if (spd == null)
                return new List<KeyValuePair<byte, Ddr5SpdInfo>>();

            // The SPD window may replace entries while a snapshot is being built
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    return spd.Where(e => e.Value != null).ToList();
                }
                catch (InvalidOperationException)
                {
                    if (attempt > 0)
                        throw;
                }
            }
        }
    }
}
