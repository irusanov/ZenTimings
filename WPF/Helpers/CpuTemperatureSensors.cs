using System.Collections.Generic;
using ZenStates.Core.Hardware;
using ZenStates.Core.Hardware.Motherboard.Lpc;

namespace ZenTimings.Helpers
{
    /// <summary>
    /// CPU temperatures the core doesn't list as sensors: Tctl/Tdie, and the I/O die on the power table
    /// versions with a known layout. The main window updates them on each refresh, the Sensors window
    /// shows them as one more sensor group.
    /// </summary>
    internal sealed class CpuTemperatureSensors
    {
        public const string GroupName = "CPU Temperatures";

        // The SMU has no register for the I/O die temperatures, they sit in the power table and move
        // between its versions. Only layouts checked against HWiNFO are listed: average, hotspot.
        private static readonly Dictionary<uint, int[]> IodTemperatureOffsets = new Dictionary<uint, int[]>
        {
            // Granite Ridge
            { 0x00620105, new[] { 0x1A8, 0x458 } },
        };

        private readonly uint smuTableVersion;
        private readonly Sensor tctl = new Sensor("CPU (Tctl/Tdie)", 0, SensorType.Temperature);
        private readonly Sensor iodAverage = new Sensor("IOD Average", 1, SensorType.Temperature);
        private readonly Sensor iodHotspot = new Sensor("IOD Hotspot", 2, SensorType.Temperature);
        private readonly Sensor[] sensors;

        public CpuTemperatureSensors(uint smuTableVersion)
        {
            this.smuTableVersion = smuTableVersion;
            sensors = HasIodTemperature(smuTableVersion) ? new[] { tctl, iodAverage, iodHotspot } : new[] { tctl };
        }

        public SensorGroup Group => new SensorGroup(GroupName, HardwareType.Cpu, Chip.Unknown, sensors);

        public static bool HasIodTemperature(uint smuTableVersion) => IodTemperatureOffsets.ContainsKey(smuTableVersion);

        public static bool TryReadIodTemperature(uint smuTableVersion, float[] table, out float average, out float hotspot)
        {
            average = 0;
            hotspot = 0;

            if (table == null || !IodTemperatureOffsets.TryGetValue(smuTableVersion, out int[] offsets) || offsets[1] / 4 >= table.Length)
                return false;

            average = table[offsets[0] / 4];
            hotspot = table[offsets[1] / 4];
            return true;
        }

        // The CPU temperature is null while nothing shows it, the I/O die comes from the power table
        // the refresh has just read
        public void Update(float? cpuTemperature, float[] table)
        {
            tctl.Value = cpuTemperature;

            if (TryReadIodTemperature(smuTableVersion, table, out float average, out float hotspot))
            {
                iodAverage.Value = average;
                iodHotspot.Value = hotspot;
            }
        }
    }
}
