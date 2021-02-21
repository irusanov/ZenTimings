//#define BETA

using AdonisUI.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ZenStates.Core;
using ZenTimings.Windows;

namespace ZenTimings
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : AdonisWindow
    {
        private readonly List<MemoryModule> modules = new List<MemoryModule>();
        private readonly List<BiosACPIFunction> biosFunctions = new List<BiosACPIFunction>();
        private readonly Cpu cpu = new Cpu();
        private readonly MemoryConfig MEMCFG = new MemoryConfig();
        private readonly BiosMemController BMC;
        private readonly PowerTable PowerTable;
        private readonly uint dramBaseAddress = 0;
        private readonly AppSettings settings = new AppSettings().Load();
        private readonly uint[] table;
        private readonly DispatcherTimer PowerCfgTimer = new DispatcherTimer();
        private bool compatMode = false;
        private readonly AsusWMI AsusWmi = new AsusWMI();

        private static void ExitApplication() => Application.Current.Shutdown();

        private BiosACPIFunction GetFunctionByIdString(string name)
        {
            return biosFunctions.Find(x => x.IDString == name);
        }

        private void ReadChannelsInfo()
        {
            int dimmIndex = 0;

            // Get the offset by probing the IMC0 to IMC7
            // It appears that offsets 0x80 and 0x84 are DIMM config registers
            // When a DIMM is DR, bit 0 is set to 1
            // 0x50000
            // offset 0, bit 0 when set to 1 means DIMM1 is installed
            // offset 8, bit 0 when set to 1 means DIMM2 is installed
            for (var i = 0; i < 8; i++)
            {
                uint channelOffset = (uint)i << 20;
                bool channel = cpu.utils.GetBits(cpu.ReadDword(channelOffset | 0x50DF0), 19, 1) == 0;
                bool dimm1 = cpu.utils.GetBits(cpu.ReadDword(channelOffset | 0x50000), 0, 1) == 1;
                bool dimm2 = cpu.utils.GetBits(cpu.ReadDword(channelOffset | 0x50008), 0, 1) == 1;

                if (channel && (dimm1 || dimm2))
                {
                    if (dimm1)
                    {
                        MemoryModule module = modules[dimmIndex++];
                        module.Slot = $"{Convert.ToChar(i + 65)}1";
                        module.DctOffset = channelOffset;
                        module.DualRank = cpu.utils.GetBits(cpu.ReadDword(channelOffset | 0x50080), 0, 1) == 1;
                    }

                    if (dimm2)
                    {
                        MemoryModule module = modules[dimmIndex++];
                        module.Slot = $"{Convert.ToChar(i + 65)}2";
                        module.DctOffset = channelOffset;
                        module.DualRank = cpu.utils.GetBits(cpu.ReadDword(channelOffset | 0x50084), 0, 1) == 1;
                    }
                }
            }
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

                        modules.Add(new MemoryModule(partNumber.Trim(), bankLabel.Trim(), manufacturer.Trim(), deviceLocator, capacity, clockSpeed));

                        //string bl = bankLabel.Length > 0 ? new string(bankLabel.Where(char.IsDigit).ToArray()) : "";
                        //string dl = deviceLocator.Length > 0 ? new string(deviceLocator.Where(char.IsDigit).ToArray()) : "";

                        //comboBoxPartNumber.Items.Add($"#{bl}: {partNumber}");
                        //comboBoxPartNumber.SelectedIndex = 0;
                    }

                    ReadChannelsInfo();

                    if (modules.Count > 0)
                    {
                        ulong totalCapacity = 0UL;

                        foreach (var module in modules)
                        {
                            string rank = module.DualRank ? "DR" : "SR";
                            totalCapacity += module.Capacity;
                            comboBoxPartNumber.Items.Add($"{module.Slot}: {module.PartNumber} ({module.Capacity / 1024 / (1024 * 1024)}GB, {rank})");
                        }

                        if (modules.FirstOrDefault().ClockSpeed != 0)
                            MEMCFG.Frequency = modules.FirstOrDefault().ClockSpeed;

                        if (totalCapacity != 0)
                            MEMCFG.TotalCapacity = $"{totalCapacity / 1024 / (1024 * 1024)}GB";

                        comboBoxPartNumber.SelectedIndex = 0;
                        comboBoxPartNumber.SelectionChanged += new SelectionChangedEventHandler(ComboBoxPartNumber_SelectionChanged);
                    }
                }
                catch
                {
                    AdonisUI.Controls.MessageBox.Show(
                        "Failed to get installed memory parameters. Corresponding fields will be empty.",
                        "Warning",
                        AdonisUI.Controls.MessageBoxButton.OK,
                        AdonisUI.Controls.MessageBoxImage.Warning);
                }
            }
        }

        private void ReadPowerTable()
        {
            if (dramBaseAddress > 0)
            {
                for (int i = 0; i < table.Length; ++i)
                {
                    cpu.utils.GetPhysLong((UIntPtr)(dramBaseAddress + (i * 4)), out uint data);
                    table[i] = data;
                }

                if (table.Any(v => v != 0))
                {
                    PowerTable.ConfiguredClockSpeed = MEMCFG.Frequency;
                    PowerTable.MemRatio = MEMCFG.Ratio;
                    PowerTable.Table = table;
                }
            }
        }

        private void ReadPowerConfig()
        {
            if (dramBaseAddress > 0)
            {
                try
                {
                    if (cpu.TransferTableToDram() != SMU.Status.OK)
                        return;

                    ReadPowerTable();
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
            ushort timeout = 20;
            uint plane1_value;
            do
                plane1_value = cpu.ReadDword(cpu.info.SVI2.SocAddress);
            while ((plane1_value & 0xFF00) != 0 && --timeout > 0);

            if (timeout > 0)
            {
                uint vddcr_soc = (plane1_value >> 16) & 0xFF;
                textBoxVSOC_SVI2.Text = $"{cpu.utils.VidToVoltage(vddcr_soc):F4}V";
            }
            //uint vcore = (ops.ReadDword(cpu.info.SVI2.CoreAddress) >> 16) & 0xFF;
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

                byte[] apcbConfig = WMI.RunCommand(classInstance, cmd.ID);

                cmd = GetFunctionByIdString("Get memory voltages");
                if (cmd != null)
                {
                    byte[] voltages = WMI.RunCommand(classInstance, cmd.ID);

                    // MEM_VDDIO is ushort, offset 27
                    // MEM_VTT is ushort, offset 29
                    for (var i = 27; i <= 30; i++)
                    {
                        byte value = voltages[i];
                        if (value > 0)
                            apcbConfig[i] = value;
                    }
                }

                BMC.Table = apcbConfig;
                bool allZero = !BMC.Table.Any(v => v != 0);

                // When ProcODT is 0, then all other resistance values are 0
                // Happens when one DIMM installed in A1 or A2 slot
                if (allZero || BMC.Table == null || BMC.Config.ProcODT < 1)
                {
                    throw new Exception();
                }

                float vdimm = Convert.ToSingle(Convert.ToDecimal(BMC.Config.MemVddio) / 1000);
                if (vdimm > 0)
                {
                    textBoxMemVddio.Text = $"{vdimm:F4}V";
                }
                else if (AsusWmi != null && AsusWmi.Status == 1)
                {
                    var sensor = AsusWmi.FindSensorByName("DRAM Voltage");
                    if (sensor != null)
                        textBoxMemVddio.Text = sensor.Value;
                }
                else
                {
                    labelMemVddio.IsEnabled = false;
                }

                float vtt = Convert.ToSingle(Convert.ToDecimal(BMC.Config.MemVtt) / 1000);
                if (vtt > 0)
                    textBoxMemVtt.Text = $"{vtt:F4}V";
                else
                    labelMemVtt.IsEnabled = false;

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

            BMC.Dispose();
        }

        private void ReadTimings(uint offset = 0)
        {
            uint powerDown = cpu.ReadDword(offset | 0x5012C);
            uint umcBase = cpu.ReadDword(offset | 0x50200);
            uint bgsa0 = cpu.ReadDword(offset | 0x500D0);
            uint bgsa1 = cpu.ReadDword(offset | 0x500D4);
            uint bgs0 = cpu.ReadDword(offset | 0x50050);
            uint bgs1 = cpu.ReadDword(offset | 0x50058);
            uint timings5 = cpu.ReadDword(offset | 0x50204);
            uint timings6 = cpu.ReadDword(offset | 0x50208);
            uint timings7 = cpu.ReadDword(offset | 0x5020C);
            uint timings8 = cpu.ReadDword(offset | 0x50210);
            uint timings9 = cpu.ReadDword(offset | 0x50214);
            uint timings10 = cpu.ReadDword(offset | 0x50218);
            uint timings11 = cpu.ReadDword(offset | 0x5021C);
            uint timings12 = cpu.ReadDword(offset | 0x50220);
            uint timings13 = cpu.ReadDword(offset | 0x50224);
            uint timings14 = cpu.ReadDword(offset | 0x50228);
            uint timings15 = cpu.ReadDword(offset | 0x50230);
            uint timings16 = cpu.ReadDword(offset | 0x50234);
            uint timings17 = cpu.ReadDword(offset | 0x50250);
            uint timings18 = cpu.ReadDword(offset | 0x50254);
            uint timings19 = cpu.ReadDword(offset | 0x50258);
            uint timings20 = cpu.ReadDword(offset | 0x50260);
            uint timings21 = cpu.ReadDword(offset | 0x50264);
            uint timings22 = cpu.ReadDword(offset | 0x5028C);
            uint timings23 = timings20 != timings21 ? (timings20 != 0x21060138 ? timings20 : timings21) : timings20;

            float configured = MEMCFG.Frequency;
            float ratio = cpu.utils.GetBits(umcBase, 0, 7) / 3.0f;
            float freqFromRatio = ratio * 200;

            MEMCFG.Ratio = ratio;

            // Fallback to ratio when ConfiguredClockSpeed fails
            if (configured == 0.0f || freqFromRatio > configured)
            {
                MEMCFG.Frequency = freqFromRatio;
            }

            MEMCFG.BGS = (bgs0 == 0x87654321 && bgs1 == 0x87654321) ? "Disabled" : "Enabled";
            MEMCFG.BGSAlt = cpu.utils.GetBits(bgsa0, 4, 7) > 0 || cpu.utils.GetBits(bgsa1, 4, 7) > 0 ? "Enabled" : "Disabled";
            MEMCFG.GDM = cpu.utils.GetBits(umcBase, 11, 1) > 0 ? "Enabled" : "Disabled";
            MEMCFG.Cmd2T = cpu.utils.GetBits(umcBase, 10, 1) > 0 ? "2T" : "1T";

            MEMCFG.CL = cpu.utils.GetBits(timings5, 0, 6);
            MEMCFG.RAS = cpu.utils.GetBits(timings5, 8, 7);
            MEMCFG.RCDRD = cpu.utils.GetBits(timings5, 16, 6);
            MEMCFG.RCDWR = cpu.utils.GetBits(timings5, 24, 6);

            MEMCFG.RC = cpu.utils.GetBits(timings6, 0, 8);
            MEMCFG.RP = cpu.utils.GetBits(timings6, 16, 6);

            MEMCFG.RRDS = cpu.utils.GetBits(timings7, 0, 5);
            MEMCFG.RRDL = cpu.utils.GetBits(timings7, 8, 5);
            MEMCFG.RTP = cpu.utils.GetBits(timings7, 24, 5);

            MEMCFG.FAW = cpu.utils.GetBits(timings8, 0, 8);

            MEMCFG.CWL = cpu.utils.GetBits(timings9, 0, 6);
            MEMCFG.WTRS = cpu.utils.GetBits(timings9, 8, 5);
            MEMCFG.WTRL = cpu.utils.GetBits(timings9, 16, 7);

            MEMCFG.WR = cpu.utils.GetBits(timings10, 0, 8);

            MEMCFG.TRCPAGE = cpu.utils.GetBits(timings11, 20, 12);

            MEMCFG.RDRDDD = cpu.utils.GetBits(timings12, 0, 4);
            MEMCFG.RDRDSD = cpu.utils.GetBits(timings12, 8, 4);
            MEMCFG.RDRDSC = cpu.utils.GetBits(timings12, 16, 4);
            MEMCFG.RDRDSCL = cpu.utils.GetBits(timings12, 24, 6);

            MEMCFG.WRWRDD = cpu.utils.GetBits(timings13, 0, 4);
            MEMCFG.WRWRSD = cpu.utils.GetBits(timings13, 8, 4);
            MEMCFG.WRWRSC = cpu.utils.GetBits(timings13, 16, 4);
            MEMCFG.WRWRSCL = cpu.utils.GetBits(timings13, 24, 6);

            MEMCFG.RDWR = cpu.utils.GetBits(timings14, 8, 5);
            MEMCFG.WRRD = cpu.utils.GetBits(timings14, 0, 4);

            MEMCFG.REFI = cpu.utils.GetBits(timings15, 0, 16);

            MEMCFG.MODPDA = cpu.utils.GetBits(timings16, 24, 6);
            MEMCFG.MRDPDA = cpu.utils.GetBits(timings16, 16, 6);
            MEMCFG.MOD = cpu.utils.GetBits(timings16, 8, 6);
            MEMCFG.MRD = cpu.utils.GetBits(timings16, 0, 6);

            MEMCFG.STAG = cpu.utils.GetBits(timings17, 16, 8);

            MEMCFG.XP = cpu.utils.GetBits(timings18, 0, 6);
            MEMCFG.CKE = cpu.utils.GetBits(timings18, 24, 5);

            MEMCFG.PHYWRL = cpu.utils.GetBits(timings19, 8, 5);
            MEMCFG.PHYRDL = cpu.utils.GetBits(timings19, 16, 6);
            MEMCFG.PHYWRD = cpu.utils.GetBits(timings19, 24, 3);

            MEMCFG.RFC = cpu.utils.GetBits(timings23, 0, 11);
            MEMCFG.RFC2 = cpu.utils.GetBits(timings23, 11, 11);
            MEMCFG.RFC4 = cpu.utils.GetBits(timings23, 22, 11);

            MEMCFG.PowerDown = cpu.utils.GetBits(powerDown, 28, 1) == 1 ? "Enabled" : "Disabled";
        }

        private bool WaitForDriverLoad()
        {
            Stopwatch timer = new Stopwatch();
            timer.Start();

            bool temp;
            // Refresh until driver is opened
            do
                temp = cpu.utils.IsInpOutDriverOpen();
            while (!temp && timer.Elapsed.TotalMilliseconds < 10000);

            timer.Stop();

            return temp;
        }

        private bool WaitForPowerTable()
        {
            if (dramBaseAddress == 0)
            {
                HandleError("Could not get DRAM base address.\nClose the application and try again.");
                return false;
            }

            if (WaitForDriverLoad() && cpu.utils.WinIoStatus == Utils.LibStatus.OK)
            {
                Stopwatch timer = new Stopwatch();
                timer.Start();
                short timeout = 10000;

                uint temp;
                SMU.Status status;

                // Refresh until table is transferred to DRAM or timeout
                do
                {
                    status = cpu.TransferTableToDram();
                    cpu.utils.GetPhysLong((UIntPtr)dramBaseAddress, out temp);
                }
                while ((temp == 0 || status != SMU.Status.OK) && timer.Elapsed.TotalMilliseconds < timeout);

                timer.Stop();

                if (temp == 0)
                    HandleError("Could not get power table.\nSkipping power table.");

                return temp != 0;
            }
            else
            {
                HandleError("I/O driver is not responding or not loaded.");
                return false;
            }
        }

        private void StartAutoRefresh()
        {
            if (settings.AutoRefresh && settings.AdvancedMode)
            {
                PowerCfgTimer.Interval = TimeSpan.FromMilliseconds(settings.AutoRefreshInterval);
                PowerCfgTimer.Start();
            }
        }

        private void PowerCfgTimer_Tick(object sender, EventArgs e)
        {
            if (AsusWmi != null && AsusWmi.Status == 1)
            {
                AsusWmi.UpdateSensors();
                var sensor = AsusWmi.FindSensorByName("DRAM Voltage");
                if (sensor != null)
                    textBoxMemVddio.Text = $"{sensor.Value}";
            }
            //ReadTimings();
            //ReadMemoryConfig();
            ReadSVI();
            ReadPowerConfig();
        }

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
            try
            {
                SplashWindow.Loading("CPU");

                IconSource = GetIcon("pack://application:,,,/ZenTimings;component/Resources/ZenTimings.ico", 16);

                if (cpu.info.family != Cpu.Family.FAMILY_17H && cpu.info.family != Cpu.Family.FAMILY_19H)
                {
                    HandleError("CPU is not supported.");
                    ExitApplication();
                }
                else if (cpu.info.codeName == Cpu.CodeName.Unsupported)
                {
                    HandleError("CPU model is not supported.\n" +
                        "Please run a debug report and send to the developer.");
                }

                if (settings.DarkMode)
                    settings.ChangeTheme();

                InitializeComponent();
                SplashWindow.Loading("Memory modules");
                ReadMemoryModulesInfo();
                SplashWindow.Loading("Timings");
                // Read from first enabled DCT
                ReadTimings(modules[0].DctOffset);

                if (settings.AdvancedMode)
                {
                    if (cpu.info.codeName != Cpu.CodeName.Unsupported)
                    {
                        // Get first base address
                        dramBaseAddress = (uint)(cpu.GetDramBaseAddress() & 0xFFFFFFFF);
                        PowerTable = new PowerTable(cpu.smu.TableVersion, cpu.smu.SMU_TYPE);
                        PowerCfgTimer.Interval = TimeSpan.FromMilliseconds(2000);
                        PowerCfgTimer.Tick += new EventHandler(PowerCfgTimer_Tick);

                        table = new uint[PowerTable.tableSize / 4];

                        SplashWindow.Loading("SVI2");
                        ReadSVI();

                        SplashWindow.Loading("Waiting for power table");
                        if (WaitForPowerTable())
                        {
                            SplashWindow.Loading("Reading power table");
                            ReadPowerTable();
                        }
                        else
                        {
                            SplashWindow.Loading("Power table error!");
                        }

                        StartAutoRefresh();
                    }
                    else
                    {
                        PowerTable = new PowerTable(0, cpu.smu.SMU_TYPE);
                    }

                    if (!AsusWmi.Init())
                    {
                        AsusWmi.Dispose();
                        AsusWmi = null;
                    }

                    SplashWindow.Loading("Memory controller");
                    BMC = new BiosMemController();
                    ReadMemoryConfig();
                }

                SplashWindow.Loading("Done");

                DataContext = new
                {
                    timings = MEMCFG,
                    powerTable = PowerTable,
                    WMIPresent = !compatMode,
                    settings
                };
            }
            catch (ApplicationException ex)
            {
                HandleError(ex.Message);
                //Dispose();
                ExitApplication();
            }
        }

        public void HandleError(string message, string title = "Error")
        {
            AdonisUI.Controls.MessageBox.Show(
                message,
                title,
                AdonisUI.Controls.MessageBoxButton.OK,
                AdonisUI.Controls.MessageBoxImage.Error
            );
        }

        private void Restart()
        {
            settings.IsRestarting = true;
            settings.Save();
            Process.Start(Application.ResourceAssembly.Location);
            Application.Current.Shutdown();
        }

        private void ShowWindow()
        {
            Show();
            Activate();
            BringIntoView();
            WindowState = WindowState.Normal;
            MinimizeFootprint();
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
            labelCPU.Text = cpu.systemInfo.CpuName;
            labelMB.Text = $"{cpu.systemInfo.MbName} | BIOS {cpu.systemInfo.BiosVersion} | SMU {cpu.systemInfo.GetSmuVersionString()}";
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
            if (settings.AdvancedMode)
            {
                var parent = Application.Current.MainWindow;
                DebugDialog debugWnd = new DebugDialog(dramBaseAddress, modules, MEMCFG, BMC, PowerTable, AsusWmi, cpu)
                {
                    Owner = parent
                };
                debugWnd.Width = parent.Width;
                debugWnd.Height = parent.Height;
                debugWnd.ShowDialog();
            }
            else
            {
                var messageBox = new AdonisUI.Controls.MessageBoxModel
                {
                    Text = "Debug functionality requires Advanced Mode.\n\n" +
                    "Do you want to enable it now (the application will restart automatically)?",
                    Caption = "Debug Report",
                    Buttons = AdonisUI.Controls.MessageBoxButtons.YesNoCancel()
                };

                AdonisUI.Controls.MessageBox.Show(messageBox);

                if (messageBox.Result == AdonisUI.Controls.MessageBoxResult.Yes)
                {
                    settings.AdvancedMode = true;
                    Restart();
                }
            }
        }

        private void AdonisWindow_StateChanged(object sender, EventArgs e) => MinimizeFootprint();

        private void AdonisWindow_SizeChanged(object sender, SizeChangedEventArgs e) => MinimizeFootprint();

        private void AdonisWindow_Activated(object sender, EventArgs e) => MinimizeFootprint();

        private void ExitToolStripMenuItem_Click(object sender, RoutedEventArgs e) => ExitApplication();

        private void AdonisWindow_Loaded(object sender, RoutedEventArgs e)
        {
            SplashWindow.Stop();

            Application.Current.MainWindow = this;

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

        private void ComboBoxPartNumber_SelectionChanged(object sender, RoutedEventArgs e)
        {
            ComboBox combo = sender as ComboBox;
            ReadTimings(modules[combo.SelectedIndex].DctOffset);
        }

        private void SystemInfoToolstripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SystemInfoWindow siWnd = new SystemInfoWindow(cpu.systemInfo, MEMCFG, AsusWmi?.sensors)
            {
                Owner = this
            };
            siWnd.Width = this.Width;
            siWnd.Height = this.Height;
            siWnd.Show();
        }
    }

    public class FloatToNAConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if ((float)value == 0)
                return "N/A";
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    public class FloatToVoltageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if ((float)value == 0)
                return "N/A";
            return $"{value:F4}V";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    public class FloatToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (float)value != 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
