#define BETA

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Management;
using System.Security.Permissions;
using System.Windows.Forms;
using ZenStates;
using ZenTimings.Utils;

namespace ZenTimings
{
    public partial class MainForm : Form
    {
        public const uint F17H_M01H_SVI = 0x0005A000;
        public const uint F17H_M01H_SVI_TEL_PLANE0 = (F17H_M01H_SVI + 0xC);
        public const uint F17H_M01H_SVI_TEL_PLANE1 = (F17H_M01H_SVI + 0x10);
        public const uint F17H_M30H_SVI_TEL_PLANE0 = (F17H_M01H_SVI + 0x14);
        public const uint F17H_M30H_SVI_TEL_PLANE1 = (F17H_M01H_SVI + 0x10);
        public const uint F17H_M70H_SVI_TEL_PLANE0 = (F17H_M01H_SVI + 0x10);
        public const uint F17H_M70H_SVI_TEL_PLANE1 = (F17H_M01H_SVI + 0xC);

        private readonly List<MemoryModule> modules = new List<MemoryModule>();
        private readonly List<BiosACPIFunction> biosFunctions = new List<BiosACPIFunction>();
        private readonly Ops OPS = new Ops();
        private readonly MemoryConfig MEMCFG = new MemoryConfig();
        private SystemInfo SI;
        private BiosMemController BMC;
        private readonly PowerTable PowerTable;
        private uint dramBaseAddress = 0;
        private UIntPtr dramPtr;
        private bool compatMode = false;
#if DEBUG
        readonly TextWriterTraceListener[] listeners = new TextWriterTraceListener[] {
            //new TextWriterTraceListener("debug.txt")
        };
#endif

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
        private void FloatToVoltageString(object sender, ConvertEventArgs cevent)
        {
            // The method converts only to string type. Test this using the DesiredType.
            if (cevent.DesiredType != typeof(string)) return;

            // Use the ToString method to format the value as currency ("c").
            cevent.Value = $"{(float) cevent.Value:F4}V";
        }

        private void InitSystemInfo()
        {
            var cpufamily = OPS.GetCpuFamily();
            if (cpufamily != SMU.CpuFamily.FAMILY_17H && cpufamily != SMU.CpuFamily.FAMILY_19H)
            {
                MessageBox.Show("CPU is not supported.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ExitApplication();
            }

            SI = new SystemInfo
            {
                CpuId = OPS.GetCpuId(),
                CpuName = OPS.GetCpuName(),
                NodesPerProcessor = OPS.GetCpuNodes(),
                PackageType = OPS.GetPkgType(),
                PatchLevel = OPS.GetPatchLevel(),
                SmuVersion = OPS.Smu.Version,
            };

            SI.Model = (SI.CpuId & 0xff) >> 4;
            SI.ExtendedModel = SI.Model + ((SI.CpuId >> 12) & 0xf0);
            int[] coreCount = OPS.GetCoreCount();
            SI.FusedCoreCount = coreCount[0];
            SI.Threads = coreCount[1];
            

            ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_BaseBoard");
            foreach (ManagementObject obj in searcher.Get())
            {
                SI.MbVendor = ((string)obj["Manufacturer"]).Trim();
                SI.MbName = ((string)obj["Product"]).Trim();
            }
            if (searcher != null) searcher.Dispose();

            searcher = new ManagementObjectSearcher("SELECT * FROM Win32_BIOS");
            foreach (ManagementObject obj in searcher.Get())
            {
                SI.BiosVersion = ((string)obj["SMBIOSBIOSVersion"]).Trim();
            }
            if (searcher != null) searcher.Dispose();
        }

        private BiosACPIFunction GetFunctionByIdString(string name)
        {
            return biosFunctions.Find(x => x.IDString == name);
        }

        private void BindControls()
        {
            // First column
            textBoxFreq.DataBindings.Add("Text", MEMCFG, "Frequency");
            textBoxBGS.DataBindings.Add("Text", MEMCFG, "BGS");
            textBoxGDM.DataBindings.Add("Text", MEMCFG, "GDM");
            textBoxCL.DataBindings.Add("Text", MEMCFG, "CL");
            textBoxRCDWR.DataBindings.Add("Text", MEMCFG, "RCDWR");
            textBoxRCDRD.DataBindings.Add("Text", MEMCFG, "RCDRD");
            textBoxRP.DataBindings.Add("Text", MEMCFG, "RP");
            textBoxRAS.DataBindings.Add("Text", MEMCFG, "RAS");
            textBoxRC.DataBindings.Add("Text", MEMCFG, "RC");
            textBoxRRDS.DataBindings.Add("Text", MEMCFG, "RRDS");
            textBoxRRDL.DataBindings.Add("Text", MEMCFG, "RRDL");
            textBoxFAW.DataBindings.Add("Text", MEMCFG, "FAW");
            textBoxWTRS.DataBindings.Add("Text", MEMCFG, "WTRS");
            textBoxWTRL.DataBindings.Add("Text", MEMCFG, "WTRL");
            textBoxWR.DataBindings.Add("Text", MEMCFG, "WR");
            textBoxRFC.DataBindings.Add("Text", MEMCFG, "RFC");
            textBoxRFC2.DataBindings.Add("Text", MEMCFG, "RFC2");
            textBoxRFC4.DataBindings.Add("Text", MEMCFG, "RFC4");
            textBoxRFCns.DataBindings.Add("Text", MEMCFG, "RFCns");
            textBoxMOD.DataBindings.Add("Text", MEMCFG, "MOD");
            textBoxMODPDA.DataBindings.Add("Text", MEMCFG, "MODPDA");


            // Second column
            textBoxCapacity.DataBindings.Add("Text", MEMCFG, "TotalCapacity");
            textBoxBGSAlt.DataBindings.Add("Text", MEMCFG, "BGSAlt");
            textBoxCmd2T.DataBindings.Add("Text", MEMCFG, "Cmd2T");
            textBoxRDRDSCL.DataBindings.Add("Text", MEMCFG, "RDRDSCL");
            textBoxWRWRSCL.DataBindings.Add("Text", MEMCFG, "WRWRSCL");
            textBoxCWL.DataBindings.Add("Text", MEMCFG, "CWL");
            textBoxRTP.DataBindings.Add("Text", MEMCFG, "RTP");
            textBoxRDWR.DataBindings.Add("Text", MEMCFG, "RDWR");
            textBoxWRRD.DataBindings.Add("Text", MEMCFG, "WRRD");
            textBoxRDRDSC.DataBindings.Add("Text", MEMCFG, "RDRDSC");
            textBoxRDRDSD.DataBindings.Add("Text", MEMCFG, "RDRDSD");
            textBoxRDRDDD.DataBindings.Add("Text", MEMCFG, "RDRDDD");
            textBoxWRWRSC.DataBindings.Add("Text", MEMCFG, "WRWRSC");
            textBoxWRWRSD.DataBindings.Add("Text", MEMCFG, "WRWRSD");
            textBoxWRWRDD.DataBindings.Add("Text", MEMCFG, "WRWRDD");
            textBoxCKE.DataBindings.Add("Text", MEMCFG, "CKE");
            textBoxREFI.DataBindings.Add("Text", MEMCFG, "REFI");
            textBoxREFIns.DataBindings.Add("Text", MEMCFG, "REFIns");
            textBoxSTAG.DataBindings.Add("Text", MEMCFG, "STAG");
            textBoxMRD.DataBindings.Add("Text", MEMCFG, "MRD");
            textBoxMRDPDA.DataBindings.Add("Text", MEMCFG, "MRDPDA");

            // Third column
            textBoxMCLK.DataBindings.Add("Text", PowerTable, "MCLK");
            textBoxFCLK.DataBindings.Add("Text", PowerTable, "FCLK");
            textBoxUCLK.DataBindings.Add("Text", PowerTable, "UCLK");
            //Binding b = new Binding("Text", PowerTable, "VDDCR_SOC");
            //b.Format += new ConvertEventHandler(FloatToVoltageString);
            textBoxVDDCR_SOC.DataBindings.Add("Text", PowerTable, "VDDCR_SOC");
            textBoxCLDO_VDDP.DataBindings.Add("Text", PowerTable, "CLDO_VDDP");
            textBoxCLDO_VDDG.DataBindings.Add("Text", PowerTable, "CLDO_VDDG");
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

                        temp = WMI.TryGetProperty(queryObject, "Capacity");
                        if (temp != null) capacity = (ulong)temp;

                        temp = WMI.TryGetProperty(queryObject, "ConfiguredClockSpeed");
                        if (temp != null) clockSpeed = (uint)temp;

                        temp = WMI.TryGetProperty(queryObject, "partNumber");
                        if (temp != null) partNumber = (string)temp;

                        modules.Add(new MemoryModule(partNumber, capacity, clockSpeed));
                        comboBoxPartNumber.Items.Add($"{partNumber}");
                        comboBoxPartNumber.SelectedIndex = 0;
                    }

                    if (modules.Count > 0)
                    {
                        var totalCapacity = 0UL;

                        foreach (var module in modules)
                        {
                            totalCapacity += module.Capacity;
                        }

                        if (modules.FirstOrDefault().ClockSpeed != 0)
                            MEMCFG.Frequency = modules.FirstOrDefault().ClockSpeed;

                        if (totalCapacity != 0)
                            MEMCFG.TotalCapacity = $"{totalCapacity / 1024 / (1024 * 1024)}GB";
                    }
                }
                catch {
                    MessageBox.Show("Failed to get installed memory parameters. Corresponding fields will be empty.",
                        "Warning", 
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }
        }

        private void ReadPowerConfig()
        {
#if DEBUG
            /*uint prev1 = 0;
            uint prev2 = 0;
            for (; ; )
            {
                uint cmd1 = 0, cmd2 = 0;
                uint arg1 = 0, arg2 = 0;
                uint rsp1 = 0, rsp2 = 0;

                ops.SmuReadReg(0x03B10564, ref arg1);
                ops.SmuReadReg(0x03B10528, ref cmd1);
                ops.SmuReadReg(0x03B10598, ref rsp1);
                if (cmd1 != prev1)
                {
                    prev1 = cmd1;
                    Console.WriteLine($"1 -> 0x{cmd1:X2}: 0x{arg1:X8}: 0x{rsp1:X8}");
                }

                ops.SmuReadReg(ops.Smu.SMU_ADDR_ARG, ref arg2);
                ops.SmuReadReg(ops.Smu.SMU_ADDR_MSG, ref cmd2);
                ops.SmuReadReg(ops.Smu.SMU_ADDR_RSP, ref rsp2);
                if (cmd2 != prev2)
                {
                    prev2 = cmd2;
                    Console.WriteLine($"2 -> 0x{cmd2:X2}: 0x{arg2:X8}: 0x{rsp2:X8}");
                }
            }*/
#endif
            if (dramBaseAddress > 0)
            {
                try
                {
                    uint data = 0;
                    uint[] table = new uint[PowerTable.tableSize / 4];

                    if (OPS.TransferTableToDram() != SMU.Status.OK)
                        OPS.TransferTableToDram(); // retry


                    for (int i = 0; i < table.Length; ++i)
                    {
                        NativeMethods.GetPhysLong(dramPtr + (i * 0x4), out data);
                        table[i] = data;
                    }

                    PowerTable.Table = table;
                }
                catch (EntryPointNotFoundException ex)
                {
                    throw new ApplicationException(ex.Message);
                }
                catch (DllNotFoundException ex)
                {
                    throw new ApplicationException(ex.Message);
                }
            }
        }

        private void ReadSVI()
        {
            uint sviSocAddress, sviCoreaddress;
            // SVI2 interface
            switch (OPS.CpuType/*si.ExtendedModel*/)
            {
                //case 0x1:  // Zen
                //case 0x8:  // Zen+
                //case 0x11: // Zen APU
                case SMU.CPUType.SummitRidge:
                case SMU.CPUType.Threadripper:
                case SMU.CPUType.PinnacleRidge:
                case SMU.CPUType.Naples:
                case SMU.CPUType.Colfax:
                case SMU.CPUType.RavenRidge:
                case SMU.CPUType.Fenghuang:
                    sviCoreaddress = F17H_M01H_SVI_TEL_PLANE0;
                    sviSocAddress = F17H_M01H_SVI_TEL_PLANE1;
                    break;

                //case 0x18: // Zen+ APU
                case SMU.CPUType.Picasso:
                    sviCoreaddress = F17H_M01H_SVI_TEL_PLANE1;
                    sviSocAddress = F17H_M01H_SVI_TEL_PLANE0;
                    break;

                //case 0x31: // Zen2 Threadripper/EPYC
                case SMU.CPUType.CastlePeak:
                case SMU.CPUType.Rome:
                    sviCoreaddress = F17H_M30H_SVI_TEL_PLANE0;
                    sviSocAddress = F17H_M30H_SVI_TEL_PLANE1;
                    break;

                //case 0x60: // Zen2 APU
                //case 0x71: // Zen2 Ryzen
                case SMU.CPUType.Matisse:
                case SMU.CPUType.Renoir:
                    sviCoreaddress = F17H_M70H_SVI_TEL_PLANE0;
                    sviSocAddress = F17H_M70H_SVI_TEL_PLANE1;
                    break;

                default:
                    sviCoreaddress = F17H_M01H_SVI_TEL_PLANE0;
                    sviSocAddress = F17H_M01H_SVI_TEL_PLANE1;
                    break;
            }

            /*if (OPS.Smu.SMU_TYPE == SMU.SmuType.TYPE_CPU1)
            {
                float mclk = MEMCFG.Frequency / 2;
                textBoxMCLK.Text = textBoxFCLK.Text = $"{mclk:F2}";
                textBoxFCLK.Text = textBoxFCLK.Text = $"{mclk:F2}";
                textBoxUCLK.Text = textBoxFCLK.Text = $"{mclk:F2}";
            }*/

            uint vddcr_soc = (OPS.ReadDword(sviSocAddress) >> 16) & 0xFF;
            //uint vcore = (ops.ReadDword(sviCoreaddress) >> 16) & 0xFF;

            textBoxVSOC_SVI2.Text = $"{OPS.VidToVoltage(vddcr_soc):F4}V";
        }

        private void ReadMemoryConfig()
        {
            string scope = "root\\wmi";
            string className = "AMD_ACPI";

            try
            {
                string instanceName = WMI.GetInstanceName(scope, className);

                ManagementBaseObject pack;
                ManagementObject classInstance = new ManagementObject(scope,
                    $"{className}.InstanceName='{instanceName}'",
                    null);

                // Get possible values (index) of a memory option in BIOS
                /*pack = WMI.InvokeMethod(classInstance, "Getdvalues", "pack", "ID", 0x20007);
                if (pack != null)
                {
                    uint[] DValuesBuffer = (uint[])pack.GetPropertyValue("DValuesBuffer");
                    for (var i = 0; i < DValuesBuffer.Length; i++)
                    {
                        Debug.WriteLine("{0}", DValuesBuffer[i]);
                    }
                }*/


                // Get function names with their IDs
                string[] functionObjects = { "GetObjectID", "GetObjectID2" };
                foreach (var functionObject in functionObjects)
                {
                    try
                    {
                        pack = WMI.InvokeMethod(classInstance, functionObject, "pack", null, 0);
                        if (pack != null)
                        {
                            uint[] ID = (uint[])pack.GetPropertyValue("ID");
                            string[] IDString = (string[])pack.GetPropertyValue("IDString");
                            byte Length = (byte)pack.GetPropertyValue("Length");

                            Debug.WriteLine("----------------------------");
                            Debug.WriteLine("WMI: BIOS Functions");
                            Debug.WriteLine("----------------------------");

                            for (var i = 0; i < Length; ++i)
                            {
                                biosFunctions.Add(new BiosACPIFunction(IDString[i], ID[i]));
                                Debug.WriteLine("{0}: {1:X8}", IDString[i], ID[i]);
                            }
                        }
                    }
                    catch { }
                }

                // Get APCB config from BIOS. Holds memory parameters.
                BiosACPIFunction cmd = GetFunctionByIdString("Get APCB Config");
                if (cmd != null)
                {
                    BMC.Table = WMI.RunCommand(classInstance, cmd.ID);

                    textBoxProcODT.Text = BMC.GetProcODTString(BMC.Config.ProcODT);

                    textBoxClkDrvStren.Text = BMC.GetDrvStrenString(BMC.Config.ClkDrvStren);
                    textBoxAddrCmdDrvStren.Text = BMC.GetDrvStrenString(BMC.Config.AddrCmdDrvStren);
                    textBoxCsOdtCmdDrvStren.Text = BMC.GetDrvStrenString(BMC.Config.CsOdtCmdDrvStren);
                    textBoxCkeDrvStren.Text = BMC.GetDrvStrenString(BMC.Config.CkeDrvStren);

                    textBoxRttNom.Text = BMC.GetRttString(BMC.Config.RttNom);
                    textBoxRttWr.Text = BMC.GetRttWrString(BMC.Config.RttWr);
                    textBoxRttPark.Text = BMC.GetRttString(BMC.Config.RttPark);

                    textBoxAddrCmdSetup.Text = $"{BMC.Config.AddrCmdSetup}";
                    textBoxCsOdtSetup.Text = $"{BMC.Config.CsOdtSetup}";
                    textBoxCkeSetup.Text = $"{BMC.Config.CkeSetup}";
                }
                else
                {
                    compatMode = true;
                }
            }
            catch 
            {
                MessageBox.Show("Failed to read AMD ACPI. Some parameters will be empty.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                enabled = OPS.GetBits(OPS.ReadDword(0x50DF0 + offset), 19, 1) == 0;
            }

            uint umcBase = OPS.ReadDword(0x50200 + offset);
            uint bgsa0 = OPS.ReadDword(0x500D0 + offset);
            uint bgsa1 = OPS.ReadDword(0x500D4 + offset);
            uint bgs0 = OPS.ReadDword(0x50050 + offset);
            uint bgs1 = OPS.ReadDword(0x50058 + offset);
            uint timings5 = OPS.ReadDword(0x50204 + offset);
            uint timings6 = OPS.ReadDword(0x50208 + offset);
            uint timings7 = OPS.ReadDword(0x5020C + offset);
            uint timings8 = OPS.ReadDword(0x50210 + offset);
            uint timings9 = OPS.ReadDword(0x50214 + offset);
            uint timings10 = OPS.ReadDword(0x50218 + offset);
            uint timings11 = OPS.ReadDword(0x5021C + offset);
            uint timings12 = OPS.ReadDword(0x50220 + offset);
            uint timings13 = OPS.ReadDword(0x50224 + offset);
            uint timings14 = OPS.ReadDword(0x50228 + offset);
            uint timings15 = OPS.ReadDword(0x50230 + offset);
            uint timings16 = OPS.ReadDword(0x50234 + offset);
            uint timings17 = OPS.ReadDword(0x50250 + offset);
            uint timings18 = OPS.ReadDword(0x50254 + offset);
            uint timings19 = OPS.ReadDword(0x50258 + offset);
            uint timings20 = OPS.ReadDword(0x50260 + offset);
            uint timings21 = OPS.ReadDword(0x50264 + offset);
            uint timings22 = OPS.ReadDword(0x5028C + offset);
            uint timings23 = timings20 != timings21 ? (timings20 != 0x21060138 ? timings20 : timings21) : timings20;

            MEMCFG.BGS = (bgs0 == 0x87654321 && bgs1 == 0x87654321) ? "Disabled" : "Enabled";
            MEMCFG.BGSAlt = OPS.GetBits(bgsa0, 4, 7) > 0 || OPS.GetBits(bgsa1, 4, 7) > 0 ? "Enabled" : "Disabled";
            MEMCFG.GDM = OPS.GetBits(umcBase, 11, 1) > 0 ? "Enabled" : "Disabled";
            MEMCFG.Cmd2T = OPS.GetBits(umcBase, 10, 1) > 0 ? "2T" : "1T";

            MEMCFG.CL = OPS.GetBits(timings5, 0, 6);
            MEMCFG.RAS = OPS.GetBits(timings5, 8, 7);
            MEMCFG.RCDRD = OPS.GetBits(timings5, 16, 6);
            MEMCFG.RCDWR = OPS.GetBits(timings5, 24, 6);

            MEMCFG.RC = OPS.GetBits(timings6, 0, 8);
            MEMCFG.RP = OPS.GetBits(timings6, 16, 6);

            MEMCFG.RRDS = OPS.GetBits(timings7, 0, 5);
            MEMCFG.RRDL = OPS.GetBits(timings7, 8, 5);
            MEMCFG.RTP = OPS.GetBits(timings7, 24, 5);

            MEMCFG.FAW = OPS.GetBits(timings8, 0, 8);

            MEMCFG.CWL = OPS.GetBits(timings9, 0, 6);
            MEMCFG.WTRS = OPS.GetBits(timings9, 8, 5);
            MEMCFG.WTRL = OPS.GetBits(timings9, 16, 7);

            MEMCFG.WR = OPS.GetBits(timings10, 0, 8);

            MEMCFG.RDRDDD = OPS.GetBits(timings12, 0, 4);
            MEMCFG.RDRDSD = OPS.GetBits(timings12, 8, 4);
            MEMCFG.RDRDSC = OPS.GetBits(timings12, 16, 4);
            MEMCFG.RDRDSCL = OPS.GetBits(timings12, 24, 6);

            MEMCFG.WRWRDD = OPS.GetBits(timings13, 0, 4);
            MEMCFG.WRWRSD = OPS.GetBits(timings13, 8, 4);
            MEMCFG.WRWRSC = OPS.GetBits(timings13, 16, 4);
            MEMCFG.WRWRSCL = OPS.GetBits(timings13, 24, 6);

            MEMCFG.RDWR = OPS.GetBits(timings14, 8, 5);
            MEMCFG.WRRD = OPS.GetBits(timings14, 0, 4);

            MEMCFG.REFI = OPS.GetBits(timings15, 0, 16);
       
            MEMCFG.MODPDA = OPS.GetBits(timings16, 24, 6);
            MEMCFG.MRDPDA = OPS.GetBits(timings16, 16, 6);
            MEMCFG.MOD = OPS.GetBits(timings16, 8, 6);
            MEMCFG.MRD = OPS.GetBits(timings16, 0, 6);

            MEMCFG.STAG = OPS.GetBits(timings17, 16, 8);

            MEMCFG.CKE = OPS.GetBits(timings18, 24, 5);

            MEMCFG.RFC = OPS.GetBits(timings23, 0, 11);
            MEMCFG.RFC2 = OPS.GetBits(timings23, 11, 11);
            MEMCFG.RFC4 = OPS.GetBits(timings23, 22, 11);

            // Fallback to ratio when ConfiguredClockSpeed fails
            if (MEMCFG.Frequency == 0)
                MEMCFG.Frequency = OPS.GetBits(umcBase, 0, 7) / 3.0f * 200;
        }

        public MainForm()
        {
            try
            {
                PowerTable = new PowerTable(OPS.Smu.SMU_TYPE);
                BMC = new BiosMemController();
#if BETA
                MessageBox.Show("This is a BETA version of the application. Some functions might be working incorrectly.\n\n" +
                    "Please report if something is not working as expected.");
#endif
#if DEBUG
                Debug.Listeners.AddRange(listeners);
#endif
                InitSystemInfo();
                InitializeComponent();
                BindControls();
            }
            catch (ApplicationException ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Dispose();
                ExitApplication();
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
#if BETA
            Text += " beta 4";
#endif
#if DEBUG
            Text += " (debug)";
#endif
            labelCPU.Text = SI.CpuName;
            labelMB.Text = $"{SI.MbName} | BIOS {SI.BiosVersion} | SMU {SI.GetSmuVersionString()}";

            ReadMemoryModulesInfo();
            ReadTimings();
            ReadMemoryConfig();
            ReadSVI();

            // Get first base address
            dramBaseAddress = (uint)(OPS.GetDramBaseAddress() & 0xFFFFFFFF);
            if (dramBaseAddress > 0)
            {
                dramPtr = new UIntPtr(dramBaseAddress);
                ReadPowerConfig();
#if !DEBUG
                PowerCfgTimer.Start();
#endif
            }
            else
            {
                compatMode = true;
            }

            if (compatMode)
            {
                Text += " (compatibility)";
                /*MessageBox.Show("Could not get DRAM base address.\nRunning in compatibility mode.",
                    "Warning",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);*/

                MessageBox.Show("Could not get some of the parameters.\nRunning in compatibility mode.",
                    "Warning",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            foreach (Control ctrl in tableLayoutPanelValues.Controls)
            {
                if (ctrl.GetType() == typeof(Label) && ctrl.Text == "N/A")
                {
                    var prop = ctrl.Name.Replace("textBox", "");
                    ctrl.Enabled = false;
                    var label = tableLayoutPanelValues.Controls[$"label{prop}"];
                    if (label != null) 
                        label.Enabled = false;
                }
            }
#if DEBUG
            foreach (TextWriterTraceListener listener in listeners)
            {
                listener.Flush();
            }
#endif
        }

        private void ButtonExit_Click(object sender, EventArgs e)
        {
            ExitApplication();
        }

        private void PowerCfgTimer_Tick(object sender, EventArgs e)
        {
            //ReadTimings();
            //ReadMemoryConfig();
            ReadSVI();
            ReadPowerConfig();
        }

        private void TableLayoutPanel1_CellPaint(object sender, TableLayoutCellPaintEventArgs e)
        {
            using (SolidBrush brush = new SolidBrush((Color)new ColorConverter().ConvertFrom("#eeeeee")))
            {
                if ((e.Row + 1) % 2 == 0)
                {
                    e.Graphics.FillRectangle(brush, e.CellBounds);
                }    
            }
        }

        private void ShowWindow()
        {
            Show();
            Activate();
            BringToFront();
            WindowState = FormWindowState.Normal;
        }

        static void MinimizeFootprint()
        {
            NativeMethods.EmptyWorkingSet(Process.GetCurrentProcess().Handle);
        }

        private void MainForm_Shown(object sender, EventArgs e)
        {
            //SplashForm.CloseForm();
            ShowWindow();
            MinimizeFootprint();
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            MinimizeFootprint();
        }

        [SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.UnmanagedCode)]
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WM_SHOWME)
            {
                ShowWindow();
            }
            base.WndProc(ref m);
        }

        private void ExitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Dispose();
            ExitApplication();
        }

        private void AboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AboutBox aboutWnd = new AboutBox();
            aboutWnd.ShowDialog();
        }

        private void OptionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Form optionsWnd = new OptionsDialog();
            optionsWnd.ShowDialog();
        }

        private void DebugToolstripItem_Click(object sender, EventArgs e)
        {
            Form debugWnd = new DebugDialog(dramBaseAddress, modules, MEMCFG, SI, BMC.Table, PowerTable.Table);
            debugWnd.ShowDialog();
        }
    }
}
