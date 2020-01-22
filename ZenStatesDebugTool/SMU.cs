using System;
using System.Collections.Generic;

namespace ZenStatesDebugTool
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "<Pending>")]
    public abstract class SMU
    {
        public enum CPUType : int
        {
            Unsupported = 0,
            DEBUG,
            SummitRidge,
            Threadripper,
            RavenRidge,
            PinnacleRidge,
            Picasso,
            Fenghuang,
            Matisse,
            Rome,
            Renoir
        };

        public enum Status : int
        {
            OK                      = 0x1,
            FAILED                  = 0xFF,
            UNKNOWN_CMD             = 0xFE,
            CMD_REJECTED_PREREQ     = 0xFD,
            CMD_REJECTED_BUSY       = 0xFC
        }

        public SMU()
        {
            Version = 0;
            // SMU
            SMU_PCI_ADDR = 0x00000000;
            SMU_OFFSET_ADDR = 0xB8;
            SMU_OFFSET_DATA = 0xBC;

            SMU_ADDR_MSG = 0x03B10528;
            SMU_ADDR_RSP = 0x03B10564;
            SMU_ADDR_ARG0 = 0x03B10598;
            SMU_ADDR_ARG1 = SMU_ADDR_ARG0 + 0x4;

            // SMU Messages
            SMC_MSG_TestMessage = 0x1;
            SMC_MSG_GetSmuVersion = 0x2;
        }

        public uint Version { get; set; }
        public uint SMU_PCI_ADDR { get; protected set; }
        public uint SMU_OFFSET_ADDR { get; protected set; }
        public uint SMU_OFFSET_DATA { get; protected set; }

        public uint SMU_ADDR_MSG { get; protected set; }
        public uint SMU_ADDR_RSP { get; protected set; }
        public uint SMU_ADDR_ARG0 { get; protected set; }

        public uint SMU_ADDR_ARG1 { get; protected set; }

        public uint SMC_MSG_TestMessage { get; protected set; }
        public uint SMC_MSG_GetSmuVersion { get; protected set; }
    }

    // inherit the base class and define the new values in ctor
    public class SummitRidgeSettings : SMU
    {
        public SummitRidgeSettings() { }
    }

    // Ryzen 3000 (Matisse), TR 3000 (Castle Peak), EPYC 2 (Rome), Renoir
    public class Zen2Settings : SMU
    {
        public Zen2Settings()
        {
            SMU_ADDR_MSG = 0x03B10524;
            SMU_ADDR_RSP = 0x03B10570;
            SMU_ADDR_ARG0 = 0x03B10A40;
            SMU_ADDR_ARG1 = SMU_ADDR_ARG0 + 0x4;
        }
    }

    // Zen+ (Pinnacle Ridge), TR2 (Colfax) 
    public class ZenPSettings : SMU
    {
        public ZenPSettings()
        {
            SMU_ADDR_MSG = 0x03B1051C;
            SMU_ADDR_RSP = 0x03B10568;
            SMU_ADDR_ARG0 = 0x03B10590;
            SMU_ADDR_ARG1 = SMU_ADDR_ARG0 + 0x4;
        }
    }

    // Raven Ridge, Raven Ridge 2, Picasso
    public class RavenRidgeSettings : SMU
    {
        public RavenRidgeSettings()
        {
            SMU_ADDR_MSG = 0x03B10A20;
            SMU_ADDR_RSP = 0x03B10A80;
            SMU_ADDR_ARG0 = 0x03B10A88;
            SMU_ADDR_ARG1 = SMU_ADDR_ARG0 + 0x4;
        }
    }

    // Matisse, Renoir, CastlePeak and Rome share the same settings
    // CastlePeak (Threadripper 3000 series) shares the same CPUID as the server counterpart Rome
    public static class GetMaintainedSettings
    {
        private static readonly Dictionary<SMU.CPUType, SMU> settings = new Dictionary<SMU.CPUType, SMU>()
        {
            { SMU.CPUType.SummitRidge, new SummitRidgeSettings() },
            { SMU.CPUType.RavenRidge, new RavenRidgeSettings() },
            { SMU.CPUType.Picasso, new RavenRidgeSettings() },
            { SMU.CPUType.PinnacleRidge, new ZenPSettings() },
            { SMU.CPUType.Matisse, new Zen2Settings() },
            { SMU.CPUType.Rome, new Zen2Settings() },
            { SMU.CPUType.Renoir, new Zen2Settings() },
        };

        public static SMU GetByType(SMU.CPUType type)
        {
            if (!settings.TryGetValue(type, out SMU output))
            {
                throw new NotImplementedException();
            }
            return output;
        }
    }

    public static class GetSMUStatus
    {
        private static readonly Dictionary<SMU.Status, String> status = new Dictionary<SMU.Status, string>()
        {
            { SMU.Status.OK, "OK" },
            { SMU.Status.FAILED, "Failed" },
            { SMU.Status.UNKNOWN_CMD, "Unknown Command" },
            { SMU.Status.CMD_REJECTED_PREREQ, "CMD Rejected Prereq" },
            { SMU.Status.CMD_REJECTED_BUSY, "CMD Rejected Busy" }
        };

        public static string GetByType(SMU.Status type)
        {
            if (!status.TryGetValue(type, out string output))
            {
                return "Unknown Status";
            }
            return output;
        }
    }
}
