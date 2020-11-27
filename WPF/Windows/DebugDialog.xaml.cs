using AdonisUI.Controls;
using Microsoft.VisualBasic.Devices;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Management;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using ZenStates.Core;

namespace ZenTimings.Windows
{
    /// <summary>
    /// Interaction logic for DebugDialog.xaml
    /// </summary>
    public partial class DebugDialog : AdonisWindow
    {
        private string result = "";
        private readonly List<MemoryModule> modules;
        private readonly MemoryConfig MEMCFG;
        private readonly uint baseAddress;
        private readonly SystemInfo SI;
        private readonly PowerTable PT;
        private readonly BiosMemController BMC;
        private readonly string wmiScope = "root\\wmi";
        private readonly string wmiAMDACPI = "AMD_ACPI";
        private readonly Cpu CPU;
        private ManagementBaseObject pack;
        private string instanceName;
        private ManagementObject classInstance;

        public DebugDialog(uint dramBaseAddr, List<MemoryModule> memModules,
            MemoryConfig memCfg, SystemInfo systemInfo,
            BiosMemController biosMemCtrl, PowerTable powerTable,
            Cpu cpu)
        {
            InitializeComponent();
            baseAddress = dramBaseAddr;
            modules = memModules;
            SI = systemInfo;
            MEMCFG = memCfg;
            PT = powerTable;
            BMC = biosMemCtrl;
            CPU = cpu;
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
            catch { }

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
                int index = 1;

                foreach (var functionObject in functionObjects)
                {
                    AddHeading($"WMI: Bios Functions {index}");

                    try
                    {
                        pack = WMI.InvokeMethod(classInstance, functionObject, "pack", null, 0);

                        if (pack != null)
                        {
                            uint[] ID = (uint[])pack.GetPropertyValue("ID");
                            string[] IDString = (string[])pack.GetPropertyValue("IDString");
                            byte Length = (byte)pack.GetPropertyValue("Length");

                            for (var i = 0; i < Length; ++i)
                            {
                                if (IDString[i] == "")
                                    return;
                                AddLine($"{IDString[i]}: {ID[i]:X8}");
                            }
                        }
                        else
                        {
                            AddLine("<FAILED>");
                        }
                    }
                    catch { }

                    index++;
                    AddLine();
                }
            }
            catch { }
        }

        private void AddHeading(string heading)
        {
            string h =
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
            AddHeading("Memory Channels Info");
            for (var i = 0u; i < 8u; i++)
            {
                try
                {
                    uint offset = (uint)i << 20;
                    bool channel = CPU.utils.GetBits(CPU.ReadDword(offset | 0x50DF0), 19, 1) == 0;
                    bool dimm1 = CPU.utils.GetBits(CPU.ReadDword(offset | 0x50000), 0, 1) == 1;
                    bool dimm2 = CPU.utils.GetBits(CPU.ReadDword(offset | 0x50008), 0, 1) == 1;
                    bool enabled = channel && (dimm1 || dimm2);

                    AddLine($"Channel{i}: {enabled}");
                    if (enabled)
                    {
                        AddLine("-- UMC Registers");
                        uint startReg = offset | 0x50000;
                        uint endReg = offset | 0x50300;
                        while (startReg <= endReg)
                        {
                            uint data = CPU.ReadDword(startReg);
                            AddLine($"   0x{startReg:X8}: 0x{data:X8}");
                            startReg += 4;
                        }
                    }
                }
                catch
                {
                    AddLine($"Channel{i}: <FAILED>");
                }
            }
            AddLine();
        }

        private void Debug()
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                SetControlsState(false);
            }));

            result = $"{System.Windows.Forms.Application.ProductName} {System.Windows.Forms.Application.ProductVersion} Debug Report" +
                Environment.NewLine +
                Environment.NewLine;

            Type type = SI.GetType();
            PropertyInfo[] properties = type.GetProperties();

            AddHeading("System Info");
            try
            {
                AddLine("OS: " + new ComputerInfo().OSFullName);

                foreach (PropertyInfo property in properties)
                {
                    if (property.Name == "CpuId" || property.Name == "PatchLevel")
                        AddLine(property.Name + ": " + $"{property.GetValue(SI, null):X8}");
                    else if (property.Name == "SmuVersion")
                        AddLine(property.Name + ": " + SI.GetSmuVersionString());
                    else
                        AddLine(property.Name + ": " + property.GetValue(SI, null));
                }
            }
            catch
            {
                AddLine("<FAILED>");
            }
            AddLine();

            // DRAM modules info
            AddHeading("Memory Modules");

            foreach (MemoryModule module in modules)
            {
                AddLine($"{module.BankLabel} | {module.DeviceLocator}");
                AddLine($"-- {module.Manufacturer}");
                AddLine($"-- {module.PartNumber} {module.Capacity / 1024 / (1024 * 1024)}GB {module.ClockSpeed}MHz");
                AddLine();
            }

            PrintChannels();

            // Memory timings info
            AddHeading("Memory Config");
            type = MEMCFG.GetType();
            properties = type.GetProperties();

            try
            {
                AddLine($"DRAM Base Address: {baseAddress:X8}");
                foreach (PropertyInfo property in properties)
                    AddLine(property.Name + ": " + property.GetValue(MEMCFG, null));
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
                for (int i = 0; i < BMC.Table.Length; ++i)
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
                for (int i = 0; i < PT.Table.Length; ++i)
                {
                    byte[] temp = BitConverter.GetBytes(PT.Table[i]);
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
                type = PT.GetType();
                properties = type.GetProperties();

                foreach (PropertyInfo property in properties)
                {
                    if (property.Name != "Table")
                        AddLine(property.Name + ": " + property.GetValue(PT, null));
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

            AddHeading("SVI2: PCI Range");
            try
            {
                uint startAddress = 0x0005A000;
                uint endAddress = 0x0005A0FF;

                if (CPU.smu.SMU_TYPE == SMU.SmuType.TYPE_APU1)
                {
                    startAddress = 0x0006F000;
                    endAddress = 0x0006F0FF;
                }

                while (startAddress <= endAddress)
                {
                    var data = CPU.ReadDword(startAddress);
                    if (data != 0xFFFFFFFF)
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
            string unixTimestamp = Convert.ToString((DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalMinutes);
            string filename = $@"{string.Join("_", Title.Split())}_{unixTimestamp}.txt";

            if (saveAs)
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog
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

            System.IO.File.WriteAllText(filename, textBoxDebugOutput.Text);
            AdonisUI.Controls.MessageBox.Show($"Debug report saved as {filename}", saveAs ? "Save As" : "Save", AdonisUI.Controls.MessageBoxButton.OK);
        }

        private async void ButtonDebug_Click(object sender, RoutedEventArgs e) => await Task.Run(() => Debug());
        private void ButtonDebugCancel_Click(object sender, RoutedEventArgs e) => Close();
        private void ButtonDebugSave_Click(object sender, RoutedEventArgs e) => SaveToFile();
        private void ButtonDebugSaveAs_Click(object sender, RoutedEventArgs e) => SaveToFile(true);
    }
}
