using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Management;
using System.Reflection;
using System.Windows.Forms;
using ZenStates;

namespace ZenTimings
{
    public partial class DebugDialog : Form
    {
        private string result = "";
        private readonly List<MemoryModule> modules;
        private readonly MemoryConfig MEMCFG;
        private readonly uint baseAddress;
        private readonly SystemInfo SI;
        private readonly uint[] PT;
        private readonly byte[] BMC;
        private readonly string wmiScope = "root\\wmi";
        private readonly string className = "AMD_ACPI";
        private readonly Ops OPS;
        private ManagementBaseObject pack;
        private string instanceName;
        ManagementObject classInstance;
        private BackgroundWorker backgroundWorker1;

        public DebugDialog(uint dramBaseAddr, List<MemoryModule> memModules, 
            MemoryConfig memCfg, SystemInfo systemInfo,
            byte[] biosMemCtrlTable, uint[] powerTable,
            Ops ops)
        {
            InitializeComponent();
            baseAddress = dramBaseAddr;
            modules = memModules;
            SI = systemInfo;
            MEMCFG = memCfg;
            PT = powerTable;
            BMC = biosMemCtrlTable;
            OPS = ops;
        }

        private void HandleError(string message, string title = "Error")
        {
            //SetStatusText(Resources.Error);
            MessageBox.Show(message, title);
        }

        private void SetButtonsState(bool enabled = true)
        {
            buttonDebugCancel.Enabled = enabled;
            buttonDebugSave.Enabled = enabled;
            buttonDebug.Enabled = enabled;
        }

        private void RunBackgroundTask(DoWorkEventHandler task, RunWorkerCompletedEventHandler completedHandler)
        {
            try
            {
                SetButtonsState(false);

                backgroundWorker1 = new BackgroundWorker();
                backgroundWorker1.DoWork += task;
                backgroundWorker1.RunWorkerCompleted += completedHandler;
                backgroundWorker1.RunWorkerAsync();
            }
            catch (ApplicationException ex)
            {
                //SetStatusText(Resources.Error);
                SetButtonsState();
                HandleError(ex.Message);
            }
        }

        private void Scan_WorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            SetButtonsState();
            //SetStatusText("Scan Complete.");
        }

        private string GetWmiInstanceName()
        {
            try
            {
                instanceName = WMI.GetInstanceName(wmiScope, className);
            }
            catch { }

            return instanceName;
        }

        private void PrintWmiFunctions() 
        {
            try
            {
                classInstance = new ManagementObject(wmiScope,
                    $"{className}.InstanceName='{instanceName}'",
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
                for (int i = 0; i < BMC.Length; ++i)
                {
                    AddLine($"Index {i:D3}: {BMC[i]:X2} ({BMC[i]})");
                }
                AddLine();

                // SMU power table
                AddHeading("SMU: Power Table");
                for (int i = 0; i < PT.Length; ++i)
                {
                    byte[] temp = BitConverter.GetBytes(PT[i]);
                    AddLine($"Offset {i * 0x4:X3}: {BitConverter.ToSingle(temp, 0):F8}");
                }
                AddLine();

                // All WMI classes in root namespace
                AddHeading("WMI: Root Classes");
                List<string> namespaces = WMI.GetClassNamesWithinWmiNamespace(wmiScope);

                foreach (var ns in namespaces)
                {
                    AddLine(ns);
                }
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
                AddLine();

                textBoxDebugOutput.Text = result;
            }
            catch (ApplicationException ex)
            {
                Invoke(new MethodInvoker(delegate
                {
                    SetButtonsState();
                    HandleError(ex.Message);
                }));
            }
        }

        private void ButtonDebug_Click(object sender, EventArgs e)
        {
            RunBackgroundTask(Debug, Scan_WorkerCompleted);
        }

        private void ButtonDebugCancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void ButtonDebugSave_Click(object sender, EventArgs e)
        {
            string unixTimestamp = Convert.ToString((DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalMinutes);
            string filename = $@"{string.Join("_", Text.Split())}_{unixTimestamp}.txt";
            System.IO.File.WriteAllText(filename, textBoxDebugOutput.Text);
            MessageBox.Show($"Debug report saved as {filename}");
        }
    }
}
