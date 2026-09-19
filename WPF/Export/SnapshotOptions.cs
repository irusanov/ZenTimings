using System;
using System.Collections.Generic;
using ZenTimings.Common;
using ZenTimings.Plugin;

namespace ZenTimings.Export
{
    [Flags]
    public enum SnapshotSections
    {
        None = 0,
        System = 1 << 0,
        Modules = 1 << 1,
        Spd = 1 << 2,
        Timings = 1 << 3,
        Aod = 1 << 4,
        Apob = 1 << 5,
        BiosController = 1 << 6,
        PowerTable = 1 << 7,
        Sensors = 1 << 8,
        DimmTelemetry = 1 << 9,
        AsusWmi = 1 << 10,
        Registers = 1 << 11,

        // What the main window shows. Sections that do not apply to the installed memory type are left out on export.
        Default = System | Modules | Timings | Aod | Apob | BiosController | PowerTable | DimmTelemetry,
        All = System | Modules | Spd | Timings | Registers | Aod | Apob | BiosController | PowerTable | Sensors | DimmTelemetry | AsusWmi,
    }

    public enum SnapshotFormat
    {
        Json,
        Text,
        Html,
    }

    public sealed class SnapshotOptions
    {
        public SnapshotSections Sections { get; set; } = SnapshotSections.Default;

        public bool IncludeSerialNumbers { get; set; }

        public bool IncludeLegend { get; set; } = true;

        public bool Has(SnapshotSections section) => (Sections & section) == section;
    }

    /// <summary>
    /// Data owned by the main window which is not reachable through CpuSingleton.
    /// </summary>
    public sealed class SnapshotSource
    {
        public BiosMemController.Resistances? BiosMemConfig { get; set; }

        public List<AsusSensorInfo> AsusSensors { get; set; }

        public List<IPlugin> Plugins { get; set; }

        public DateTime? LastRefreshUtc { get; set; }

        public bool AutoRefreshActive { get; set; }
    }
}
