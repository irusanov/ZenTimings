using Microsoft.VisualBasic.Devices;
using Microsoft.Win32;
using System;
using System.Globalization;
using System.IO;
using System.Management;
using System.Windows;
using ZenStates.Core;
using Application = System.Windows.Application;
namespace ZenTimings.Windows
{
    /// <summary>
    ///     Interaction logic for DebugDialog.xaml
    /// </summary>
    public partial class DebugDialog
    {
        private readonly AsusWMI AWMI;
        private readonly BiosMemController BMC;
        private readonly Cpu cpu;

        private readonly ZenStates.Core.DRAM.MemoryConfig memoryConfig;
        //private readonly List<MemoryModule> modules;

        //private readonly uint baseAddress;
        private readonly string wmiAMDACPI = "AMD_ACPI";
        private readonly string wmiScope = "root\\wmi";
        private ManagementObject classInstance;
        private string instanceName;
        private ManagementBaseObject pack;
        private string result = "";
        private delegate void Action();

        public DebugDialog(BiosMemController biosMemCtrl, AsusWMI asusWmi)
        {
            InitializeComponent();
            cpu = CpuSingleton.Instance;
            //modules = memModules;
            memoryConfig = cpu.GetMemoryConfig();
            BMC = biosMemCtrl;
            AWMI = asusWmi;
        }

        private void SetControlsState(bool enabled = true)
        {
            buttonDebugCancel.IsEnabled = enabled;
            buttonDebugSave.IsEnabled = enabled;
            buttonDebugSaveAs.IsEnabled = enabled;
            buttonDebug.IsEnabled = enabled;
            textBoxDebugOutput.IsEnabled = enabled;
        }

        private string GetWmiInstanceName()
        {
            try
            {
                instanceName = WMI.GetInstanceName(wmiScope, wmiAMDACPI);
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
                classInstance = new ManagementObject(wmiScope,
                    $"{wmiAMDACPI}.InstanceName='{instanceName}'",
                    null);

                // Get function names with their IDs
                string[] functionObjects = { "GetObjectID", "GetObjectID2" };
                var index = 1;

                foreach (var functionObject in functionObjects)
                {
                    AddHeading($"WMI: Bios Functions {index}");

                    try
                    {
                        pack = WMI.InvokeMethodAndGetValue(classInstance, functionObject, "pack", null, 0);

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
                    catch
                    {
                        // ignored
                    }

                    index++;
                    AddLine();
                }
            }
            catch
            {
                // ignored
            }
        }

        private void AddHeading(string heading)
        {
            var h =
                "######################################################" +
                Environment.NewLine +
                heading +
                Environment.NewLine +
                "######################################################" +
                Environment.NewLine;
            result += h;
        }

        private void AddLine(string row = "")
        {
            result += row + Environment.NewLine;
        }

        private void PrintChannels()
        {
            uint channelsPerDimm = 1; // memoryConfig.Type >= ZenStates.Core.DRAM.MemoryConfig.MemType.DDR5 ? 2u : 1u;
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
                    var channel = Utils.GetBits(cpu.ReadDword(offset | 0x50DF0), 19, 1) == 0;
                    var dimm1 = Utils.GetBits(cpu.ReadDword(offset | 0x50000), 0, 1) == 1;
                    var dimm2 = Utils.GetBits(cpu.ReadDword(offset | 0x50008), 0, 1) == 1;
                    var enabled = channel && (dimm1 || dimm2);

                    AddLine($"Channel{i / channelsPerDimm}: {enabled}");
                    if (enabled)
                    {
                        AddLine("-- UMC Registers");
                        var startReg = offset | 0x50000;
                        var endReg = offset | 0x50300;
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
            AddLine();
        }

        private void Debug()
        {
            Application.Current.Dispatcher.Invoke(new Action(() => { SetControlsState(false); }));

            result =
                $"{System.Windows.Forms.Application.ProductName} {System.Windows.Forms.Application.ProductVersion} Debug Report" +
                Environment.NewLine +
                $"{"Core Version: "}{cpu.Version}" +
                Environment.NewLine +
                Environment.NewLine;

            var type = cpu.systemInfo.GetType();
            var properties = type.GetProperties();

            AddHeading("System Info");
            try
            {
                AddLine($"{"OS:",-19}{new ComputerInfo().OSFullName}");

                foreach (var property in properties)
                {
                    if (property.Name == "CpuId" || property.Name == "PatchLevel" || property.Name == "SmuTableVersion")
                        AddLine($"{property.Name + ":",-19}{property.GetValue(cpu.systemInfo, null):X8}");
                    else if (property.Name == "SmuVersion")
                        AddLine($"{property.Name + ":",-19}{cpu.systemInfo.GetSmuVersionString()}");
                    else
                        AddLine($"{property.Name + ":",-19}{property.GetValue(cpu.systemInfo, null)}");

                }
                AddLine($"{"DRAM Base Address:",-19}{((long)cpu.powerTable.DramBaseAddressHi << 32) | cpu.powerTable.DramBaseAddress:X16}");
            }
            catch
            {
                AddLine("<FAILED>");
            }

            AddLine();

            // DRAM modules info
            AddHeading("Memory Modules");

            foreach (var module in memoryConfig.Modules)
            {
                AddLine($"{module.BankLabel} | {module.DeviceLocator}");
                AddLine($"-- Slot: {module.Slot}");
                if (module.Rank == ZenStates.Core.DRAM.MemRank.DR)
                    AddLine("-- Dual Rank");
                else
                    AddLine("-- Single Rank");
                AddLine($"-- DCT Offset: 0x{module.DctOffset >> 20:X}");
                AddLine($"-- Manufacturer: {module.Manufacturer}");
                AddLine($"-- {module.PartNumber} {module.Capacity} {module.ClockSpeed}MHz");
                AddLine($"-- {module.AddressConfig}");
                AddLine();
            }

            PrintChannels();

            // Memory timings info
            AddHeading("Memory Config");
            type = memoryConfig.Timings[0].Value.GetType();
            properties = type.GetProperties();

            try
            {
                foreach (var property in properties)
                    AddLine($"{property.Name + ":",-18}{memoryConfig.Timings[0].Value[property.Name]}");
            }
            catch
            {
                AddLine("<FAILED>");
            }

            AddLine();

            // AOD Table
            AddHeading("ACPI: AOD Table");
            type = cpu.info.aod.Table.GetType();
            properties = type.GetProperties();
            try
            {
                foreach (var property in properties)
                {
                    AddLine($"{property.Name + ":",-19}{property.GetValue(cpu.info.aod.Table, null)}");
                }
            }
            catch
            {
                AddLine("<FAILED>");
            }

            AddLine();

            AddHeading("ACPI: Raw AOD Table");
            try
            {
                for (var i = 0; i < cpu.info.aod.Table.RawAodTable.Length; i++)
                    AddLine($"Index {i:D3}: {cpu.info.aod.Table.RawAodTable[i]:X2} ({cpu.info.aod.Table.RawAodTable[i]})");
            }
            catch
            {
                AddLine("<FAILED>");
            }

            AddLine();

            // Configured DRAM memory controller settings from BIOS
            AddHeading("BIOS: Memory Controller Config");
            try
            {
                for (var i = 0; i < BMC.Table.Length; i++)
                    AddLine($"Index {i:D3}: {BMC.Table[i]:X2} ({BMC.Table[i]})");
            }
            catch
            {
                AddLine("<FAILED>");
            }

            AddLine();

            // SMU power table
            AddHeading("SMU: Power Table");
            try
            {
                for (var i = 0; i < cpu.powerTable.Table.Length; i++)
                {
                    var temp = BitConverter.GetBytes(cpu.powerTable.Table[i]);
                    AddLine($"Offset {i * 0x4:X3}: {BitConverter.ToSingle(temp, 0):F8}");
                }
            }
            catch
            {
                AddLine("<FAILED>");
            }

            AddLine();

            // SMU power table
            AddHeading("SMU: Power Table Detected Values");
            try
            {
                type = cpu.powerTable.GetType();
                properties = type.GetProperties();

                foreach (var property in properties)
                {
                    if (property.Name == "TableVersion")
                        AddLine($"{property.Name + ":",-25}{property.GetValue(cpu.powerTable, null):X8}");
                    else if (property.Name != "Table")
                        AddLine($"{property.Name + ":",-25}{property.GetValue(cpu.powerTable, null)}");
                }

                /*AddLine($"MCLK: {PT.MCLK}");
                AddLine($"FCLK: {PT.FCLK}");
                AddLine($"UCLK: {PT.UCLK}");
                AddLine($"VSOC_SMU: {PT.VDDCR_SOC}");
                AddLine($"CLDO_VDDP: {PT.CLDO_VDDP}");
                AddLine($"CLDO_VDDG: {PT.CLDO_VDDG_IOD}");
                AddLine($"CLDO_VDDG: {PT.CLDO_VDDG_CCD}");*/
            }
            catch
            {
                AddLine("<FAILED>");
            }

            AddLine();

            // All WMI classes in root namespace
            /*AddHeading("WMI: Root Classes");
            List<string> namespaces = WMI.GetClassNamesWithinWmiNamespace(wmiScope);

            foreach (var ns in namespaces)
            {
                AddLine(ns);
            }
            AddLine();*/

            // Check if AMD_ACPI class exists
            AddHeading("WMI: AMD_ACPI");
            if (WMI.Query(wmiScope, wmiAMDACPI) != null)
                AddLine("OK");
            else
                AddLine("<FAILED>");
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

            if (AWMI != null && AWMI.Status == 1)
            {
                AddHeading("ASUS WMI");
                try
                {
                    foreach (var sensor in AWMI.sensors)
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

            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                textBoxDebugOutput.Text = result;
                SetControlsState();
            }));
        }

        private void SaveToFile(bool saveAs = false)
        {
            var unixTimestamp = Convert.ToString(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMinutes,
                CultureInfo.InvariantCulture);
            var filename = $@"{string.Join("_", Title.Split())}_{unixTimestamp}.txt";

            if (saveAs)
            {
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "text files (*.txt)|*.txt|All files (*.*)|*.*",
                    FilterIndex = 1,
                    DefaultExt = "txt",
                    FileName = filename,
                    RestoreDirectory = true
                };

                if (saveFileDialog.ShowDialog() == false)
                    return;

                filename = saveFileDialog.FileName;
            }

            File.WriteAllText(filename, textBoxDebugOutput.Text);
            MessageBox.Show($"Debug report saved as {filename}", saveAs ? "Save As" : "Save");
        }

        private void ButtonDebug_Click(object sender, RoutedEventArgs e)
        {
            Debug();
        }

        private void ButtonDebugCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ButtonDebugSave_Click(object sender, RoutedEventArgs e)
        {
            SaveToFile();
        }

        private void ButtonDebugSaveAs_Click(object sender, RoutedEventArgs e)
        {
            SaveToFile(true);
        }
    }
}