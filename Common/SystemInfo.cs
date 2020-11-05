using System;

namespace ZenStates
{
    [Serializable]
    public class SystemInfo
    {
        private int ccdCount;
        private int threads;

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
            Model = 0;
            ExtendedModel = 0;
            NodesPerProcessor = 1;
            PackageType = 0;
            MbVendor = "";
            MbName = "";
            CpuName = "";
            CodeName = "";
            BiosVersion = "";
            SmuVersion = 0;
            FusedCoreCount = 2; // minimum cores
            Threads = 2;
            PatchLevel = 0;
        }

        public SystemInfo(uint cpuId, uint model, uint eModel, int nodes, uint pkgType,
            string mbVendor, string mbName, string cpuName, string codeName, string biosVersion,
            uint smuVersion, int fusedCoreCount, int threads, uint patchLevel)
        {
            CpuId = cpuId;
            Model = model;
            ExtendedModel = eModel;
            NodesPerProcessor = nodes;
            PackageType = pkgType;
            MbVendor = mbVendor ?? throw new ArgumentNullException(nameof(mbVendor));
            MbName = mbName ?? throw new ArgumentNullException(nameof(mbName));
            CpuName = cpuName ?? throw new ArgumentNullException(nameof(cpuName));
            CodeName = codeName ?? throw new ArgumentNullException(nameof(codeName));
            BiosVersion = biosVersion ?? throw new ArgumentNullException(nameof(biosVersion));
            SmuVersion = smuVersion;
            FusedCoreCount = fusedCoreCount;
            Threads = threads;
            PatchLevel = patchLevel;
        }

        public string CpuName { get; set; }
        public string CodeName { get; set; }
        public uint CpuId { get; set; }
        public uint Model { get; set; }
        public uint ExtendedModel { get; set; }
        public uint PackageType { get; set; }
        public int FusedCoreCount { get; set; }
        public int PhysicalCoreCount { get; private set; }
        public int NodesPerProcessor { get; set; }
        public int Threads { get; set; }
        public bool SMT { get; set; }

        public int CCDCount
        {
            get => ccdCount;
            set
            {
                if (value > 0)
                {
                    ccdCount = value;
                    CCXCount = ccdCount * 2;
                }
                else
                {
                    ccdCount = 1;
                    CCXCount = ccdCount;
                }

                NumCoresInCCX = FusedCoreCount / CCXCount;
                PhysicalCoreCount = CCXCount * 4;
            }
        }
        public int CCXCount { get; private set; }
        public int NumCoresInCCX { get; private set; }
        public string MbVendor { get; set; }
        public string MbName { get; set; }
        public string BiosVersion { get; set; }
        public uint SmuVersion { get; set; }
        public uint PatchLevel { get; set; }

        public string GetSmuVersionString()
        {
            return SmuVersionToString(SmuVersion);
        }

        public string GetCpuIdString()
        {
            return CpuId.ToString("X8").TrimStart('0');
        }
    }
}
