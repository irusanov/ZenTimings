using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using ZenStates.Core;
using ZenStates.Core.Common;
using ZenStates.Core.Hardware;
using ZenStates.Core.Hardware.DRAM;
using ZenStates.Core.Hardware.DRAM.DDR5.Pmic;
using ZenStates.Core.Hardware.DRAM.DDR5.Spd;
using ZenStates.Core.Hardware.DRAM.DDR5.Thermal;
using ZenTimings.Helpers;
using ZenTimings.Settings;
using CpuSingleton = ZenTimings.Common.CpuSingleton;

namespace ZenTimings.Export
{
    /// <summary>
    /// Builds a snapshot tree from the values the application already holds in memory.
    /// Nothing here triggers a hardware read, the data is as fresh as the last auto refresh.
    /// </summary>
    public sealed class SnapshotBuilder
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

        private static readonly HashSet<string> SpdSkippedFields = new HashSet<string>
        {
            "RawSpd", "XmpProfiles", "ExpoProfile1", "ExpoProfile2", "ThermalData", "PmicData", "ModuleSerialNumber"
        };

        // Live PMIC values belong to readings.dimm, RawRegisters is a raw dump
        private static readonly HashSet<string> PmicSkippedFields = new HashSet<string>
        {
            "RawRegisters", "CurrentLimitRaw", "VinBulkMv", "SwaAdcMv", "SwbAdcMv", "SwcAdcMv", "Vout18AdcMv", "Vout10AdcMv",
            "SwaTelemetryRaw", "SwbTelemetryRaw", "SwcTelemetryRaw", "SwaW", "SwbW", "SwcW", "TotalW",
            "PmicTemperature", "VinBulkOverVoltage", "SwaPowerGoodFault", "SwbPowerGoodFault", "SwcPowerGoodFault",
            "HighTemperatureWarning", "CriticalTemperatureShutdown", "PecError", "ParityError",

            // The state of the ADC input selector, it changes with every telemetry read
            "AdcEnabled", "AdcSelectedInput",

            // Bus addresses as decimal numbers, the hub address is already there as i2c_address_hex
            "I2cAddress", "SpdHubAddress"
        };

        private object BuildSpd(Cpu cpu)
        {
            MemoryConfig mc = cpu.GetMemoryConfig();
            var entries = GetSpdEntries(cpu);
            if (entries.Count == 0)
            {
                Missing("static.spd", "not_present", "SPD data is only read for DDR5 modules");
                return null;
            }

            var result = new List<object>();
            for (int i = 0; i < entries.Count; i++)
            {
                Ddr5SpdInfo info = entries[i].Value;
                string path = $"static.spd[{i}]";
                var module = i < mc.Modules.Count ? mc.Modules[i] : null;

                var item = new SnapshotObject()
                    .Add("module_index", i)
                    .Add("slot", Text(module?.Slot))
                    .Add("i2c_address_hex", entries[i].Key.ToString("X2"));

                SnapshotObject fields = ReflectObject(info, 0, SpdSkippedFields);
                if (info.IsPartial)
                    fields = WithoutDefaults(fields);
                AddSerial(fields, "ModuleSerialNumber", info.ModuleSerialNumber, $"{path}.info.ModuleSerialNumber");
                item.Add("info", fields);

                if (info.IsPartial)
                    Missing(path, "partial", "info only lists the fields read at startup and the XMP / EXPO profiles are not loaded, open Tools > Advanced Info > SPD to load the full SPD");

                var xmp = new List<object>();
                if (info.XmpProfiles != null)
                {
                    foreach (var profile in info.XmpProfiles)
                    {
                        if (profile != null && profile.IsValid)
                            xmp.Add(ReflectObject(profile, 0, null));
                    }
                }
                item.Add("xmp_profiles", info.IsPartial ? null : (object)xmp);

                var expo = new List<object>();
                foreach (var profile in new[] { info.ExpoProfile1, info.ExpoProfile2 })
                {
                    if (profile != null && profile.IsValid)
                        expo.Add(ReflectObject(profile, 0, null));
                }
                item.Add("expo_profiles", info.IsPartial ? null : (object)expo);

                Ddr5PmicData pmic = info.PmicData;
                item.Add("pmic", pmic != null && pmic.IsValid ? ReflectObject(pmic, 0, PmicSkippedFields) : null);

                Ddr5ThermalData thermal = info.ThermalData;
                if (thermal != null && thermal.IsValid)
                {
                    item.Add("thermal_sensor", new SnapshotObject()
                        .Add("supported", thermal.TempSensorSupported)
                        .Add("enabled", thermal.TempSensorEnabled)
                        .Add("limit_high_c", thermal.TempMaxMilliC / 1000.0)
                        .Add("limit_low_c", thermal.TempMinMilliC / 1000.0)
                        .Add("limit_crit_high_c", thermal.TempCritMilliC / 1000.0)
                        .Add("limit_crit_low_c", thermal.TempLCritMilliC / 1000.0));
                }
                else
                {
                    item.Add("thermal_sensor", null);
                }

                result.Add(item);
            }

            return result;
        }

        // Members of the DDR4 / DDR5 classes which are regular timings. Everything else those classes declare
        // is a raw memory controller register field.
        private static readonly HashSet<string> DerivedTimingNames = new HashSet<string> { "RFCsb", "RFC4", "RFCns", "Nitro" };

        private static bool IsRegister(Member member)
        {
            return member.DeclaringType != typeof(BaseDramTimings) && !DerivedTimingNames.Contains(member.Name);
        }

        private object BuildTimings(Cpu cpu)
        {
            var all = cpu.GetMemoryConfig()?.Timings;
            if (all == null || all.Count == 0)
            {
                Missing("config.timings", "read_failed", "no memory timings available");
                return null;
            }

            var unique = all.GroupBy(t => t.Key).Select(g => g.First()).ToList();

            // Frequency and the ns values fall back to a MMIO read when the power table has no MCLK
            bool hasMclk = cpu.powerTable != null && cpu.powerTable.MCLK > 0;
            if (!hasMclk)
                Missing("config.timings.per_dct[*].timings.Frequency", "not_supported", "power table has no MCLK, derived values are skipped to avoid a hardware read");

            // Every DCT has the same timings type, so the values of a member line up by index
            var perDct = new List<object>();
            var mismatch = new List<object>();
            Member[] members = GetMembers(unique[0].Value.GetType());
            var valueSets = unique.Select(u => new SnapshotObject()).ToList();
            var registerSets = unique.Select(u => new SnapshotObject()).ToList();
            bool withTimings = options.Has(SnapshotSections.Timings);
            bool withRegisters = options.Has(SnapshotSections.Registers);

            foreach (Member member in members)
            {
                if (!hasMclk && member.IsComputed)
                    continue;

                bool isRegister = IsRegister(member);
                if (isRegister ? !withRegisters : !withTimings)
                    continue;

                bool differs = false;
                string expected = null;
                for (int i = 0; i < unique.Count; i++)
                {
                    object value = ReadMember(member, unique[i].Value, 0);
                    (isRegister ? registerSets : valueSets)[i].Add(member.Name, value);

                    if (unique.Count == 1 || differs)
                        continue;

                    string text = SnapshotWriter.Scalar(value is SnapshotObject nested ? SnapshotWriter.ToJson(nested) : value);
                    if (i == 0)
                        expected = text;
                    else if (text != expected)
                        differs = true;
                }

                if (differs)
                    mismatch.Add(member.Name);
            }

            for (int i = 0; i < unique.Count; i++)
            {
                var channel = new SnapshotObject().Add("dct", unique[i].Key >> 20);
                if (withTimings)
                    channel.Add("timings", valueSets[i]);
                if (withRegisters)
                    channel.Add("registers", registerSets[i]);
                perDct.Add(channel);
            }

            return new SnapshotObject()
                .Add("note", "timing values are in memory clock cycles (tCK) unless the name ends with ns, Frequency is in MT/s")
                .Add("dct_mismatch", mismatch)
                .Add("per_dct", perDct);
        }

        private object BuildAod(Cpu cpu)
        {
            var data = cpu.info.aod?.Table?.Data;
            if (data == null)
            {
                Missing("config.aod", "not_supported", "AOD table is not available on this platform");
                return null;
            }

            // The table repeats the timings and leaves some of them at zero, the application does not show them either
            var timingCopies = new HashSet<string>(GetMembers(data.GetType()).Select(m => m.Name).Where(n => n.StartsWith("T", StringComparison.Ordinal)));
            return ReflectObject(data, 0, timingCopies);
        }

        private object BuildApob(Cpu cpu)
        {
            var apob = cpu.info.apob;
            if (apob == null || !apob.IsValid)
            {
                Missing("config.apob", "not_supported", Text(apob?.ErrorReason) ?? "APOB table is not available on this platform");
                return null;
            }

            return new SnapshotObject()
                .Add("main", apob.Data != null ? ReflectObject(apob.Data, 0, null) : null)
                .Add("extended", apob.ExtendedData != null ? ReflectObject(apob.ExtendedData, 0, null) : null)
                .Add("ccdl", new SnapshotObject()
                    .Add("tccd_l", apob.CcdlData.Tccdl)
                    .Add("tccd_l_wr", apob.CcdlData.Tccdlwr)
                    .Add("tccd_l_wr2", apob.CcdlData.Tccdlwr2));
        }

        private object BuildBiosController(Cpu cpu)
        {
            if (!source.BiosMemConfig.HasValue)
            {
                Missing("config.ddr4_bios_controller", "not_supported", "BIOS memory controller table is not available");
                return null;
            }

            return ReflectObject(source.BiosMemConfig.Value, 0, null);
        }

        private object BuildPowerTable(Cpu cpu)
        {
            PowerTable pt = cpu.powerTable;
            if (pt == null)
            {
                Missing("readings.power_table", "read_failed", "power table is not initialized");
                return null;
            }

            var result = new SnapshotObject();
            AddNonZero(result, "fclk_mhz", pt.FCLK);
            AddNonZero(result, "uclk_mhz", pt.UCLK);
            AddNonZero(result, "mclk_mhz", pt.MCLK);
            AddNonZero(result, "vddcr_soc_v", pt.VDDCR_SOC);
            AddNonZero(result, "cldo_vddp_v", pt.CLDO_VDDP);
            AddNonZero(result, "cldo_vddg_iod_v", pt.CLDO_VDDG_IOD);
            AddNonZero(result, "cldo_vddg_ccd_v", pt.CLDO_VDDG_CCD);
            AddNonZero(result, "vdd_misc_v", pt.VDD_MISC);
            return result;
        }

        // A zero means the offset is not defined for the current table version
        private void AddNonZero(SnapshotObject target, string key, float value)
        {
            if (value > 0)
            {
                target.Add(key, value);
                return;
            }

            target.Add(key, null);
            Missing($"readings.power_table.{key}", "not_supported", "not reported by the power table on this platform");
        }

        private object BuildSensors(Cpu cpu)
        {
            var hidden = new HashSet<string>(SensorSettings.Instance.HiddenSensors ?? new List<string>());
            var items = new List<object>();
            int hiddenCount = 0;

            foreach (var group in cpu.systemInfo.SensorGroups)
            {
                if (group.Sensors == null)
                    continue;

                foreach (Sensor sensor in group.Sensors)
                {
                    if (hidden.Count > 0 && hidden.Contains($"{group.ChipName}|{sensor.Name}"))
                    {
                        hiddenCount++;
                        continue;
                    }

                    items.Add(BuildSensor(group.ChipName, sensor));
                }
            }

            if (source.Plugins != null)
            {
                foreach (var plugin in source.Plugins)
                {
                    if (plugin?.Sensors == null)
                        continue;

                    foreach (Sensor sensor in plugin.Sensors)
                    {
                        if (sensor.Value.HasValue)
                            items.Add(BuildSensor(plugin.Name, sensor));
                    }
                }
            }

            if (items.Count == 0 && hiddenCount == 0)
                Missing("readings.sensors", "not_supported", "no supported sensor chip was detected");

            return new SnapshotObject()
                .Add("hidden_by_user", hiddenCount)
                .Add("items", items);
        }

        private SnapshotObject BuildSensor(string group, Sensor sensor)
        {
            return new SnapshotObject()
                .Add("group", Text(group))
                .Add("name", Text(sensor.Name))
                .Add("type", sensor.SensorType.ToString().ToLowerInvariant())
                .Add("unit", GetSensorUnit(sensor.SensorType))
                .Add("value", Round(sensor.Value, sensor.SensorType))
                .Add("min", Round(sensor.Min, sensor.SensorType))
                .Add("max", Round(sensor.Max, sensor.SensorType));
        }

        // Fan speeds are whole numbers in the sensors window as well
        private static object Round(float? value, SensorType sensorType)
        {
            if (!value.HasValue)
                return null;

            return sensorType == SensorType.Fan ? (float)Math.Round(value.Value) : value.Value;
        }

        private static string GetSensorUnit(SensorType sensorType)
        {
            switch (sensorType)
            {
                case SensorType.Voltage: return "V";
                case SensorType.Current: return "A";
                case SensorType.Power: return "W";
                case SensorType.Clock: return "MHz";
                case SensorType.Temperature: return "C";
                case SensorType.Load: return "%";
                case SensorType.Frequency: return "Hz";
                case SensorType.Fan: return "RPM";
                case SensorType.Flow: return "L/h";
                case SensorType.Control: return "%";
                case SensorType.Level: return "%";
                case SensorType.Data: return "GB";
                case SensorType.SmallData: return "MB";
                case SensorType.Throughput: return "B/s";
                case SensorType.TimeSpan: return "s";
                case SensorType.Timing: return "ns";
                case SensorType.Energy: return "mWh";
                case SensorType.Noise: return "dBA";
                case SensorType.Conductivity: return "uS/cm";
                case SensorType.Humidity: return "%";
                default: return null;
            }
        }

        private object BuildDimmTelemetry(Cpu cpu)
        {
            MemoryConfig mc = cpu.GetMemoryConfig();
            var entries = GetSpdEntries(cpu);
            if (entries.Count == 0)
            {
                Missing("readings.dimm", "not_present", "DIMM telemetry is only available for DDR5 modules");
                return null;
            }

            var result = new List<object>();
            for (int i = 0; i < entries.Count; i++)
            {
                Ddr5PmicData pmic = entries[i].Value.PmicData;
                Ddr5ThermalData thermal = entries[i].Value.ThermalData;
                var module = i < mc.Modules.Count ? mc.Modules[i] : null;
                string path = $"readings.dimm[{i}]";

                var item = new SnapshotObject()
                    .Add("module_index", i)
                    .Add("slot", Text(module?.Slot));

                if (pmic != null && pmic.IsValid)
                {
                    // Most PMICs only report a range such as "< 85 C" instead of a value
                    object temperature = ParseTemperature(pmic.PmicTemperature);

                    item.Add("vdd_mv", pmic.SwaAdcMv)
                        .Add("vddq_mv", pmic.SwbAdcMv)
                        .Add("vpp_mv", pmic.SwcAdcMv)
                        .Add("vin_bulk_mv", pmic.VinBulkMv)
                        .Add("vout_1v8_mv", pmic.Vout18AdcMv)
                        .Add("vout_1v0_mv", pmic.Vout10AdcMv)
                        .Add("pmic_temp_c", temperature is double ? temperature : null)
                        .Add("pmic_temp_range", temperature as string)
                        .Add("total_power_w", pmic.TotalW)
                        .Add("pmic_high_temp", pmic.HighTemperatureWarning)
                        .Add("pmic_critical_shutdown", pmic.CriticalTemperatureShutdown)
                        .Add("pmic_vin_over_voltage", pmic.VinBulkOverVoltage)
                        .Add("pmic_power_good_fault", pmic.SwaPowerGoodFault || pmic.SwbPowerGoodFault || pmic.SwcPowerGoodFault);
                }
                else
                {
                    Missing($"{path}.pmic", "not_supported", "PMIC is not readable on this module");
                }

                if (thermal != null && thermal.IsValid && thermal.TempSensorEnabled)
                {
                    item.Add("spd_hub_temp_c", thermal.TemperatureC)
                        .Add("spd_hub_alarm_high", thermal.AlarmHigh)
                        .Add("spd_hub_alarm_crit_high", thermal.AlarmCritHigh);
                }
                else
                {
                    item.Add("spd_hub_temp_c", null);
                    Missing($"{path}.spd_hub_temp_c", "not_supported", "SPD hub temperature sensor is absent or disabled");
                }

                result.Add(item);
            }

            return result;
        }

        private static object ParseTemperature(string value)
        {
            if (string.IsNullOrEmpty(value))
                return null;

            string number = value.Replace("°C", "").Replace(',', '.').Trim();
            if (double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out double result))
                return result;

            return Text(value);
        }

        private object BuildAsusWmi()
        {
            if (source.AsusSensors == null || source.AsusSensors.Count == 0)
            {
                Missing("readings.asus_wmi", "not_present", "this system has no ASUS sensor interface (WMI class ASUSHW), it only exists on some older ASUS boards");
                return null;
            }

            var result = new List<object>();
            foreach (var sensor in source.AsusSensors.ToList())
            {
                result.Add(new SnapshotObject()
                    .Add("name", Text(sensor.Name))
                    .Add("type", sensor.Type.ToString())
                    .Add("value", Text(sensor.Value)));
            }

            return result;
        }

        private const int MaxTextLength = 128;
        private const int MaxDepth = 4;

        private sealed class Member
        {
            public string Name;
            public Type DeclaringType;
            public Func<object, object> Get;

            // A property without a setter, its value is calculated on every read
            public bool IsComputed;
        }

        // Snapshots are built both from the UI thread and from the auto refresh task
        private static readonly ConcurrentDictionary<Type, Member[]> MemberCache = new ConcurrentDictionary<Type, Member[]>();

        // Strings coming from SPD and SMBIOS are not trusted: they can be rewritten by the user or the vendor
        internal static string Text(string value)
        {
            if (string.IsNullOrEmpty(value))
                return null;

            var sb = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                if (!char.IsControl(c) && !char.IsSurrogate(c))
                    sb.Append(c);
            }

            string result = sb.ToString().Trim();
            if (result.Length > MaxTextLength)
                result = result.Substring(0, MaxTextLength);

            if (result.Length == 0 || result == "N/A")
                return null;

            return result;
        }

        private static Member[] GetMembers(Type type)
        {
            return MemberCache.GetOrAdd(type, CreateMembers);
        }

        private static Member[] CreateMembers(Type type)
        {
            var result = new List<Member>();
            var names = new HashSet<string>();

            // A property hidden with "new" is returned twice, the most derived one comes first
            foreach (PropertyInfo prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!prop.CanRead || prop.GetIndexParameters().Length > 0 || prop.PropertyType == typeof(byte[]))
                    continue;
                if (!names.Add(prop.Name))
                    continue;

                PropertyInfo current = prop;
                result.Add(new Member
                {
                    Name = current.Name,
                    DeclaringType = current.DeclaringType,
                    Get = obj => current.GetValue(obj, null),
                    IsComputed = current.GetSetMethod(true) == null,
                });
            }

            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (field.FieldType == typeof(byte[]))
                    continue;

                FieldInfo current = field;
                result.Add(new Member { Name = current.Name, DeclaringType = current.DeclaringType, Get = current.GetValue });
            }

            return result.ToArray();
        }

        private SnapshotObject ReflectObject(object obj, int depth, ICollection<string> skip)
        {
            var result = new SnapshotObject();
            foreach (Member member in GetMembers(obj.GetType()))
            {
                if (skip != null && skip.Contains(member.Name))
                    continue;

                result.Add(member.Name, ReadMember(member, obj, depth + 1));
            }

            return result;
        }

        // The fields of a partially read object which were not read hold defaults that look like real values
        private static SnapshotObject WithoutDefaults(SnapshotObject source)
        {
            var result = new SnapshotObject();
            foreach (var item in source.Items)
            {
                object value = item.Value;
                bool isDefault = value == null
                    || (value is bool flag && !flag)
                    || (value is List<object> list && list.Count == 0)
                    || (value is IConvertible number && !(value is string) && !(value is bool)
                        && Convert.ToDouble(number, CultureInfo.InvariantCulture) == 0);

                if (!isDefault)
                    result.Add(item.Key, value);
            }

            return result;
        }

        private object ReadMember(Member member, object obj, int depth)
        {
            try
            {
                return ToValue(member.Get(obj), depth);
            }
            catch
            {
                return null;
            }
        }

        private object ToValue(object value, int depth)
        {
            if (value == null)
                return null;

            if (value is string text)
                return Text(text);

            if (value is bool || value is byte || value is sbyte || value is short || value is ushort
                || value is int || value is uint || value is long || value is ulong
                || value is float || value is double || value is decimal)
                return value;

            if (value is Enum)
                return value.ToString();

            if (value is DateTime date)
                return date.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);

            if (value is BooleanProp flag)
            {
                uint raw = flag;
                return raw > 1 ? (object)null : raw == 1;
            }

            if (value is CommandRateProp)
                return Text(value.ToString());

            if (value is BankRefreshMode refreshMode)
                return refreshMode.Name;

            if (value is EncodedValueBase encoded)
            {
                if (encoded.IsNull)
                    return null;

                return new SnapshotObject()
                    .Add("raw", encoded.RawValue)
                    .Add("text", Text(encoded.ToString()));
            }

            // A zero means the table has no value for it
            if (value is Voltage voltage)
                return voltage.RawValue == 0 ? null : new SnapshotObject().Add("mv", voltage.RawValue);

            if (value is Capacity capacity)
                return capacity.SizeInBytes;

            if (value is byte[])
                return null;

            Type type = value.GetType();
            if (type.FullName == "ZenStates.Core.Hardware.DRAM.Ddr5Timings+NitroSettings")
                return Text(value.ToString());

            if (value is IEnumerable enumerable)
            {
                var list = new List<object>();
                foreach (object item in enumerable)
                    list.Add(ToValue(item, depth + 1));
                return list;
            }

            string ns = value.GetType().Namespace ?? string.Empty;
            if (depth < MaxDepth && (ns.StartsWith("ZenStates.", StringComparison.Ordinal) || ns.StartsWith("ZenTimings", StringComparison.Ordinal)))
                return ReflectObject(value, depth, null);

            return Text(value.ToString());
        }

        // A reader that was not written for this file, such as an AI assistant, cannot tell a regular timing from
        // a raw register field or a measured voltage from a BIOS set point. Only the exported sections are explained.
        private SnapshotObject BuildLegend()
        {
            var legend = new SnapshotObject()
                .Add("about", "Snapshot of an AMD Ryzen system made by ZenTimings. It shows what is applied right now, which can differ from what was entered in BIOS.")
                .Add("structure", "static = hardware that does not change while the system runs, config = memory configuration applied at boot, readings = live values sampled at readings.sampled_at.")
                .Add("null", "null means the value is not available or does not exist on this platform. When the reason is known it is listed under unavailable with the path.");

            bool ddr4 = memoryType == MemType.DDR4 || memoryType == MemType.LPDDR4;
            bool timings = options.Has(SnapshotSections.Timings);
            bool registers = options.Has(SnapshotSections.Registers);

            if (timings || registers)
                legend.Add("dct", "dct is the index of a memory channel (DRAM controller), per_dct has one entry per populated channel. Names in dct_mismatch differ between channels, the main ZenTimings window shows one channel at a time.");

            if (timings)
            {
                legend.Add("timings", "Read back from the memory controller. Names are the usual AMD ones without the leading t (CL = tCL, RCDRD = tRCDRD, RDRDSCL = tRDRDSCL). The unit is memory clock cycles, except RFCns and REFIns (nanoseconds), Frequency (MT/s) and Ratio (memory clock multiplier).");

                // The refresh timings and the way the active one is chosen differ between the generations
                legend.Add("refresh", ddr4
                    ? "Only one refresh timing is in use: RFC when RefreshMode is NORMAL, otherwise RFC2 or RFC4, FGR says which one (2 or 4). RFCns is the active one in nanoseconds."
                    : "Only one refresh timing is in use: RFC when RefreshMode is NORMAL, otherwise RFC2, together with RFCsb when it is MIXED. RFCns is the active one in nanoseconds (active timing = RFCns x MCLK in GHz), the inactive ones keep their BIOS values.");

                legend.Add("not_tunable", "PHYRDL, PHYWRL and PHYWRD are results of memory training, a PHYRDL difference between channels is common. SD and DD variants (RDRDSD, RDRDDD, WRWRSD, WRWRDD) only matter with two ranks or two DIMMs per channel.");

                if (!ddr4)
                    legend.Add("nitro", "Nitro is the DDR5 Nitro mode: RxData, TxData and CtrlLine set the timing between the memory controller and the PHY.");
            }

            if (registers)
                legend.Add("registers", "registers holds raw bit fields of the memory controller that have no public documentation. Do not guess their meaning and do not base tuning advice on them.");

            if (options.Has(SnapshotSections.Aod) || options.Has(SnapshotSections.Apob))
                legend.Add("aod_apob", "aod is the ACPI table BIOS publishes with its memory settings, apob is the block the platform firmware (PSP) writes during memory training. Both hold resistances, drive strengths and voltages and they can disagree, ZenTimings shows apob by default. A value is {raw, text}: text is what BIOS shows (ohms or RZQ/x), raw is the register code. {mv} is a voltage set point in millivolts. In apob, main is what ZenTimings shows, extended is a second copy that is only used where main has no value.");

            if (options.Has(SnapshotSections.BiosController))
                legend.Add("ddr4_bios_controller", "BIOS table with the DDR4 termination, drive strength and setup settings as raw register codes. MemVddio, MemVtt and MemVpp are voltage set points in millivolts.");

            if (options.Has(SnapshotSections.PowerTable))
                legend.Add("power_table", "From the SMU power table. mclk = memory clock (MT/s divided by 2), uclk = unified memory controller clock, fclk = Infinity Fabric clock; mclk equal to uclk is the 1:1 mode. vddcr_soc, cldo_vddp, cldo_vddg and vdd_misc are CPU rails.");

            if (options.Has(SnapshotSections.Sensors))
                legend.Add("sensors", "Read from the sensor chip of the motherboard. Names like Voltage #6 are inputs the application has no label for, what they measure is unknown. A null value means nothing is connected or the reading is invalid. min and max are counted since the application started.");

            if (options.Has(SnapshotSections.DimmTelemetry))
                legend.Add("dimm", "Measured on the module by its PMIC and SPD hub: vdd, vddq and vpp are actual voltages, unlike the set points in aod.");

            if (options.Has(SnapshotSections.Spd))
                legend.Add("spd", "What the module reports about itself: JEDEC, XMP and EXPO profiles are what it is rated for, not what is applied. Times ending with Ps are picoseconds, with Ns nanoseconds. When IsPartial is true only the fields read at startup are listed, the SPD window in ZenTimings loads the rest. In pmic the set points are decoded two ways, ...Mv (JEDEC 7-bit) and ...Mv8bit (vendor extension used in HighVoltageMode), compare them with the measured voltages in readings.dimm to see which one applies.");

            return legend;
        }

        /// <summary>
        /// Sections the running system has no data for, with the reason. Used to disable them in the export dialog.
        /// Like the snapshot itself, this only looks at what the application already holds in memory.
        /// </summary>
        public static Dictionary<SnapshotSections, string> GetUnavailableSections(SnapshotSource source)
        {
            var result = new Dictionary<SnapshotSections, string>();
            source = source ?? new SnapshotSource();

            Cpu cpu = CpuSingleton.Instance;
            MemoryConfig memory = cpu.GetMemoryConfig();
            MemType type = memory?.Type ?? MemType.UNKNOWN;

            foreach (SnapshotSections section in SingleSections)
            {
                if (!AppliesTo(section, type))
                {
                    result[section] = $"ZenTimings has no such data with {type} memory";
                    continue;
                }

                try
                {
                    string reason = GetMissingReason(section, cpu, memory, source);
                    if (reason != null)
                        result[section] = reason;
                }
                catch (Exception ex)
                {
                    result[section] = ex.Message;
                }
            }

            return result;
        }

        private static string GetMissingReason(SnapshotSections section, Cpu cpu, MemoryConfig memory, SnapshotSource source)
        {
            switch (section)
            {
                case SnapshotSections.Timings:
                    return memory?.Timings == null || memory.Timings.Count == 0 ? "No memory timings were read" : null;

                case SnapshotSections.Registers:
                    if (memory?.Timings == null || memory.Timings.Count == 0)
                        return "No memory timings were read";
                    return GetMembers(memory.Timings[0].Value.GetType()).Any(IsRegister) ? null : "No register fields are read for this memory type";

                case SnapshotSections.Spd:
                    return GetSpdEntries(cpu).Count == 0 ? "No SPD data was read from the modules" : null;

                case SnapshotSections.DimmTelemetry:
                    return GetSpdEntries(cpu).Any(e => (e.Value.PmicData != null && e.Value.PmicData.IsValid)
                        || (e.Value.ThermalData != null && e.Value.ThermalData.IsValid))
                        ? null
                        : "The modules report no PMIC or temperature data";

                case SnapshotSections.Aod:
                    return cpu.info.aod?.Table?.Data == null ? "The AOD table was not found on this system" : null;

                case SnapshotSections.Apob:
                    if (cpu.info.apob != null && cpu.info.apob.IsValid)
                        return null;
                    return Text(cpu.info.apob?.ErrorReason) ?? "The APOB table was not found on this system";

                case SnapshotSections.BiosController:
                    return source.BiosMemConfig.HasValue ? null : "The BIOS memory controller table is not available";

                case SnapshotSections.PowerTable:
                    return cpu.powerTable == null ? "The power table is not initialized" : null;

                case SnapshotSections.Sensors:
                    bool hasChip = cpu.systemInfo.SensorGroups.Any(g => g.Sensors != null && g.Sensors.Any());
                    bool hasPlugin = source.Plugins != null && source.Plugins.Any(p => p?.Sensors != null && p.Sensors.Any(s => s.Value.HasValue));
                    return hasChip || hasPlugin ? null : "No supported sensor chip was detected on this board";

                case SnapshotSections.AsusWmi:
                    return source.AsusSensors == null || source.AsusSensors.Count == 0 ? "This system has no ASUS sensor interface (WMI class ASUSHW), it only exists on some older ASUS boards" : null;

                default:
                    return null;
            }
        }
    }
}
