using System;
using System.Collections.Generic;
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
        private ManagementBaseObject pack;
        private string instanceName;
        ManagementObject classInstance;

        public DebugDialog(uint dramBaseAddr, List<MemoryModule> memModules, 
            MemoryConfig memCfg, SystemInfo systemInfo,
            byte[] biosMemCtrlTable, uint[] powerTable)
        {
            InitializeComponent();
            baseAddress = dramBaseAddr;
            modules = memModules;
            SI = systemInfo;
            MEMCFG = memCfg;
            PT = powerTable;
            BMC = biosMemCtrlTable;
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
                                result += $"{IDString[i]}: {ID[i]:X8}" + Environment.NewLine;
                            }
                        }
                        else
                        {
                            result += "<FAILED>" + Environment.NewLine;
                        }
                    }
                    catch { }

                    index++;
                    result += Environment.NewLine;
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

        private void Debug()
        {
            result = "";
            buttonDebugSave.Enabled = false;

            AddHeading("System Info");
            Type type = SI.GetType();
            PropertyInfo[] properties = type.GetProperties();

            foreach (PropertyInfo property in properties)
            {
                if (property.Name == "CpuId" || property.Name == "PatchLevel")
                    result += property.Name + ": " + $"{property.GetValue(SI, null):X8}" + Environment.NewLine;
                else if (property.Name == "SmuVersion")
                    result += property.Name + ": " + SI.GetSmuVersionString() + Environment.NewLine;
                else
                    result += property.Name + ": " + property.GetValue(SI, null) + Environment.NewLine;
            }
            result += Environment.NewLine;

            AddHeading("Memory Modules");
            foreach (MemoryModule module in modules)
            {
                result += $"{module.PartNumber} {module.Capacity / 1024 / (1024 * 1024)}GB {module.ClockSpeed}MHz" 
                    + Environment.NewLine;
            }
            result += Environment.NewLine;


            AddHeading("Memory Config");
            type = MEMCFG.GetType();
            properties = type.GetProperties();

            result += $"DRAM Base Address: {baseAddress:X8}" + Environment.NewLine;

            foreach (PropertyInfo property in properties)
            {
                result += property.Name + ": " + property.GetValue(MEMCFG, null) + Environment.NewLine;
            }
            result += Environment.NewLine;


            AddHeading("BIOS: Memory Controller Config");
            for (int i = 0; i < BMC.Length; ++i)
            {
                result += $"Index {i:D3}: {BMC[i]:X2} ({BMC[i]})" + Environment.NewLine;
            }
            result += Environment.NewLine;


            AddHeading("SMU: Power Table");
            for (int i = 0; i < PT.Length; ++i)
            {
                byte[] temp = BitConverter.GetBytes(PT[i]);
                result += $"Offset {i * 0x4:X3}: {BitConverter.ToSingle(temp, 0):F8}" + Environment.NewLine;
            }
            result += Environment.NewLine;


            AddHeading("WMI: Root Classes");
            List<string> namespaces = WMI.GetClassNamesWithinWmiNamespace(wmiScope);

            foreach (var ns in namespaces)
            {
                result += ns + Environment.NewLine;
            }
            result += Environment.NewLine;


            AddHeading("WMI: Instance Name");
            var wmiInstanceName = GetWmiInstanceName();
            if (wmiInstanceName.Length == 0)
                wmiInstanceName = "<FAILED>";
            result += wmiInstanceName + Environment.NewLine + Environment.NewLine;


            PrintWmiFunctions();

            textBoxDebugOutput.Text = result;
            buttonDebugSave.Enabled = true;
        }

        private void ButtonDebug_Click(object sender, EventArgs e)
        {
            Debug();
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
