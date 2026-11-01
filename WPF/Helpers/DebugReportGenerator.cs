using Microsoft.VisualBasic.Devices;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using ZenStates.Core;
using ZenStates.Core.Common;
using ZenStates.Core.Hardware;
using ZenStates.Core.Hardware.DRAM.DDR5.Spd;
using ZenTimings.Common;
using ZenTimings.Export;
using ZenTimings.Settings;
using ZenTimings.Utils;
using DRAM = ZenStates.Core.Hardware.DRAM;

namespace ZenTimings.Helpers
{
    /// <summary>
    /// Builds the debug report: everything the core and the system can tell, section by section. A section that
    /// fails is marked and the report goes on, since the report matters most on systems where parts of the core
    /// failed to initialize. It always aims at the full report; with the Advanced Mode setting off the power table
    /// and the I/O driver are not loaded, which the report notes.
    /// </summary>
    /// <remarks>Used by the Debug dialog and by the report sending, which may run it after an error.</remarks>
    internal sealed class DebugReportGenerator
    {
        /// <summary>How long <see cref="GenerateAsync"/> waits for the full report before falling back.</summary>
        public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);

        /// <summary>The sections the report can't fill with the Advanced Mode setting off.</summary>
        public const string AdvancedModeOffSections = "power table, APOB, AOD and AGESA data";

        private const string WmiAmdAcpi = "AMD_ACPI";
        private const string WmiScope = "root\\wmi";

        private readonly Cpu cpu;
        private readonly BiosMemController bmc;
        private readonly AsusWMI asusWmi;
        private readonly object syncRoot = new object();
        private readonly StringBuilder result = new StringBuilder();
        private DRAM.MemoryConfig memoryConfig;
        private string instanceName;

        /// <param name="cpu">The core, null when it could not be created (only the fallback report is built then).</param>
        /// <param name="biosMemController">DDR4: the memory controller config read from the BIOS, if loaded.</param>
        /// <param name="asusWmi">ASUS WMI sensors, if loaded.</param>
        public DebugReportGenerator(Cpu cpu, BiosMemController biosMemController = null, AsusWMI asusWmi = null)
        {
            this.cpu = cpu;
            bmc = biosMemController;
            this.asusWmi = asusWmi;
        }

        /// <summary>The exception that ended the report early, null when it ran to the end.</summary>
        public Exception Error { get; private set; }

        /// <summary><see cref="GenerateAsync"/> gave up waiting and returned the fallback report.</summary>
        public bool TimedOut { get; private set; }

        /// <summary>The report collected so far.</summary>
        public string Partial
        {
            get
            {
                lock (syncRoot)
                    return result.ToString();
            }
        }

        /// <summary>"ZenTimings 1.2.3", the start of the first line of a report.</summary>
        public static string ProductHeader
        {
            get { return $"{System.Windows.Forms.Application.ProductName} {System.Windows.Forms.Application.ProductVersion}"; }
        }

        /// <summary>
        /// Builds the report on the calling thread. An exception that escapes the sections ends the report with it
        /// (<see cref="Error"/>); without the core only the fallback report is built.
        /// </summary>
        public string Generate(bool printSerialNumbers = false)
        {
            if (cpu == null)
                return BuildFallbackReport("The core is not available.");

            try
            {
                CoreOptionsScope.Run(printSerialNumbers, Build);
            }
            catch (Exception ex)
            {
                Error = ex;
                AddLine();
                AddLine("<FAILED> " + ex);
            }

            return Partial;
        }

        /// <summary>
        /// Builds the report on a background thread. Past <paramref name="timeout"/> (a hung hardware read) it
        /// returns the fallback report with what was collected so far, and <see cref="TimedOut"/> is set.
        /// </summary>
        public async Task<string> GenerateAsync(bool printSerialNumbers = false, TimeSpan? timeout = null)
        {
            TimeSpan limit = timeout ?? DefaultTimeout;
            Task<string> work = Task.Run(() => Generate(printSerialNumbers));

            if (await Task.WhenAny(work, Task.Delay(limit)).ConfigureAwait(false) == work)
                return await work.ConfigureAwait(false);

            TimedOut = true;
            return BuildFallbackReport($"The report did not finish within {(int)limit.TotalSeconds} s.", Partial);
        }

        /// <summary>
        /// A short report that needs next to nothing from the core: versions, OS, CPU, why the full report is
        /// missing, optional details (an exception, crash log lines), then whatever was collected.
        /// </summary>
        public static string BuildFallbackReport(string reason, string partial = null, string details = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{ProductHeader} Debug Report");
            sb.AppendLine($"{"PawnIO Version: "}{Safe(() => DriverHelper.Version?.ToString())}");
            sb.AppendLine();
            sb.Append(ReportBuilder.Heading("Fallback Report"));
            sb.AppendLine($"{"Reason:",-19}{reason}");
            sb.AppendLine($"{"OS:",-19}{Safe(() => new ComputerInfo().OSFullName)} ({(Environment.Is64BitOperatingSystem ? "64" : "32")}-bit)");
            sb.AppendLine($"{"CLR:",-19}{Environment.Version}");

            // Only a core that already exists: creating one here could fail or hang the same way again
            Cpu existing = CpuSingleton.Existing;
            if (existing != null)
            {
                sb.AppendLine($"{"Core Version:",-19}{Safe(() => existing.Version?.ToString())}");
                sb.AppendLine($"{"CpuName:",-19}{Safe(() => existing.systemInfo?.CpuName)}");
                sb.AppendLine($"{"CpuId:",-19}{Safe(() => existing.systemInfo != null ? existing.systemInfo.CpuId.Value.ToString("X8") : null)}");
                sb.AppendLine($"{"CodeName:",-19}{Safe(() => existing.info.codeName.ToString())}");
            }
            else
            {
                sb.AppendLine($"{"Core:",-19}not created");
            }

            if (!AppSettings.Instance.AdvancedMode)
                sb.AppendLine($"{"Advanced Mode:",-19}Off ({AdvancedModeOffSections} may be missing)");

            if (!string.IsNullOrEmpty(details))
            {
                sb.AppendLine();
                sb.Append(ReportBuilder.Heading("Details"));
                sb.AppendLine(details.TrimEnd());
            }

            if (!string.IsNullOrEmpty(partial))
            {
                sb.AppendLine();
                sb.Append(ReportBuilder.Heading("Partial Report"));
                sb.Append(partial);
            }

            return sb.ToString();
        }

        private static string Safe(Func<string> value)
        {
            try
            {
                return value() ?? "unknown";
            }
            catch
            {
                return "<FAILED>";
            }
        }

        private void AddHeading(string heading)
        {
            lock (syncRoot)
                result.Append(ReportBuilder.Heading(heading));
        }

        private void AddLine(string row = "")
        {
            lock (syncRoot)
                result.Append(row + Environment.NewLine);
        }

        private string GetWmiInstanceName()
        {
            try
            {
                instanceName = WMI.GetInstanceName(WmiScope, WmiAmdAcpi);
            }
            catch
            {
                // ignored
            }

            return instanceName;
        }

        private void PrintWmiFunctions()
        {
            try
            {
                using (var classInstance = new ManagementObject(WmiScope, $"{WmiAmdAcpi}.InstanceName='{instanceName}'", null))
                {
                    // Get function names with their IDs
                    string[] functionObjects = { "GetObjectID", "GetObjectID2" };
                    var index = 1;

                    foreach (var functionObject in functionObjects)
                    {
                        AddHeading($"WMI: Bios Functions {index}");

                        try
                        {
                            using (var pack = WMI.InvokeMethodAndGetValue(classInstance, functionObject, "pack", null, 0))
                            {
                                if (pack != null)
                                {
                                    var ID = (uint[])pack.GetPropertyValue("ID");
                                    var IDString = (string[])pack.GetPropertyValue("IDString");
                                    var Length = (byte)pack.GetPropertyValue("Length");

                                    for (var i = 0; i < Length; ++i)
                                    {
                                        if (IDString[i] == "")
                                            break;
                                        AddLine($"{IDString[i] + ":",-30}{ID[i]:X8}");
                                    }
                                }
                                else
                                {
                                    AddLine("<FAILED>");
                                }
                            }
                        }
                        catch
                        {
                            // ignored
                        }

                        index++;
                        AddLine();
                    }
                }
            }
            catch
            {
                // ignored
            }
        }

        private void PrintChannels()
        {
            try
            {
                uint channelsPerDimm = 1; // memoryConfig.Type >= DRAM.MemoryConfig.MemType.DDR5 ? 2u : 1u;
                AddHeading("Memory Channels Info");

                AddLine("-- UMC Configuration");

                for (var i = 0u; i < 0xC; i += 1)
                {
                    var offset = i << 20;
                    var reg = offset | 0x50100;
                    AddLine($"0x{reg:X8}: 0x{cpu.ReadDword(reg):X8}");
                }

                AddLine();

                for (var i = 0u; i < 0xC * channelsPerDimm; i += channelsPerDimm)
                {
                    try
                    {
                        var offset = i << 20;
                        var channel = ZenStates.Core.Utils.GetBits(cpu.ReadDword(offset | 0x50DF0), 19, 1) == 0;
                        var dimm1 = ZenStates.Core.Utils.GetBits(cpu.ReadDword(offset | 0x50000), 0, 1) == 1;
                        var dimm2 = ZenStates.Core.Utils.GetBits(cpu.ReadDword(offset | 0x50008), 0, 1) == 1;
                        var enabled = channel && (dimm1 || dimm2);

                        AddLine($"Channel{i / channelsPerDimm}: {enabled}");
                        if (enabled)
                        {
                            AddLine("-- UMC Registers");
                            var startReg = offset | 0x50000;
                            var endReg = offset | 0x50FFC;
                            while (startReg <= endReg)
                            {
                                var data = cpu.ReadDword(startReg);
                                AddLine($"   0x{startReg:X8}: 0x{data:X8}");
                                startReg += 4;
                            }
                        }
                    }
                    catch
                    {
                        AddLine($"Channel{i / channelsPerDimm}: <FAILED>");
                    }
                }
            }
            catch (Exception ex)
            {
                AddLine($"<FAILED: {ex.Message}>");
            }

            AddLine();
        }

        private bool PrintRawBinaryData(byte[] data)
        {
            if (data != null && typeof(byte[]).IsAssignableFrom(data.GetType()))
            {
                for (var i = 0; i < data.Length; i += 16)
                {
                    var hexLine = "";
                    for (var j = 0; j < 16 && i + j < data.Length; j++)
                    {
                        hexLine += $"{data[i + j]:X2}";
                        if (j < 15) hexLine += " ";
                    }
                    //AddLine($"0x{i:X8}: {hexLine}");
                    AddLine($"{hexLine}");
                }
                return true;
            }
            return false;
        }

        // Adds one section of the report; a section that fails is marked and the report goes on,
        // since the report matters most on systems where parts of the core failed to initialize.
        private void AddReport(Func<string> report)
        {
            try
            {
                AddLine(report());
            }
            catch (Exception ex)
            {
                AddLine("<FAILED>");
                AddLine(ex.Message);
            }
        }

        private void Build()
        {
            lock (syncRoot)
                result.Clear();

            memoryConfig = cpu.GetMemoryConfig();

            AddLine($"{ProductHeader} Debug Report");
            AddLine($"{"Core Version: "}{cpu.Version}");
            AddLine($"{"PawnIO Version: "}{DriverHelper.Version}");
            AddLine();

            var type = cpu.systemInfo.GetType();
            var properties = type.GetProperties();

            AddHeading("System Info");
            try
            {
                AddLine($"{"OS:",-19}{new ComputerInfo().OSFullName}");

                foreach (var property in properties)
                {
                    if (property.Name == "Hardware" || property.Name == "SensorGroups")
                        continue;

                    if (property.Name == "CpuId" || property.Name == "PatchLevel" || property.Name == "SmuTableVersion")
                        AddLine($"{property.Name + ":",-19}{property.GetValue(cpu.systemInfo, null):X8}");
                    else if (property.Name == "SmuVersion")
                        AddLine($"{property.Name + ":",-19}{cpu.systemInfo.SmuVersion}");
                    else if (property.Name == "Model" || property.Name == "ExtendedModel" || property.Name == "BaseModel")
                        AddLine($"{property.Name + ":",-19}{property.GetValue(cpu.systemInfo, null)} (0x{property.GetValue(cpu.systemInfo, null):X})");
                    else if (property.Name != "SMBios")
                        AddLine($"{property.Name + ":",-19}{property.GetValue(cpu.systemInfo, null)}");

                }
                AddLine($"{"DRAM Base Address:",-19}{(long)cpu.powerTable.DramBaseAddress:X16}");
                AddLine($"{"MemType:",-19}{memoryConfig.Type}");
                if (!AppSettings.Instance.AdvancedMode)
                    AddLine($"{"Advanced Mode:",-19}Off ({AdvancedModeOffSections} may be missing)");
                //AddLine($"{"DRAM Base Address:",-19}{((long)cpu.powerTable.DramBaseAddressHi << 32) | cpu.powerTable.DramBaseAddress:X16}");
            }
            catch
            {
                AddLine("<FAILED>");
            }

            AddLine();

            AddHeading("CPU Topology");
            AddReport(GetTopologyReport);
            AddLine();

            // DRAM modules info. The SPD is read in the background at startup; a report taken right away waits for it.
            memoryConfig?.WaitForSpdInfo(5000);
            AddHeading("Memory Modules");

            try
            {
                foreach (var module in memoryConfig.Modules)
                {
                    AddLine($"{module.BankLabel} | {module.DeviceLocator}");
                    AddLine($"-- Slot: {module.Slot}");
                    if (module.Rank == DRAM.MemRank.DR)
                        AddLine("-- Dual Rank");
                    else
                        AddLine("-- Single Rank");
                    AddLine($"-- DCT Offset: 0x{module.DctOffset >> 20:X}");
                    AddLine($"-- Manufacturer: {module.Manufacturer}");
                    AddLine($"-- {module.PartNumber} {module.Capacity} {module.ClockSpeed}MHz");
                    AddLine($"-- {module.AddressConfig}");
                    AddLine();
                }
            }
            catch (Exception ex)
            {
                AddLine("<FAILED>");
                AddLine(ex.Message);
            }

            if (cpu.memoryConfig?.Type == DRAM.MemType.DDR5)
            {
                AddHeading("SMBUS Memory Modules");
                AddLine();

                try
                {
                    Dictionary<byte, Ddr5SpdInfo> results = CpuSingleton.Instance.memoryConfig.ReadAndDecodeAll();

                    foreach (KeyValuePair<byte, Ddr5SpdInfo> kvp in results ?? new Dictionary<byte, Ddr5SpdInfo>())
                    {
                        AddLine(string.Format("DIMM at I2C address 0x{0:X2}", kvp.Key));
                        AddLine(kvp.Value.ToString());
                    }
                }
                catch (Exception ex)
                {
                    AddLine("<FAILED>");
                    AddLine(ex.Message);
                }
                AddLine();
            }

            if (cpu.memoryConfig?.Type == DRAM.MemType.LPDDR5)
            {
                // Soldered LPDDR5: the SPD copies of the APOB (its raw 1/17 entry is in the APOB section). Not "SMBUS
                // Memory Modules": that section is parsed back as SPD dumps read from the bus.
                AddHeading("APOB Memory Modules (LPDDR5 SPD)");
                AddLine();

                try
                {
                    var spd = cpu.memoryConfig.SpdInfo;
                    if (spd == null || spd.Count == 0)
                        AddLine("No SPD in the APOB.");

                    foreach (KeyValuePair<byte, Ddr5SpdInfo> kvp in spd ?? new Dictionary<byte, Ddr5SpdInfo>())
                    {
                        AddLine(string.Format("{0} {1}", kvp.Value.FromApob ? "APOB slot" : "DIMM at I2C address", kvp.Key));
                        AddLine(kvp.Value.ToString());
                    }
                }
                catch (Exception ex)
                {
                    AddLine("<FAILED>");
                    AddLine(ex.Message);
                }
                AddLine();
            }

            if (cpu.memoryConfig?.Type == DRAM.MemType.DDR4)
            {
                // Not "SMBUS Memory Modules": that section is parsed back as DDR5 SPD dumps
                AddHeading("SMBUS Memory Modules (DDR4 SPD)");
                AddLine();

                try
                {
                    var results = cpu.memoryConfig.ReadAndDecodeAllDdr4();
                    if (results == null || results.Count == 0)
                        AddLine("No module SPD could be read.");

                    foreach (var kvp in results ?? new Dictionary<byte, DRAM.DDR4.Spd.Ddr4SpdInfo>())
                    {
                        AddLine(string.Format("DIMM at SPD address 0x{0:X2}", kvp.Key));
                        AddLine(kvp.Value.ToString());
                    }
                }
                catch (Exception ex)
                {
                    AddLine("<FAILED>");
                    AddLine(ex.Message);
                }
                AddLine();
            }

            if (cpu.memoryConfig?.Type == DRAM.MemType.DDR4)
            {
                AddHeading("SMBUS Memory Module Thermal Sensors");
                AddLine();

                try
                {
                    var sensors = cpu.memoryConfig.Ddr4ThermalSensors;
                    if (sensors == null || sensors.Count == 0)
                        AddLine("No module SPD found on the SMBus.");

                    foreach (var kvp in sensors ?? new Dictionary<byte, DRAM.DDR4.Thermal.Ddr4ThermalData>())
                    {
                        AddLine(string.Format("DIMM at SPD address 0x{0:X2}", kvp.Key));
                        AddLine(kvp.Value != null ? kvp.Value.ToString() : "  Thermal sensor: not available");
                    }
                }
                catch (Exception ex)
                {
                    AddLine("<FAILED>");
                    AddLine(ex.Message);
                }
                AddLine();
            }

            PrintChannels();

            // Memory timings info
            AddHeading("Memory Config");
            try
            {
                type = memoryConfig.Timings[0].Value.GetType();
                properties = type.GetProperties();

                foreach (var property in properties)
                    if (property.Name != "Item")
                        AddLine($"{property.Name + ":",-28}{memoryConfig.Timings[0].Value[property.Name]}");
            }
            catch
            {
                AddLine("<FAILED>");
            }

            AddLine();
            AddReport(() => cpu.info.apob?.GetReport() ?? "APOB: not available");

            AddLine();
            AddReport(() => cpu.info.aod?.GetReport() ?? "AOD: not available");

            // Configured DRAM memory controller settings from BIOS
            if (bmc?.Table != null)
            {
                AddHeading("BIOS: Memory Controller Config");
                try
                {
                    for (var i = 0; i < bmc.Table.Length; i++)
                        AddLine($"Index {i:D3}: {bmc.Table[i]:X2} ({bmc.Table[i]})");
                }
                catch (Exception ex)
                {
                    AddLine("<FAILED>");
                    AddLine(ex.Message);
                }
            }

            // SMU power table
            AddLine();
            AddReport(() => cpu.powerTable?.GetReport() ?? "SMU: Power Table not available");

            AddHeading("SuperIO");
            foreach (var hardware in cpu.systemInfo?.Hardware ?? new List<IHardware>())
            {
                if (hardware.HardwareType == HardwareType.SuperIO)
                    AddReport(hardware.GetReport);
            }
            AddLine();
            AddReport(() => SystemInfo.SMBios.GetReport());

            // All WMI classes in root namespace
            /*AddHeading("WMI: Root Classes");
            List<string> namespaces = WMI.GetClassNamesWithinWmiNamespace(WmiScope);

            foreach (var ns in namespaces)
            {
                AddLine(ns);
            }
            AddLine();*/

            // Check if AMD_ACPI class exists
            AddHeading("WMI: AMD_ACPI");
            AddReport(() => WMI.Query(WmiScope, WmiAmdAcpi) != null ? "OK" : "<FAILED>");
            AddLine();

            AddHeading("WMI: Instance Name");
            try
            {
                var wmiInstanceName = GetWmiInstanceName();
                if (wmiInstanceName.Length == 0)
                    wmiInstanceName = "<FAILED>";
                AddLine(wmiInstanceName);
            }
            catch
            {
                AddLine("<FAILED>");
            }

            AddLine();

            PrintWmiFunctions();
            AddLine();

            if (asusWmi != null && asusWmi.Status == 1)
            {
                AddHeading("ASUS WMI");
                try
                {
                    foreach (var sensor in asusWmi.sensors)
                        AddLine($"{sensor.Name + ": ",-25}{sensor.Value}");
                }
                catch
                {
                    AddLine("<FAILED>");
                }

                AddLine();
            }

            AddHeading("SVI2: PCI Range");
            try
            {
                uint startAddress = 0x0005A000;
                uint endAddress = 0x0005A0FF;

                if (cpu.smu.SMU_TYPE == SMU.SmuType.TYPE_APU1 || cpu.smu.SMU_TYPE == SMU.SmuType.TYPE_APU2)
                {
                    startAddress = 0x0006F000;
                    endAddress = 0x0006F0FF;
                }

                while (startAddress <= endAddress)
                {
                    uint data = 0xFFFFFFFF;
                    bool success = cpu.ReadDwordEx(startAddress, ref data);
                    if (success)
                    {
                        AddLine($"0x{startAddress:X8}: 0x{data:X8}");
                    }
                    startAddress += 4;
                }
            }
            catch
            {
                AddLine("<FAILED>");
            }

            // Strix / Krackan have no SMUFUSE block at 0x5D200; their fuses are looked for around 0x3820AB0
            if (cpu.ReadDword(0x0005D218) == 0xFFFFFFFF)
            {
                AddHeading("SMU: SMUFUSE 0x3820A00");
                try
                {
                    for (uint address = 0x03820A00; address <= 0x03820BFF; address += 4)
                        AddLine($"0x{address:X8}: 0x{cpu.ReadDword(address):X8}");
                }
                catch
                {
                    AddLine("<FAILED>");
                }

                AddLine();
            }

            AddHeading("SMU: SMUFUSE NBSMNIND");
            try
            {
                uint startAddress = 0x0005D200;
                uint endAddress = 0x0005D5FF;

                while (startAddress <= endAddress)
                {
                    var data = cpu.ReadDword(startAddress);
                    // if (data != 0xFFFFFFFF)
                    {
                        AddLine($"0x{startAddress:X8}: 0x{data:X8}");
                    }
                    startAddress += 4;
                }
            }
            catch
            {
                AddLine("<FAILED>");
            }

            AddLine();
            AddReport(() => Mmio.Instance.GetReport());
        }

        /// <summary>
        /// How the CCD / CCX / core counts were found: the CCXs CPUID reports, the CCD and core fuses with their raw
        /// values and the disabled cores they give, and why a fuse was not used.
        /// </summary>
        private string GetTopologyReport()
        {
            Cpu.CpuTopology t = cpu.info.topology;
            StringBuilder sb = new StringBuilder();

            sb.AppendLine($"{"CCDs:",-19}{t.ccds} (enabled 0x{t.ccdEnableMap:X2}, disabled 0x{t.ccdDisableMap:X2})");
            if (t.fuse1 != 0)
                sb.AppendLine($"{"CCD fuses:",-19}0x{t.fuse1:X8} = 0x{t.ccdsPresent:X8}, 0x{t.fuse2:X8} = 0x{t.ccdsDown:X8}");

            string ccxCores = t.ccxCoreCounts != null && t.ccxCoreCounts.Length > 0
                ? string.Join(", ", Array.ConvertAll(t.ccxCoreCounts, c => c.ToString()))
                : "unknown";
            sb.AppendLine($"{"CCXs:",-19}{t.ccxs} (cores per CCX from CPUID: {ccxCores})");
            sb.AppendLine($"{"Cores:",-19}{t.cores} of {t.physicalCores} slots, {t.threadsPerCore} thread(s) each, {t.cpuNodes} node(s)");

            if (t.coreFuseAddress != 0)
            {
                sb.AppendLine($"{"Core fuse:",-19}0x{t.coreFuseAddress:X8}, mask at bit {t.coreFuseShift}");
                for (int i = 0; t.coreFuseValues != null && i < t.coreFuseValues.Length; i++)
                {
                    string disabled = t.coreDisableMap != null && i < t.coreDisableMap.Length
                        ? $", disabled cores 0x{t.coreDisableMap[i]:X2}"
                        : "";
                    string label = "  CCD " + i + ":";
                    sb.AppendLine($"{label,-19}0x{t.coreFuseValues[i]:X8}{disabled}");
                }
            }
            else
            {
                sb.AppendLine($"{"Core fuse:",-19}not known for this processor");
            }

            if (!string.IsNullOrEmpty(t.fuseNote))
                sb.AppendLine($"{"Note:",-19}{t.fuseNote}");

            return sb.ToString().TrimEnd();
        }
    }
}
