using System;

namespace ZenStatesDebugTool
{
    [Serializable]
    public class SystemInfo
    {
        private static string SmuVersionToString(uint version)
        {
            string[] versionString = new string[3];
            versionString[0] = ((version & 0x00FF0000) >> 16).ToString("D2");
            versionString[1] = ((version & 0x0000FF00) >> 8).ToString("D2");
            versionString[2] = (version & 0x000000FF).ToString("D2");

            return string.Join(".", versionString);
        }

        public SystemInfo()
        {
            CpuId = 0;
            MbVendor = "";
            MbName = "";
            CpuName = "";
            BiosVersion = "";
            SmuVersion = 0;
        }

        public SystemInfo(uint cpuId, string mbVendor, string mbName, string cpuName, string biosVersion, uint smuVersion)
        {
            CpuId = cpuId;
            MbVendor = mbVendor ?? throw new ArgumentNullException(nameof(mbVendor));
            MbName = mbName ?? throw new ArgumentNullException(nameof(mbName));
            CpuName = cpuName ?? throw new ArgumentNullException(nameof(cpuName));
            BiosVersion = biosVersion ?? throw new ArgumentNullException(nameof(biosVersion));
            SmuVersion = smuVersion;
        }

        public uint CpuId { get; set; }
        public string MbVendor { get; set; }
        public string MbName { get; set; }
        public string CpuName { get; set; }
        public string BiosVersion { get; set; }
        public uint SmuVersion { get; set; }

        public string GetSmuVersionString()
        {
            return SmuVersionToString(this.SmuVersion);
        }

        public string GetCpuIdString()
        {
            return CpuId.ToString("X16").TrimStart('0');
        }
    }
}
