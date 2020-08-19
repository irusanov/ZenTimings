using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Management;
using System.Reflection;
using System.Windows.Forms;
using ZenStates;
using ZenTimings.Utils;

namespace ZenTimings
{
    public partial class DebugDialog : Form
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
        private readonly Ops OPS;
        private ManagementBaseObject pack;
        private string instanceName;
        ManagementObject classInstance;
        private BackgroundWorker backgroundWorker1;

        public DebugDialog(uint dramBaseAddr, List<MemoryModule> memModules, 
            MemoryConfig memCfg, SystemInfo systemInfo,
            BiosMemController biosMemCtrl, PowerTable powerTable,
            Ops ops)
        {
            InitializeComponent();
            baseAddress = dramBaseAddr;
            modules = memModules;
            SI = systemInfo;
            MEMCFG = memCfg;
            PT = powerTable;
            BMC = biosMemCtrl;
            OPS = ops;
        }

        private void HandleError(string message, string title = "Error")
        {
            //SetStatusText(Resources.Error);
            MessageBox.Show(message, title);
        }

        private void SetControlsState(bool enabled = true)
        {
            buttonDebugCancel.Enabled = enabled;
            buttonDebugSave.Enabled = enabled;
            buttonDebugSaveAs.Enabled = enabled;
            buttonDebug.Enabled = enabled;
            textBoxDebugOutput.Enabled = enabled;
        }

        private void RunBackgroundTask(DoWorkEventHandler task, RunWorkerCompletedEventHandler completedHandler)
        {
            try
            {
                SetControlsState(false);

                backgroundWorker1 = new BackgroundWorker();
                backgroundWorker1.DoWork += task;
                backgroundWorker1.RunWorkerCompleted += completedHandler;
                backgroundWorker1.RunWorkerAsync();
            }
            catch (ApplicationException ex)
            {
                //SetStatusText(Resources.Error);
                SetControlsState();
                HandleError(ex.Message);
            }
        }

        private void Scan_WorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            SetControlsState();
            //SetStatusText("Scan Complete.");
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
            }
            catch { }

            try {
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
                "########################################################" +
                Environment.NewLine +
                heading + 
                Environment.NewLine +
                "########################################################" + 
                Environment.NewLine;
            result += h;
        }

        private void AddLine(string row = "")
        {
            result += row + Environment.NewLine;
        }

        private void Debug(object sender, DoWorkEventArgs e)
        {
            try 
            {
                result = $"{Application.ProductName} v{Application.ProductVersion} Debug Report" +
                    Environment.NewLine +
                    Environment.NewLine;

                AddHeading("System Info");
                Type type = SI.GetType();
                PropertyInfo[] properties = type.GetProperties();

                foreach (PropertyInfo property in properties)
                {
                    if (property.Name == "CpuId" || property.Name == "PatchLevel")
                        AddLine(property.Name + ": " + $"{property.GetValue(SI, null):X8}");
                    else if (property.Name == "SmuVersion")
                        AddLine(property.Name + ": " + SI.GetSmuVersionString());
                    else
                        AddLine(property.Name + ": " + property.GetValue(SI, null));
                }
                AddLine();
                
                // DRAM modules info
                AddHeading("Memory Modules");

                foreach (MemoryModule module in modules)
                {
                    AddLine($"{module.PartNumber} {module.Capacity / 1024 / (1024 * 1024)}GB {module.ClockSpeed}MHz");
                }
                AddLine();

                // Memory timings info
                AddHeading("Memory Config");
                type = MEMCFG.GetType();
                properties = type.GetProperties();
                AddLine($"DRAM Base Address: {baseAddress:X8}");
                foreach (PropertyInfo property in properties)
                {
                    AddLine(property.Name + ": " + property.GetValue(MEMCFG, null));
                }
                AddLine();

                // Configured DRAM memory controller settings from BIOS
                AddHeading("BIOS: Memory Controller Config");
                for (int i = 0; i < BMC.Table.Length; ++i)
                {
                    AddLine($"Index {i:D3}: {BMC.Table[i]:X2} ({BMC.Table[i]})");
                }
                AddLine();

                // SMU power table
                AddHeading("SMU: Power Table");
                for (int i = 0; i < PT.Table.Length; ++i)
                {
                    byte[] temp = BitConverter.GetBytes(PT.Table[i]);
                    AddLine($"Offset {i * 0x4:X3}: {BitConverter.ToSingle(temp, 0):F8}");
                }
                AddLine();

                // SMU power table
                AddHeading("SMU: Power Table Detected Values");
                AddLine($"MCLK: {PT.MCLK}");
                AddLine($"FCLK: {PT.FCLK}");
                AddLine($"UCLK: {PT.UCLK}");
                AddLine($"VSOC_SMU: {PT.VDDCR_SOC}");
                AddLine($"CLDO_VDDP: {PT.CLDO_VDDP}");
                AddLine($"CLDO_VDDG: {PT.CLDO_VDDG}");
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
                var wmiInstanceName = GetWmiInstanceName();
                if (wmiInstanceName.Length == 0)
                    wmiInstanceName = "<FAILED>";
                AddLine(wmiInstanceName);
                AddLine();

                PrintWmiFunctions();
                AddLine();

                AddHeading("SVI2: PCI Range");
                uint startAddress = 0x0005A000;
                uint endAddress = 0x0005A0FF;
                while (startAddress <= endAddress)
                {
                    var data = OPS.ReadDword(startAddress);
                    AddLine($"0x{startAddress:X8}: 0x{data:X8}");
                    startAddress += 4;
                }

                Invoke(new MethodInvoker(delegate
                {
                    textBoxDebugOutput.Text = result;
                }));
            }
            catch (ApplicationException ex)
            {
                Invoke(new MethodInvoker(delegate
                {
                    SetControlsState();
                    HandleError(ex.Message);
                }));
            }
        }

        private void SaveToFile(bool saveAs = false)
        {
            string unixTimestamp = Convert.ToString((DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalMinutes);
            string filename = $@"{string.Join("_", Text.Split())}_{unixTimestamp}.txt";

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

                if (saveFileDialog.ShowDialog() != DialogResult.OK)
                    return;

                filename = saveFileDialog.FileName;
            }

            System.IO.File.WriteAllText(filename, textBoxDebugOutput.Text);
            MessageBox.Show($"Debug report saved as {filename}");
        }

        private void ButtonDebug_Click(object sender, EventArgs e) => RunBackgroundTask(Debug, Scan_WorkerCompleted);
        private void ButtonDebugCancel_Click(object sender, EventArgs e) => Close();
        private void ButtonDebugSave_Click(object sender, EventArgs e) => SaveToFile();
        private void ButtonDebugSaveAs_Click(object sender, EventArgs e) => SaveToFile(true);
    }
}
