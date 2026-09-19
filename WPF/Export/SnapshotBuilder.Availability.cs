using System;
using System.Collections.Generic;
using System.Linq;
using ZenStates.Core;
using ZenStates.Core.Hardware.DRAM;
using CpuSingleton = ZenTimings.Common.CpuSingleton;

namespace ZenTimings.Export
{
    public sealed partial class SnapshotBuilder
    {
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
