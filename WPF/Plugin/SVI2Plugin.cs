using System;
using System.Collections.Generic;
using System.Diagnostics;
using ZenStates.Core;
using ZenStates.Core.Drivers;
using ZenStates.Core.Hardware;
using ZenStates.Core.Hardware.MutexLock;

namespace ZenTimings.Plugin
{
    public class SVI2Plugin : IPlugin
    {
        private int timeout = 20;
        private const string VERSION = "1.1";

        public string Name => "SVI2 Sensors";

        public string Description => "";

        public string Author => "";

        public string Version => VERSION;

        public List<Sensor> Sensors { get; private set; }

        private Cpu cpuInstance;

        public SVI2Plugin(Cpu cpu)
        {
            cpuInstance = cpu;
            InitializeSensors();
        }

        private void InitializeSensors()
        {
            if (cpuInstance != null && cpuInstance.Status == IODriver.LibStatus.OK)
            {
                Sensors = new List<Sensor>
                {
                    new Sensor("VSOC", 0, SensorType.Voltage),
                    new Sensor("VCORE", 1, SensorType.Voltage),
                };
            }
        }

        public bool Update()
        {
            if (Sensors?.Count > 0 && cpuInstance != null)
            {
                uint socPlaneValue;
                uint vcorePlaneValue;
                do
                {
                    ReadSensorValues(out socPlaneValue, out vcorePlaneValue);
                } while ((socPlaneValue & 0xFF00) != 0 && (vcorePlaneValue & 0xFF00) != 0 && --timeout > 0);

                if (timeout > 0)
                {
                    UpdateSensorValue(socPlaneValue, Sensors[0].Index);
                    UpdateSensorValue(vcorePlaneValue, Sensors[1].Index);

                    return true;
                }
            }

            return false;
        }

        private void ReadSensorValues(out uint socPlaneValue, out uint vcorePlaneValue)
        {
            socPlaneValue = 0;
            vcorePlaneValue = 0;

            using (new PciBusLock())
            {
                socPlaneValue = cpuInstance.ReadDwordNoLock(cpuInstance.info.svi2.socAddress);
                vcorePlaneValue = cpuInstance.ReadDwordNoLock(cpuInstance.info.svi2.coreAddress);
            }
        }

        private void UpdateSensorValue(uint planeValue, int sensorIndex)
        {
            uint vid = (planeValue >> 16) & 0xFF;
            Sensors[sensorIndex].Value = (float)Utils.VidToVoltage(vid);

            Debug.WriteLine($"{Sensors[sensorIndex].Name}: {Sensors[sensorIndex].Min} {Sensors[sensorIndex].Max}");
        }

        public void Open()
        {
            throw new NotImplementedException();
        }

        public void Close()
        {
            cpuInstance = null;
            Sensors = null;
        }
    }
}
