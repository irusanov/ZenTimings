using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ZenStates.Core;
using ZenTimings.Common;

namespace ZenTimings.Plugin
{
    public class OHWMPlugin : IPlugin
    {
        internal dynamic computer;
        internal Assembly assembly;
        internal Type Mainboard;
        public string Name => "OpenHardwareMonitor Plugin";

        public string Description => "A wrapper around OpenHardwareMonitor's DLL";

        public string Author => "Ivan Rusanov";

        public string Version => "1.0";

        public List<Sensor> Sensors { get; internal set; }

        public OHWMPlugin()
        {
            try
            {
                assembly = Assembly.LoadFrom("OpenHardwareMonitorLib.dll");
                Type type = assembly.GetType("OpenHardwareMonitor.Hardware.Computer");
                computer = Activator.CreateInstance(type);
                Mainboard = assembly.GetType("OpenHardwareMonitor.Hardware.Mainboard.Mainboard");
            }
            catch (Exception ex)
            {
                Close();
            }
        }

        public void Close()
        {
            computer?.Close();
            assembly = null;
        }

        public void Open()
        {
            Sensors = new List<Sensor>();
            computer.MainboardEnabled = true;
            computer.Open();

            foreach (var hardware in computer.Hardware)
            {
                if (GetPropValue(hardware, "HardwareType").ToString() == "Mainboard")
                {
                    foreach (var subHardware in GetPropValue(hardware, "SubHardware"))
                    {
                        Type type = subHardware.GetType();
                        // subHardware.Update();

                        type.InvokeMember("Update",
                          BindingFlags.Default | BindingFlags.InvokeMethod,
                          null,
                          subHardware,
                          null);

                        foreach (var subsensor in GetPropValue(subHardware, "Sensors"))
                        {
                            if (GetPropValue(subsensor, "SensorType").ToString() == "Voltage")
                            {
                                // Console.WriteLine($"----Index: {GetPropValue(subsensor, "Index")}, {GetPropValue(subsensor, "SensorType")}, Name: {GetPropValue(subsensor, "Name")}, Value: {GetPropValue(subsensor, "Value")}");
                                try
                                {
                                    Sensors.Add(
                                        new Sensor((string)GetPropValue(subsensor, "Name"), (int)GetPropValue(subsensor, "Index"))
                                        {
                                            Value = GetPropValue(subsensor, "Value") ?? 0,
                                        }
                                    );
                                }
                                catch { }
                            }
                        }
                    }
                }
            }
        }

        public bool Update()
        {
            foreach (var hardware in computer.Hardware)
            {
                if (GetPropValue(hardware, "HardwareType").ToString() == "Mainboard")
                {
                    foreach (var subHardware in GetPropValue(hardware, "SubHardware"))
                    {
                        Type type = subHardware.GetType();
                        // subHardware.Update();

                        type.InvokeMember("Update",
                          BindingFlags.Default | BindingFlags.InvokeMethod,
                          null,
                          subHardware,
                          null);

                        foreach (var subsensor in GetPropValue(subHardware, "Sensors"))
                        {
                            if (GetPropValue(subsensor, "SensorType").ToString() == "Voltage")
                            {
                                Sensors[(int)GetPropValue(subsensor, "Index")].Value = GetPropValue(subsensor, "Value") ?? 0;
                            }
                        }
                    }
                }
            }

            return true;
        }

        private static object GetPropValue(object src, string propName)
        {
            return src.GetType().GetProperty(propName)?.GetValue(src, null);
        }
    }
}
