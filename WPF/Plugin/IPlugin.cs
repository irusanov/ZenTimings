using System.Collections.Generic;
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
        void Open();
        void Close();
        bool Update();
    }
}
