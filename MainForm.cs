using OpenLibSys;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Management;
using System.Threading;
using System.Windows.Forms;

namespace ZenTimings
{
    public partial class MainForm : Form
    {
        private enum CpuFamily
        {
            UNSUPPORTED = 0x0,
            FAMILY_17H = 0x17
        };

        private readonly Ols ols;

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

        private static void ExitApplication()
        {
            if (Application.MessageLoop)
            {
                Application.Exit();
            }
            else
            {
                Environment.Exit(1);
            }
        }

        private static uint GetBits(uint val, int offset, int n)
        {
            return (val >> offset) & ~(~0U << n);
        }

        private CpuFamily GetCpuFamily()
        {
            uint eax = 0, ebx = 0, ecx = 0, edx = 0;
            ols.Cpuid(0x00000000, ref eax, ref ebx, ref ecx, ref edx);
            if (ols.Cpuid(0x00000001, ref eax, ref ebx, ref ecx, ref edx) == 1)
            {
                CpuFamily family = (CpuFamily)(GetBits(eax, 8, 4) + GetBits(eax, 20, 7));
                return family;
            }
            else
            {
                MessageBox.Show("Could not get CPU Family. Aborting.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ExitApplication();
            }
            return CpuFamily.UNSUPPORTED;
        }

        public static object TryGetProperty(ManagementObject wmiObj, string propertyName)
        {
            object retval;
            try
            {
                retval = wmiObj.GetPropertyValue(propertyName);
            }
            catch (ManagementException ex)
            {
                retval = null;
            }
            return retval;
        }

        private void InitSystemInfo()
        {
            if (GetCpuFamily() != CpuFamily.FAMILY_17H)
            {
                MessageBox.Show("CPU is not supported.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ExitApplication();
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
            uint vid = ReadDword(0x5a054) >> 24;
            if (vid > 0) {
                return 1.55 - vid * 0.00625;
            }
            return 0;
        }

        private void ReadMemoryModulesInfo()
        {
            using (var searcher = new ManagementObjectSearcher("select * from Win32_PhysicalMemory"))
            {
                try
                {
                    var modules = new List<MemoryModule>();

                    foreach (var queryObject in searcher.Get().Cast<ManagementObject>())
                    {
                        ulong capacity = 0UL;
                        uint clockSpeed = 0U;
                        string partNumber = "N/A";
                        object temp;

                        temp = TryGetProperty(queryObject, "Capacity");
                        if (temp != null) capacity = (ulong)temp;

                        temp = TryGetProperty(queryObject, "ConfiguredClockSpeed");
                        if (temp != null) clockSpeed = (uint)temp;

                        temp = TryGetProperty(queryObject, "partNumber");
                        if (temp != null) partNumber = (string)temp;

                        modules.Add(new MemoryModule(partNumber, capacity, clockSpeed));
                    }

                    if (modules.Count > 0)
                    {
                        var totalCapacity = 0UL;

                        foreach (var module in modules)
                        {
                            totalCapacity += module.Capacity;
                        }

                        if (modules.FirstOrDefault().ClockSpeed != 0)
                            textBoxMCLK.Text = modules.FirstOrDefault().ClockSpeed.ToString();

                        if (totalCapacity != 0)
                            textBoxCapacity.Text = $"{totalCapacity / 1024 / (1024 * 1024)}GB";

                        textBoxPartNumber.Text = modules.FirstOrDefault().PartNumber;
                    }
                }
                catch { }
            }
        }

        private void ReadTimings()
        {
            uint offset = 0;
            bool enabled = false;

            // Get the offset by probing the IMC0 to IMC7
            // Reading from first one that matches should be sufficient,
            // because no bios allows setting different timings for the different channels.
            for (var i = 0u; i < 8u && !enabled; i++)
            {
                offset = i << 20;
                enabled = GetBits(ReadDword(0x50DF0 + offset), 19, 1) == 0;
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
            textBoxBGSAlt.Text = GetBits(bgsa0, 4, 7) > 0 || GetBits(bgsa1, 4, 7) > 0 ? "Enabled" : "Disabled";
            textBoxGDM.Text = GetBits(umcBase, 11, 1) > 0 ? "Enabled" : "Disabled";
            textBoxCmd2T.Text = GetBits(umcBase, 10, 1) > 0 ? "2T" : "1T";

            textBoxCL.Text = GetBits(num5, 0, 6).ToString();
            textBoxRCDWR.Text = GetBits(num5, 24, 6).ToString();
            textBoxRCDRD.Text = GetBits(num5, 16, 6).ToString();
            textBoxRP.Text = GetBits(num6, 16, 6).ToString();
            textBoxRAS.Text = GetBits(num5, 8, 7).ToString();
            textBoxRC.Text = GetBits(num6, 0, 8).ToString();
            textBoxRRDS.Text = GetBits(num7, 0, 5).ToString();
            textBoxRRDL.Text = GetBits(num7, 8, 5).ToString();
            textBoxFAW.Text = GetBits(num8, 0, 8).ToString();
            textBoxWTRS.Text = GetBits(num9, 8, 5).ToString();
            textBoxWTRL.Text = GetBits(num9, 16, 7).ToString();
            textBoxWR.Text = GetBits(num10, 0, 8).ToString();
            textBoxRDRDSCL.Text = GetBits(num12, 24, 6).ToString();
            textBoxWRWRSCL.Text = GetBits(num13, 24, 6).ToString();
            textBoxRFC.Text = GetBits(num23, 0, 11).ToString();
            textBoxCWL.Text = GetBits(num9, 0, 6).ToString();
            textBoxRTP.Text = GetBits(num7, 24, 5).ToString();
            textBoxRDWR.Text = GetBits(num14, 8, 5).ToString();
            textBoxWRRD.Text = GetBits(num14, 0, 4).ToString();
            textBoxRDRDSC.Text = GetBits(num12, 16, 4).ToString();
            textBoxRDRDSD.Text = GetBits(num12, 8, 4).ToString();
            textBoxRDRDDD.Text = GetBits(num12, 0, 4).ToString();
            textBoxWRWRSC.Text = GetBits(num13, 16, 4).ToString();
            textBoxWRWRSD.Text = GetBits(num13, 8, 4).ToString();
            textBoxWRWRDD.Text = GetBits(num13, 0, 4).ToString();
            textBoxCKE.Text = GetBits(num18, 24, 5).ToString();

            // VDDCR_SOC is not returning correct value
            // Same issue exists in Ryzen Master
            // MessageBox.Show(GetVDDCR_SOC().ToString("F2") + "V");
        }

        public MainForm()
        {
            try
            {
                ols = new Ols();

                CheckOlsStatus();
                InitSystemInfo();
                InitializeComponent();
            }
            catch (ApplicationException ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Dispose();
                Application.Exit();
            }
        }

        private void buttonScreenshot_Click(object sender, EventArgs e)
        {
            Rectangle bounds = Bounds;
            using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height))
            {
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.CopyFromScreen(new Point(bounds.Left, bounds.Top), Point.Empty, bounds.Size);
                }

                using (Form saveForm = new SaveForm(bitmap))
                {
                    saveForm.ShowDialog();
                }
            }
        }

        /// <summary>
        /// Gets triggered right before the form gets displayd
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainForm_Load(object sender, EventArgs e)
        {
            Text = $"{Application.ProductName} {Application.ProductVersion.Substring(0, Application.ProductVersion.LastIndexOf('.'))}";
#if DEBUG
            Text += " (debug)";
#endif
            ReadMemoryModulesInfo();
            ReadTimings();
        }
    }
}
