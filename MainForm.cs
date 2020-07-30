using OpenLibSys;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Management;
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
        private readonly List<MemoryModule> modules = new List<MemoryModule>();

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
            if (vid > 0)
            {
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
            uint timings5 = ReadDword(0x50204 + offset);
            uint timings6 = ReadDword(0x50208 + offset);
            uint timings7 = ReadDword(0x5020C + offset);
            uint timings8 = ReadDword(0x50210 + offset);
            uint timings9 = ReadDword(0x50214 + offset);
            uint timings10 = ReadDword(0x50218 + offset);
            uint timings11 = ReadDword(0x5021C + offset);
            uint timings12 = ReadDword(0x50220 + offset);
            uint timings13 = ReadDword(0x50224 + offset);
            uint timings14 = ReadDword(0x50228 + offset);
            uint timings15 = ReadDword(0x50230 + offset);
            uint timings16 = ReadDword(0x50234 + offset);
            uint timings17 = ReadDword(0x50250 + offset);
            uint timings18 = ReadDword(0x50254 + offset);
            uint timings19 = ReadDword(0x50258 + offset);
            uint timings20 = ReadDword(0x50260 + offset);
            uint timings21 = ReadDword(0x50264 + offset);
            uint timings22 = ReadDword(0x5028C + offset);
            uint timings23 = timings20 != timings21 ? (timings20 != 0x21060138 ? timings20 : timings21) : timings20;

            textBoxBGS.Text = (bgs0 == 0x87654321 && bgs1 == 0x87654321) ? "Disabled" : "Enabled";
            textBoxBGSAlt.Text = GetBits(bgsa0, 4, 7) > 0 || GetBits(bgsa1, 4, 7) > 0 ? "Enabled" : "Disabled";
            textBoxGDM.Text = GetBits(umcBase, 11, 1) > 0 ? "Enabled" : "Disabled";
            textBoxCmd2T.Text = GetBits(umcBase, 10, 1) > 0 ? "2T" : "1T";

            textBoxCL.Text = GetBits(timings5, 0, 6).ToString();
            textBoxRCDWR.Text = GetBits(timings5, 24, 6).ToString();
            textBoxRCDRD.Text = GetBits(timings5, 16, 6).ToString();
            textBoxRP.Text = GetBits(timings6, 16, 6).ToString();
            textBoxRAS.Text = GetBits(timings5, 8, 7).ToString();
            textBoxRC.Text = GetBits(timings6, 0, 8).ToString();
            textBoxRRDS.Text = GetBits(timings7, 0, 5).ToString();
            textBoxRRDL.Text = GetBits(timings7, 8, 5).ToString();
            textBoxFAW.Text = GetBits(timings8, 0, 8).ToString();
            textBoxWTRS.Text = GetBits(timings9, 8, 5).ToString();
            textBoxWTRL.Text = GetBits(timings9, 16, 7).ToString();
            textBoxWR.Text = GetBits(timings10, 0, 8).ToString();
            textBoxRDRDSCL.Text = GetBits(timings12, 24, 6).ToString();
            textBoxWRWRSCL.Text = GetBits(timings13, 24, 6).ToString();

            uint trfc = GetBits(timings23, 0, 11);
            uint trfc2 = GetBits(timings23, 11, 11);
            uint trfc4 = GetBits(timings23, 22, 11);
            textBoxRFC.Text = trfc.ToString();
            textBoxRFC2.Text = trfc2.ToString();
            textBoxRFC4.Text = trfc4.ToString();

            float mclk = modules.FirstOrDefault().ClockSpeed * 1.0f;
            // Fallback to ratio when ConfiguredClockSpeed fails
            if (mclk == 0)
            {
                mclk = GetBits(umcBase, 0, 7) / 3.0f * 200;
                textBoxMCLK.Text = Convert.ToInt32(mclk).ToString();
            }

            textBoxRFCns.Text = Convert.ToInt32(trfc * 2000 / mclk).ToString();

            ToolTip tooltip = new ToolTip();
            tooltip.SetToolTip(textBoxRFC2, $"{Convert.ToInt32(trfc2 * 2000 / mclk)} ns");
            tooltip.SetToolTip(textBoxRFC4, $"{Convert.ToInt32(trfc4 * 2000 / mclk)} ns");

            textBoxCWL.Text = GetBits(timings9, 0, 6).ToString();
            textBoxRTP.Text = GetBits(timings7, 24, 5).ToString();
            textBoxRDWR.Text = GetBits(timings14, 8, 5).ToString();
            textBoxWRRD.Text = GetBits(timings14, 0, 4).ToString();
            textBoxRDRDSC.Text = GetBits(timings12, 16, 4).ToString();
            textBoxRDRDSD.Text = GetBits(timings12, 8, 4).ToString();
            textBoxRDRDDD.Text = GetBits(timings12, 0, 4).ToString();
            textBoxWRWRSC.Text = GetBits(timings13, 16, 4).ToString();
            textBoxWRWRSD.Text = GetBits(timings13, 8, 4).ToString();
            textBoxWRWRDD.Text = GetBits(timings13, 0, 4).ToString();
            textBoxCKE.Text = GetBits(timings18, 24, 5).ToString();

            textBoxSTAG.Text = GetBits(timings17, 16, 8).ToString();
            textBoxMOD.Text = GetBits(timings16, 8, 6).ToString();
            textBoxMRD.Text = GetBits(timings16, 0, 6).ToString();

            uint trefi = GetBits(timings15, 0, 16);
            textBoxREFI.Text = trefi.ToString();
            textBoxREFIns.Text = Convert.ToInt32(1000 / mclk * 2 * trefi).ToString();

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

        private void ButtonScreenshot_Click(object sender, EventArgs e)
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
