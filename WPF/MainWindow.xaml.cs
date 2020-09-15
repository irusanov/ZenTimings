#define BETA

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using ZenTimings;
using System.Management;
using System.Diagnostics;
using System.Threading;
using System.IO;
using System.Windows.Threading;
using ZenStates;
using ZenTimings.Utils;
using AdonisUI.Controls;
using ZenTimings.Windows;
using System.Security.Permissions;
using System.Windows.Interop;
using System.Reflection;
using System.Drawing;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using System.Windows.Media.Effects;
using System.Windows.Media;
using System.Windows.Controls.Primitives;

namespace ZenTimings
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : AdonisWindow
    {
        public const uint F17H_M01H_SVI = 0x0005A000;
        public const uint F17H_M02H_SVI = 0x0006F000; // Renoir only?
        public const uint F17H_M01H_SVI_TEL_PLANE0 = (F17H_M01H_SVI + 0xC);
        public const uint F17H_M01H_SVI_TEL_PLANE1 = (F17H_M01H_SVI + 0x10);
        public const uint F17H_M30H_SVI_TEL_PLANE0 = (F17H_M01H_SVI + 0x14);
        public const uint F17H_M30H_SVI_TEL_PLANE1 = (F17H_M01H_SVI + 0x10);
        public const uint F17H_M60H_SVI_TEL_PLANE0 = (F17H_M02H_SVI + 0x38);
        public const uint F17H_M60H_SVI_TEL_PLANE1 = (F17H_M02H_SVI + 0x3C);
        public const uint F17H_M70H_SVI_TEL_PLANE0 = (F17H_M01H_SVI + 0x10);
        public const uint F17H_M70H_SVI_TEL_PLANE1 = (F17H_M01H_SVI + 0xC);

        private readonly List<MemoryModule> modules = new List<MemoryModule>();
        private readonly List<BiosACPIFunction> biosFunctions = new List<BiosACPIFunction>();
        private readonly Ops OPS = new Ops();
        private readonly MemoryConfig MEMCFG = new MemoryConfig();
        private readonly BiosMemController BMC;
        private readonly PowerTable PowerTable;
        private SystemInfo SI;
        private readonly uint dramBaseAddress = 0;
        private bool compatMode = false;
        private BackgroundWorker backgroundWorker1;
        private readonly AppSettings settings = new AppSettings();
        private readonly uint[] table = new uint[PowerTable.tableSize / 4];
        private readonly DispatcherTimer PowerCfgTimer = new DispatcherTimer();

        private static void ExitApplication()
        {
            Application.Current.Shutdown();
        }

        private void InitSystemInfo()
        {
            var cpufamily = OPS.GetCpuFamily();
            if (cpufamily != SMU.CpuFamily.FAMILY_17H && cpufamily != SMU.CpuFamily.FAMILY_19H)
            {
                AdonisUI.Controls.MessageBox.Show("CPU is not supported.", "Error", AdonisUI.Controls.MessageBoxButton.OK, AdonisUI.Controls.MessageBoxImage.Error);
                ExitApplication();
            }

            int[] coreCount = OPS.GetCoreCount();
            SI = new SystemInfo
            {
                CpuId = OPS.GetCpuId(),
                CpuName = OPS.GetCpuName(),
                NodesPerProcessor = OPS.GetCpuNodes(),
                PackageType = OPS.GetPkgType(),
                PatchLevel = OPS.GetPatchLevel(),
                SmuVersion = OPS.Smu.Version,
                FusedCoreCount = coreCount[0],
                Threads = coreCount[1],
                CCDCount = OPS.GetCCDCount(),
                CodeName = $"{OPS.CpuType}",
                CpuFrequency = OPS.GetTimeStampFrequency(),
            };

            SI.Model = (SI.CpuId & 0xff) >> 4;
            SI.ExtendedModel = SI.Model + ((SI.CpuId >> 12) & 0xF0);

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
                        string bankLabel = "";
                        string manufacturer = "";
                        string deviceLocator = "";
                        object temp;

                        temp = WMI.TryGetProperty(queryObject, "Capacity");
                        if (temp != null) capacity = (ulong)temp;

                        temp = WMI.TryGetProperty(queryObject, "ConfiguredClockSpeed");
                        if (temp != null) clockSpeed = (uint)temp;

                        temp = WMI.TryGetProperty(queryObject, "partNumber");
                        if (temp != null) partNumber = (string)temp;

                        temp = WMI.TryGetProperty(queryObject, "BankLabel");
                        if (temp != null) bankLabel = (string)temp;

                        temp = WMI.TryGetProperty(queryObject, "Manufacturer");
                        if (temp != null) manufacturer = (string)temp;

                        temp = WMI.TryGetProperty(queryObject, "DeviceLocator");
                        if (temp != null) deviceLocator = (string)temp;

                        modules.Add(new MemoryModule(partNumber, bankLabel, manufacturer, deviceLocator, capacity, clockSpeed));
                        string dl = deviceLocator.Length > 0 ? $"#{deviceLocator.Replace("DIMM_", "")}: " : "";
                        comboBoxPartNumber.Items.Add($"{dl}{partNumber}");
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
                catch
                {
                    AdonisUI.Controls.MessageBox.Show("Failed to get installed memory parameters. Corresponding fields will be empty.",
                        "Warning",
                        AdonisUI.Controls.MessageBoxButton.OK,
                        AdonisUI.Controls.MessageBoxImage.Warning);
                }
            }
        }

        private void ReadPowerConfig()
        {
            if (dramBaseAddress > 0)
            {
                try
                {
                    uint data = 0;

                    if (OPS.TransferTableToDram() != SMU.Status.OK)
                        OPS.TransferTableToDram(); // retry

                    for (int i = 0; i < table.Length; ++i)
                    {
                        InteropMethods.GetPhysLong((UIntPtr)dramBaseAddress + (i * 0x4), out data);
                        table[i] = data;
                    }

                    if (table.Any(v => v != 0))
                    {
                        PowerTable.ConfiguredClockSpeed = MEMCFG.Frequency;
                        PowerTable.Table = table;
                    }
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
                case SMU.CPUType.PinnacleRidge:
                case SMU.CPUType.RavenRidge:
                case SMU.CPUType.Fenghuang:
                    sviCoreaddress = F17H_M01H_SVI_TEL_PLANE0;
                    sviSocAddress = F17H_M01H_SVI_TEL_PLANE1;
                    break;

                case SMU.CPUType.Threadripper:
                case SMU.CPUType.Naples:
                case SMU.CPUType.Colfax:
                    sviCoreaddress = F17H_M01H_SVI_TEL_PLANE1;
                    sviSocAddress = F17H_M01H_SVI_TEL_PLANE0;
                    break;

                //case 0x31: // Zen2 Threadripper/EPYC
                case SMU.CPUType.CastlePeak:
                case SMU.CPUType.Rome:
                    sviCoreaddress = F17H_M30H_SVI_TEL_PLANE0;
                    sviSocAddress = F17H_M30H_SVI_TEL_PLANE1;
                    break;

                //case 0x18: // Zen+ APU
                //case 0x60: // Zen2 APU
                //case 0x71: // Zen2 Ryzen
                case SMU.CPUType.Picasso:
                case SMU.CPUType.Matisse:
                    //case SMU.CPUType.Renoir:
                    sviCoreaddress = F17H_M70H_SVI_TEL_PLANE0;
                    sviSocAddress = F17H_M70H_SVI_TEL_PLANE1;
                    break;

                // Temprorary skip Renoir. SVI2 PCI address range is empty.
                case SMU.CPUType.Renoir:
                    sviCoreaddress = F17H_M60H_SVI_TEL_PLANE0;
                    sviSocAddress = F17H_M60H_SVI_TEL_PLANE1;
                    break;

                default:
                    sviCoreaddress = F17H_M01H_SVI_TEL_PLANE0;
                    sviSocAddress = F17H_M01H_SVI_TEL_PLANE1;
                    break;
            }

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
                if (cmd == null)
                    throw new Exception();

                BMC.Table = WMI.RunCommand(classInstance, cmd.ID);
                var allZero = !BMC.Table.Any(v => v != 0);

                if (allZero || BMC.Table == null || BMC.Config.ProcODT < 1)
                {
                    BMC.Table = null;
                    throw new Exception();
                }

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
            catch (Exception ex)
            {
                compatMode = true;
                AdonisUI.Controls.MessageBox.Show(
                    "Failed to read AMD ACPI. Some parameters will be empty.",
                    "Warning",
                    AdonisUI.Controls.MessageBoxButton.OK,
                    AdonisUI.Controls.MessageBoxImage.Warning);
                Console.WriteLine(ex.Message);
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

            Console.WriteLine($"Txp: {OPS.GetBits(timings18, 0, 6)}");

            MEMCFG.STAG = OPS.GetBits(timings17, 16, 8);

            MEMCFG.CKE = OPS.GetBits(timings18, 24, 5);

            MEMCFG.RFC = OPS.GetBits(timings23, 0, 11);
            MEMCFG.RFC2 = OPS.GetBits(timings23, 11, 11);
            MEMCFG.RFC4 = OPS.GetBits(timings23, 22, 11);

            var configured = MEMCFG.Frequency;
            var freqFromRatio = OPS.GetBits(umcBase, 0, 7) / 3.0f * 200;

            // Fallback to ratio when ConfiguredClockSpeed fails
            if (configured == 0 || freqFromRatio > configured)
            {
                MEMCFG.Frequency = freqFromRatio;
                //PowerTable.ConfiguredClockSpeed = freqFromRatio;
            }
        }

        private void WaitForPowerTable(object sender, DoWorkEventArgs e)
        {
            int minimum_retries = 2;
            uint temp = 0;

            Stopwatch timer = new Stopwatch();
            timer.Start();

            // Refresh until table is transferred to DRAM or timeout
            do
                InteropMethods.GetPhysLong((UIntPtr)dramBaseAddress, out temp);
            while (temp == 0 && timer.Elapsed.TotalMilliseconds < 10000);

            InteropMethods.GetPhysLong((UIntPtr)dramBaseAddress, out temp);

            // Already in DRAM and auto-refresh disabled
            if (temp != 0)
                Thread.Sleep(Convert.ToInt32(PowerCfgTimer.Interval.TotalMilliseconds) * minimum_retries);

            timer.Stop();
        }

        private void WaitForPowerTable_Complete(object sender, RunWorkerCompletedEventArgs e)
        {
            if (settings.AutoRefresh)
                PowerCfgTimer.Interval = TimeSpan.FromMilliseconds(settings.AutoRefreshInterval);
            else
                PowerCfgTimer.Stop();

            //DisableInactiveControls();
        }

        public static bool CheckConfigFileIsPresent()
        {
            return File.Exists(AppDomain.CurrentDomain.SetupInformation.ConfigurationFile);
        }

        private void StartAutoRefresh()
        {
            PowerCfgTimer.Start();

            //if (!CheckConfigFileIsPresent())
                //return;

            backgroundWorker1 = new BackgroundWorker();
            backgroundWorker1.DoWork += WaitForPowerTable;
            backgroundWorker1.RunWorkerCompleted += WaitForPowerTable_Complete;
            backgroundWorker1.RunWorkerAsync();
        }

        private void PowerCfgTimer_Tick(object sender, EventArgs e)
        {
            Console.WriteLine("refreshing");
            //ReadTimings();
            //ReadMemoryConfig();
            ReadSVI();
            ReadPowerConfig();
        }

        /*        private void DisableInactiveControls()
                {
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
                }*/

        private ImageSource GetIcon(string iconSource, double width)
        {
            var decoder = BitmapDecoder.Create(new Uri(iconSource),
                BitmapCreateOptions.DelayCreation,
                BitmapCacheOption.OnDemand);

            var result = decoder.Frames.SingleOrDefault(f => f.Width == width);
            if (result == default(BitmapFrame))
            {
                result = decoder.Frames.OrderBy(f => f.Width).First();
            }

            return result;
        }

        public MainWindow()
        {
            // Try to get your icon in the correct size here and assign it.
            IconSource = GetIcon("pack://application:,,,/ZenTimings;component/Resources/ZenTimings.ico", 16);

            try
            {
                PowerTable = new PowerTable(OPS.Smu.SMU_TYPE);
                BMC = new BiosMemController();

                PowerCfgTimer.Interval = TimeSpan.FromMilliseconds(2000);
                PowerCfgTimer.Tick += new EventHandler(PowerCfgTimer_Tick);

                if (settings.DarkMode)
                    settings.ChangeTheme();

                InitSystemInfo();
                InitializeComponent();
                //BindCommands();
                ReadMemoryModulesInfo();
                ReadTimings();

                //System.Windows.MessageBox.Show(OPS.GetBaseClock().ToString());

                DataContext = new
                {
                    timings = MEMCFG,
                    powerTable = PowerTable,
                    settings,
                };

                if (settings.AdvancedMode)
                {
                    ReadMemoryConfig();
                    ReadSVI();

                    // Get first base address
                    dramBaseAddress = (uint)(OPS.GetDramBaseAddress() & 0xFFFFFFFF);
                    if (dramBaseAddress > 0)
                        ReadPowerConfig();
                    else
                        compatMode = true;

                    StartAutoRefresh();
                }
            }
            catch (ApplicationException ex)
            {
                AdonisUI.Controls.MessageBox.Show(ex.Message, "Error", AdonisUI.Controls.MessageBoxButton.OK, AdonisUI.Controls.MessageBoxImage.Error);
                //Dispose();
                ExitApplication();
            }
        }
        
		private void ShowWindow()
        {
            Show();
            Activate();
            BringIntoView();
            WindowState = WindowState.Normal;
        }

        private static void MinimizeFootprint()
        {
            InteropMethods.EmptyWorkingSet(Process.GetCurrentProcess().Handle);
        }

        public void SetWindowTitle()
        {
            var AssemblyTitle = "ZT";

            if (settings.AdvancedMode)
                AssemblyTitle = ((AssemblyTitleAttribute)Attribute.GetCustomAttribute(
                    Assembly.GetExecutingAssembly(),
                    typeof(AssemblyTitleAttribute), false)).Title;

            var AssemblyVersion = ((AssemblyFileVersionAttribute)Attribute.GetCustomAttribute(
                Assembly.GetExecutingAssembly(),
                typeof(AssemblyFileVersionAttribute), false)).Version;

            Title = $"{AssemblyTitle} {AssemblyVersion.Substring(0, AssemblyVersion.LastIndexOf('.'))}";
#if BETA
            Title = $@"{AssemblyTitle} {AssemblyVersion} beta";

            AdonisUI.Controls.MessageBox.Show("This is a BETA version of the application. Some functions might be working incorrectly.\n\n" +
                "Please report if something is not working as expected.", "Beta version", AdonisUI.Controls.MessageBoxButton.OK);
#else
#if DEBUG
            Title = $@"{AssemblyTitle} {AssemblyVersion} (debug)";
#endif
#endif
            if (compatMode && settings.AdvancedMode)
                Title += " (compatibility)";
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            SetWindowTitle();
            labelCPU.Text = SI.CpuName;
            labelMB.Text = $"{SI.MbName} | BIOS {SI.BiosVersion} | SMU {SI.GetSmuVersionString()}";
#if DEBUG
            /*foreach (TextWriterTraceListener listener in listeners)
            {
                listener.Flush();
            }
            Debug.Listeners.AddRange(listeners);*/
#endif
            //ShowWindow();

            MinimizeFootprint();
        }
		
		private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
		{
		    if (msg == InteropMethods.WM_SHOWME)
                ShowWindow();
		
		    return IntPtr.Zero;
		}

        private void DebugToolstripItem_Click(object sender, RoutedEventArgs e)
        {
            var parent = Application.Current.MainWindow;
            DebugDialog debugWnd = new DebugDialog(dramBaseAddress, modules, MEMCFG, SI, BMC, PowerTable, OPS)
            {
                Owner = parent
            };
            debugWnd.Width = parent.Width;
            debugWnd.Height = parent.Height;
            debugWnd.Show();
        }

        private void AdonisWindow_StateChanged(object sender, EventArgs e) => MinimizeFootprint();

        private void AdonisWindow_SizeChanged(object sender, SizeChangedEventArgs e) => MinimizeFootprint();

        private void AdonisWindow_Activated(object sender, EventArgs e) => MinimizeFootprint();

        private void ExitToolStripMenuItem_Click(object sender, RoutedEventArgs e) => ExitApplication();

        private void AdonisWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var handle = new WindowInteropHelper(Application.Current.MainWindow).Handle;
            if (handle == null)
                return;

            var source = HwndSource.FromHwnd(handle);
            source.AddHook(WndProc);
        }

        private void OptionsToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            OptionsDialog optionsWnd = new OptionsDialog(settings, PowerCfgTimer)
            {
                Owner = Application.Current.MainWindow
            };
            optionsWnd.ShowDialog();
        }

        private void AboutToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            AboutDialog AboutWnd = new AboutDialog()
            {
                Owner = Application.Current.MainWindow
            };
            AboutWnd.ShowDialog();
        }

        private void ButtonScreenshot_Click(object sender, RoutedEventArgs e)
        {
            Screenshot screenshot = new Screenshot();
            Bitmap bitmap = screenshot.CaptureActiveWindow();

            using (SaveWindow saveWnd = new SaveWindow(bitmap))
            {
                saveWnd.Owner = Application.Current.MainWindow;
                saveWnd.ShowDialog();
                screenshot.Dispose();
            }
        }
    }
}
