using OpenLibSys;
using System;
using System.Linq;
using System.Management;
using System.Threading;
using System.Windows.Forms;

namespace ZenTimings
{
    public partial class MainForm : Form
    {
        // The only thing we need from SMU for now
        private enum CPUType : int
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
        private readonly Ols ols;
        private CPUType cpuType = CPUType.Unsupported;
        private readonly Mutex hMutexPci;

        private void CheckOlsStatus()
        {
            // Check support library status
            switch (ols.GetStatus())
            {
                case (uint)Ols.Status.NO_ERROR:
                    break;
                case (uint)Ols.Status.DLL_NOT_FOUND:
                    throw new ApplicationException("WinRing DLL_NOT_FOUND");
                case (uint)Ols.Status.DLL_INCORRECT_VERSION:
                    throw new ApplicationException("WinRing DLL_INCORRECT_VERSION");
                case (uint)Ols.Status.DLL_INITIALIZE_ERROR:
                    throw new ApplicationException("WinRing DLL_INITIALIZE_ERROR");
            }

            // Check WinRing0 status
            switch (ols.GetDllStatus())
            {
                case (uint)Ols.OlsDllStatus.OLS_DLL_NO_ERROR:
                    break;
                case (uint)Ols.OlsDllStatus.OLS_DLL_DRIVER_NOT_LOADED:
                    throw new ApplicationException("WinRing OLS_DRIVER_NOT_LOADED");
                case (uint)Ols.OlsDllStatus.OLS_DLL_UNSUPPORTED_PLATFORM:
                    throw new ApplicationException("WinRing OLS_UNSUPPORTED_PLATFORM");
                case (uint)Ols.OlsDllStatus.OLS_DLL_DRIVER_NOT_FOUND:
                    throw new ApplicationException("WinRing OLS_DLL_DRIVER_NOT_FOUND");
                case (uint)Ols.OlsDllStatus.OLS_DLL_DRIVER_UNLOADED:
                    throw new ApplicationException("WinRing OLS_DLL_DRIVER_UNLOADED");
                case (uint)Ols.OlsDllStatus.OLS_DLL_DRIVER_NOT_LOADED_ON_NETWORK:
                    throw new ApplicationException("WinRing DRIVER_NOT_LOADED_ON_NETWORK");
                case (uint)Ols.OlsDllStatus.OLS_DLL_UNKNOWN_ERROR:
                    throw new ApplicationException("WinRing OLS_DLL_UNKNOWN_ERROR");
            }
        }

        private uint GetCpuInfo()
        {
            uint eax = 0, ebx = 0, ecx = 0, edx = 0;
            ols.CpuidPx(0x00000000, ref eax, ref ebx, ref ecx, ref edx, (UIntPtr)1);
            if (ols.CpuidPx(0x00000001, ref eax, ref ebx, ref ecx, ref edx, (UIntPtr)1) == 1)
            {
                return eax;
            }
            return 0;
        }

        private void InitSystemInfo()
        {
            // CPU Check. Compare family, model, ext family, ext model. Ignore stepping/revision
            switch (GetCpuInfo() & 0xFFFFFFF0)
            {
                case 0x00800F10: // CPU \ Zen \ Summit Ridge \ ZP - B0 \ 14nm
                case 0x00800F00: // CPU \ Zen \ Summit Ridge \ ZP - A0 \ 14nm
                    cpuType = CPUType.SummitRidge;
                    break;
                case 0x00800F80: // CPU \ Zen + \ Pinnacle Ridge \ Colfax 12nm
                    cpuType = CPUType.PinnacleRidge;
                    break;
                case 0x00810F80: // APU \ Zen + \ Picasso \ 12nm
                    cpuType = CPUType.Picasso;
                    break;
                case 0x00810F00: // APU \ Zen \ Raven Ridge \ RV - A0 \ 14nm
                case 0x00810F10: // APU \ Zen \ Raven Ridge \ 14nm
                case 0x00820F00: // APU \ Zen \ Raven Ridge 2 \ RV2 - A0 \ 14nm
                    cpuType = CPUType.RavenRidge;
                    break;
                case 0x00870F10: // CPU \ Zen2 \ Matisse \ MTS - B0 \ 7nm + 14nm I/ O Die
                case 0x00870F00: // CPU \ Zen2 \ Matisse \ MTS - A0 \ 7nm + 14nm I/ O Die
                    cpuType = CPUType.Matisse;
                    break;
                case 0x00830F00:
                case 0x00830F10: // CPU \ Epyc 2 \ Rome \ Treadripper 2 \ Castle Peak 7nm
                    cpuType = CPUType.Rome;
                    break;
                case 0x00850F00:
                    cpuType = CPUType.Fenghuang;
                    break;
                case 0x00850F10: // APU \ Renoir
                    cpuType = CPUType.Renoir;
                    break;
                default:
                    cpuType = CPUType.Unsupported;
#if DEBUG
                    cpuType = CPUType.DEBUG;
#endif
                    break;
            }

            if (cpuType == CPUType.Unsupported)
            {
                throw new ApplicationException("CPU is not supported.");
            }
        }

        private uint ReadDword(uint value)
        {
            ols.WritePciConfigDword(0U, 0xB8, value);
            return ols.ReadPciConfigDword(0U, 0xBC);
        }

        private double GetVDDCR_SOC()
        {
            // The address is different for each gen
            uint vid = ReadDword(0x5a058) >> 24;
            if (vid > 0) {
                return 1.55 - vid * 0.00625;
            }
            return 0;
        }

        private void GetMemoryInfo()
        {
            try
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PhysicalMemory");
                ManagementObjectCollection moc = searcher.Get();
                ManagementObject mo = moc.OfType<ManagementObject>().FirstOrDefault();
                ulong capacity = 0;

                foreach(ManagementObject obj in moc)
                {
                    capacity += (ulong)obj["Capacity"];
                }

                textBoxCapacity.Text = $"{(capacity / 1024 / 1024 / 1000).ToString()}GB";
                textBoxPartNumber.Text = (string)mo["PartNumber"];
                textBoxMCLK.Text = ((uint)mo["ConfiguredClockSpeed"]).ToString();

                if (searcher != null) searcher.Dispose();
                if (mo != null) mo.Dispose();
                if (moc != null) moc.Dispose();
            }
            catch { }
        }

        private void ReadTimings()
        {
            uint offset = 0;
            uint i = 0;
            bool enabled = false;

            // Get the offset by probing the IMC0 to IMC7
            // Reading from first one that matches should be sufficient,
            // because no bios allow setting different timings for the different channels.
            while (i < 8 && !enabled)
            {
                offset = (uint)i << 20;
                enabled = ((ReadDword(0x50DF0 + offset) >> 19) & 1) != 1;
                i++;
            }

            uint umcBase = ReadDword(0x50200 + offset);
            uint bgsa0 = ReadDword(0x500D0 + offset);
            uint bgsa1 = ReadDword(0x500D4 + offset);
            uint bgs0 = ReadDword(0x50050 + offset);
            uint bgs1 = ReadDword(0x50058 + offset);
            uint num5 = ReadDword(0x50204 + offset);
            uint num6 = ReadDword(0x50208 + offset);
            uint num7 = ReadDword(0x5020C + offset);
            uint num8 = ReadDword(0x50210 + offset);
            uint num9 = ReadDword(0x50214 + offset);
            uint num10 = ReadDword(0x50218 + offset);
            int num11 = (int)ReadDword(0x5021C + offset);
            uint num12 = ReadDword(0x50220 + offset);
            uint num13 = ReadDword(0x50224 + offset);
            uint num14 = ReadDword(0x50228 + offset);
            int num15 = (int)ReadDword(0x50230 + offset);
            int num16 = (int)ReadDword(0x50234 + offset);
            int num17 = (int)ReadDword(0x50250 + offset);
            uint num18 = ReadDword(0x50254 + offset);
            uint num19 = ReadDword(0x50258 + offset);
            uint num20 = ReadDword(0x50260 + offset);
            uint num21 = ReadDword(0x50264 + offset);
            int num22 = (int)ReadDword(0x5028C + offset);
            uint num23 = (int)num20 != (int)num21 ? (num20 != 0x21060138 ? num20 : num21) : num20;

            textBoxBGS.Text = (bgs0 == 0x87654321 && bgs1 == 0x87654321) ? "Disabled" : "Enabled";
            textBoxBGSAlt.Text = ((bgsa0 & 0x7F0) >> 4 > 0 || (bgsa1 & 0x7F0) >> 4 > 0) ? "Enabled" : "Disabled";
            textBoxGDM.Text = (umcBase & 0x800) >> 11 > 0 ? "Enabled" : "Disabled";
            textBoxCmd2T.Text = (umcBase & 0x400) >> 10 > 0 ? "2T" : "1T";

            textBoxCL.Text = (num5 & 0x3F).ToString();
            textBoxRCDWR.Text = ((num5 & 0x3F000000) >> 24).ToString();
            textBoxRCDRD.Text = ((num5 & 0x3F0000) >> 16).ToString();
            textBoxRP.Text = ((num6 & 0x3F0000) >> 16).ToString();
            textBoxRAS.Text = ((num5 & 0x7F00) >> 8).ToString();
            textBoxRC.Text = (num6 & 0xFF).ToString();
            textBoxRRDS.Text = (num7 & 0x1F).ToString();
            textBoxRRDL.Text = ((num7 & 0x1F00) >> 8).ToString();
            textBoxFAW.Text = (num8 & 0xFF).ToString();
            textBoxWTRS.Text = ((num9 & 0x1F00) >> 8).ToString();
            textBoxWTRL.Text = ((num9 & 0x7F0000) >> 16).ToString();
            textBoxWR.Text = (num10 & 0xFF).ToString();
            textBoxRDRDSCL.Text = ((num12 & 0x3F000000) >> 24).ToString();
            textBoxWRWRSCL.Text = ((num13 & 0x3F000000) >> 24).ToString();
            textBoxRFC.Text = (num23 & 0x7FF).ToString();
            textBoxCWL.Text = (num9 & 0x3F).ToString();
            textBoxRTP.Text = ((num7 & 0x1F000000) >> 24).ToString();
            textBoxRDWR.Text = ((num14 & 0x1F00) >> 8).ToString();
            textBoxWRRD.Text = (num14 & 0xF).ToString();
            textBoxRDRDSC.Text = ((num12 & 0xF0000) >> 16).ToString();
            textBoxRDRDSD.Text = ((num12 & 0xF00U) >> 8).ToString();
            textBoxRDRDDD.Text = (num12 & 0xF).ToString();
            textBoxWRWRSC.Text = ((num13 & 0xF0000) >> 16).ToString();
            textBoxWRWRSD.Text = ((num13 & 0xF00) >> 8).ToString();
            textBoxWRWRDD.Text = (num13 & 0xF).ToString();
            textBoxCKE.Text = ((num18 & 0x1F000000) >> 24).ToString();

            // VDDCR_SOC is not returning correct value
            // Same issue exists in Ryzen Master
            // MessageBox.Show(GetVDDCR_SOC().ToString("F2") + "V");
        }

        public MainForm()
        {
            try
            {
                ols = new Ols();
                hMutexPci = new Mutex();

                CheckOlsStatus();
                InitSystemInfo();
                InitializeComponent();
                GetMemoryInfo();
                ReadTimings();
            }
            catch (ApplicationException ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Dispose();
                Application.Exit();
            }
        }
    }
}
