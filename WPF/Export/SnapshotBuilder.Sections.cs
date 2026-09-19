using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ZenStates.Core;
using ZenStates.Core.Hardware;
using ZenStates.Core.Hardware.DRAM;
using ZenStates.Core.Hardware.DRAM.DDR5.Pmic;
using ZenStates.Core.Hardware.DRAM.DDR5.Spd;
using ZenStates.Core.Hardware.DRAM.DDR5.Thermal;
using ZenTimings.Settings;

namespace ZenTimings.Export
{
    public sealed partial class SnapshotBuilder
    {
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
    }
}
