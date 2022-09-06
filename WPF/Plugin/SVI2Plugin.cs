using System;
using System.Collections.Generic;
using ZenStates.Core;
using ZenTimings.Common;

namespace ZenTimings.Plugin
{
    public class SVI2Plugin : IPlugin
    {
        internal Cpu _cpu;
        internal int timeout = 20;
        internal const string VERSION = "1.1";

        public string Name => "SVI2 Sensors";

        public string Description => "";

        public string Author => "";

        public string Version => VERSION;

        public List<Sensor> Sensors { get; private set; }

        public SVI2Plugin(Cpu cpu)
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
            else
            {
                throw new Exception("CPU module is not initialized");
            }
        }

        public bool Update()
        {
            if (Sensors.Count > 0)
            {
                uint soc_plane_value;
                uint vcore_plane_value;
                do
                {
                    soc_plane_value = _cpu.ReadDword(_cpu.info.svi2.socAddress);
                    vcore_plane_value = _cpu.ReadDword(_cpu.info.svi2.coreAddress);
                } while ((soc_plane_value & 0xFF00) != 0 && (vcore_plane_value & 0xFF00) != 0 && --timeout > 0);

                if (timeout > 0)
                {
                    uint socVid = (soc_plane_value >> 16) & 0xFF;
                    Sensors[0].Value = Convert.ToSingle(Utils.VidToVoltage(socVid));

                    Console.WriteLine("Vsoc: " + Sensors[0].Min + " " + Sensors[0].Max);

                    uint coreVid = (vcore_plane_value >> 16) & 0xFF;
                    Sensors[1].Value = Convert.ToSingle(Utils.VidToVoltage(coreVid));

                    Console.WriteLine("Vcore: " + Sensors[1].Min + " " + Sensors[1].Max);
                    return true;
                }
            }

            return false;
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
