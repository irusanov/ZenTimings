using OpenHardwareMonitor.Hardware;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using ZenTimings.Common;
using ZenTimings.Plugin;

namespace OHWMPlugin
{
    public class OHWMPlugin : IPlugin
    {
        internal Computer computer;
        public string Name => "OpenHardwareMonitor Plugin";

        public string Description => "A wrapper around OpenHardwareMonitor's DLL";

        public string Author => "Ivan Rusanov";

        public string Version => "1.0";

        public List<Sensor> Sensors { get; internal set; }

        public OHWMPlugin()
        {
            Assembly assembly = Assembly.LoadFrom("OpenHardwareMonitorLib.dll");
        }

        public void Close()
        {
            throw new NotImplementedException();
        }

        public void Open()
        {
            computer = new Computer()
            {
                //CPUEnabled = true,
                //RAMEnabled = true,
                MainboardEnabled = true,
                //FanControllerEnabled = true,
            };

            computer.Open();
            int index = 0;

            foreach (var hardware in computer.Hardware)
            {
                if (hardware.HardwareType == HardwareType.Mainboard)
                {
                    foreach (var subHardware in hardware.SubHardware)
                    {
                        foreach (var subsensor in subHardware.Sensors)
                        {
                            //if (subsensor.SensorType == SensorType.Voltage)
                            Console.WriteLine($"----{subsensor.SensorType}, Name: {subsensor.Name}, Value: {subsensor.Value}");
                            Sensors.Add(new Sensor(subsensor.Name, index));
                            index++;
                        }
                    }
                }
            }
        }

        public bool Update()
        {
            int index = 0;

            foreach (var hardware in computer.Hardware)
            {
                if (hardware.HardwareType == HardwareType.Mainboard)
                {
                    foreach (var subHardware in hardware.SubHardware)
                    {
                        subHardware.Update();

                        foreach (var subsensor in subHardware.Sensors)
                        {
                            Sensors[index].Value = subsensor.Value;
                        }
                    }
                }
            }

            return true;
        }
    }
}
