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

        private Cpu _cpu;

        public SVI2Plugin(Cpu cpu)
        {
            InitializeSensors(cpu);
        }

        private void InitializeSensors(Cpu cpu)
        {
            if (cpu != null && cpu.Status == IOModule.LibStatus.OK)
            {
                _cpu = cpu;
                Sensors = new List<Sensor>
                {
                    new Sensor("VSOC", 0),
                    new Sensor("VCORE", 1),
                };
            }
        }

        public bool Update()
        {
            if (Sensors?.Count > 0 && _cpu != null)
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
            socPlaneValue = _cpu.ReadDword(_cpu.info.svi2.socAddress);
            vcorePlaneValue = _cpu.ReadDword(_cpu.info.svi2.coreAddress);
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
            _cpu = null;
            Sensors = null;
        }
    }
}
