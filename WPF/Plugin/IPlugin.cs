using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZenTimings.Common;

namespace ZenTimings.Plugin
{
    public interface IPlugin
    {
        string Name { get; }
        string Description { get; }
        string Author { get; }
        string Version { get; }
        List<Sensor> Sensors { get; }
        //void Init();
        bool Update();
    }
}
