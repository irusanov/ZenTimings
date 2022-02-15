using System;
using System.Collections.Generic;
using ZenStates.Core;
using ZenTimings.Common;

namespace ZenTimings.Plugin
{
    public class SVI2Plugin : IPlugin
    {
        internal readonly Cpu _cpu;
        internal int timeout = 20;
        internal const string VERSION = "1.0";

        public string Name => "SVI2 Sensors";

        public string Description => "";

        public string Author => "";

        public string Version => VERSION;

        public List<Sensor> Sensors { get; }

        public SVI2Plugin(Cpu cpu)
        {
            if (cpu != null && cpu.Status == Utils.LibStatus.OK)
            {
                _cpu = cpu;
                Sensors = new List<Sensor>
                {
                    new Sensor("VSOC", 0)
                };
            }
            else
            {
                throw new Exception("CPU module is not initialized");
            }
        }

        /*public void Init()
        {
            throw new NotImplementedException();
        }*/

        public bool Update()
        {
            uint soc_plane_value;
            do
            {
                soc_plane_value = _cpu.ReadDword(_cpu.info.svi2.socAddress);
            } while ((soc_plane_value & 0xFF00) != 0 && --timeout > 0);

            if (timeout > 0)
            {
                uint socVid = (soc_plane_value >> 16) & 0xFF;
                Sensors[0].Value = Convert.ToSingle(_cpu.utils.VidToVoltage(socVid));

                Console.WriteLine(Sensors[0].Min + " " + Sensors[0].Max);
                return true;
            }

            return false;
        }
    }
}
