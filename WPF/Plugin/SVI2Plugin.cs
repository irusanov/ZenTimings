using System;
using System.Collections.Generic;
using ZenStates.Core;
using ZenTimings.Common;

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
            if (cpuInstance != null && cpuInstance.Status == IOModule.LibStatus.OK)
            {
                Sensors = new List<Sensor>
                {
                    new Sensor("VSOC", 0),
                    new Sensor("VCORE", 1),
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
                    UpdateSensorValue(socPlaneValue, Sensors[0]);
                    UpdateSensorValue(vcorePlaneValue, Sensors[1]);

                    return true;
                }
            }

            return false;
        }

        private void ReadSensorValues(out uint socPlaneValue, out uint vcorePlaneValue)
        {
            socPlaneValue = cpuInstance.ReadDword(cpuInstance.info.svi2.socAddress);
            vcorePlaneValue = cpuInstance.ReadDword(cpuInstance.info.svi2.coreAddress);
        }

        private void UpdateSensorValue(uint planeValue, Sensor sensor)
        {
            uint vid = (planeValue >> 16) & 0xFF;
            sensor.Value = Convert.ToSingle(Utils.VidToVoltage(vid));

            Console.WriteLine($"{sensor.Name}: {sensor.Min} {sensor.Max}");
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
