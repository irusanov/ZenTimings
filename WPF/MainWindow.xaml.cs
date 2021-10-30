//#define BETA

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
using AdonisUI.Controls;
using ZenStates.Core;
using ZenTimings.Windows;
using MessageBox = AdonisUI.Controls.MessageBox;
using MessageBoxButton = AdonisUI.Controls.MessageBoxButton;
using MessageBoxImage = AdonisUI.Controls.MessageBoxImage;
using MessageBoxResult = AdonisUI.Controls.MessageBoxResult;

namespace ZenTimings
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private readonly AsusWMI AsusWmi = new AsusWMI();
        private readonly List<BiosACPIFunction> biosFunctions = new List<BiosACPIFunction>();
        private readonly BiosMemController BMC;
        private readonly Cpu cpu = new Cpu();
        private readonly MemoryConfig MEMCFG = new MemoryConfig();
        private readonly List<MemoryModule> modules = new List<MemoryModule>();
        private readonly DispatcherTimer PowerCfgTimer = new DispatcherTimer();
        private readonly AppSettings settings = (Application.Current as App)?.settings;
        private bool compatMode;

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

                InitializeComponent();
                SplashWindow.Loading("Memory modules");
                ReadMemoryModulesInfo();
                SplashWindow.Loading("Timings");

                // Read from first enabled DCT
                if (modules.Count > 0)
                    ReadTimings(modules[0].DctOffset);
                else
                    ReadTimings();

                if (settings.AdvancedMode)
                {
                    if (cpu.info.codeName != Cpu.CodeName.Unsupported)
                    {
                        PowerCfgTimer.Interval = TimeSpan.FromMilliseconds(2000);
                        PowerCfgTimer.Tick += PowerCfgTimer_Tick;

                        SplashWindow.Loading("SVI2");
                        ReadSVI();

                        SplashWindow.Loading("Waiting for power table");
                        if (WaitForPowerTable())
                        {
                            // refresh the table again, to avoid displaying initial fclk, mclk and uclk values,
                            // which seem to be a little off when transferring the table for the "first" time,
                            // after an idle period
                            RefreshPowerTable();
                            SplashWindow.Loading("Reading power table");
                        }
                        else
                        {
                            SplashWindow.Loading("Power table error!");
                        }

                        if (!AsusWmi.Init())
                        {
                            AsusWmi.Dispose();
                            AsusWmi = null;
                        }

                        StartAutoRefresh();
                    }

                    SplashWindow.Loading("Memory controller");
                    BMC = new BiosMemController();
                    ReadMemoryConfig();
                }

                SplashWindow.Loading("Done");

                DataContext = new
                {
                    timings = MEMCFG,
                    cpu.powerTable,
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

        private static void ExitApplication()
        {
            Application.Current.Shutdown();
        }

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
                bool connected = false;
                try
                {
                    WMI.Connect(@"root\cimv2");

                    connected = true;

                    foreach (var queryObject in searcher.Get().Cast<ManagementObject>())
                    {
                        var capacity = 0UL;
                        var clockSpeed = 0U;
                        var partNumber = "N/A";
                        var bankLabel = "";
                        var manufacturer = "";
                        var deviceLocator = "";

                        var temp = WMI.TryGetProperty(queryObject, "Capacity");
                        if (temp != null) capacity = (ulong) temp;

                        temp = WMI.TryGetProperty(queryObject, "ConfiguredClockSpeed");
                        if (temp != null) clockSpeed = (uint) temp;

                        temp = WMI.TryGetProperty(queryObject, "partNumber");
                        if (temp != null) partNumber = (string) temp;

                        temp = WMI.TryGetProperty(queryObject, "BankLabel");
                        if (temp != null) bankLabel = (string) temp;

                        temp = WMI.TryGetProperty(queryObject, "Manufacturer");
                        if (temp != null) manufacturer = (string) temp;

                        temp = WMI.TryGetProperty(queryObject, "DeviceLocator");
                        if (temp != null) deviceLocator = (string) temp;

                        modules.Add(new MemoryModule(partNumber.Trim(), bankLabel.Trim(), manufacturer.Trim(),
                            deviceLocator, capacity, clockSpeed));

                        //string bl = bankLabel.Length > 0 ? new string(bankLabel.Where(char.IsDigit).ToArray()) : "";
                        //string dl = deviceLocator.Length > 0 ? new string(deviceLocator.Where(char.IsDigit).ToArray()) : "";

                        //comboBoxPartNumber.Items.Add($"#{bl}: {partNumber}");
                        //comboBoxPartNumber.SelectedIndex = 0;
                    }
                }
                catch (Exception ex)
                {
                    var title = connected ? @"Failed to get installed memory parameters." : $@"{ex.Message}";
                    MessageBox.Show(
                        title,
                        "Warning",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }

            if (modules.Count > 0)
            {
                ReadChannelsInfo();

                var totalCapacity = 0UL;

                foreach (var module in modules)
                {
                    var rank = module.DualRank ? "DR" : "SR";
                    totalCapacity += module.Capacity;
                    comboBoxPartNumber.Items.Add(
                        $"{module.Slot}: {module.PartNumber} ({module.Capacity / 1024 / (1024 * 1024)}GB, {rank})");
                }

                if (modules[0].ClockSpeed != 0)
                    MEMCFG.Frequency = modules[0].ClockSpeed;

                if (totalCapacity != 0)
                    MEMCFG.TotalCapacity = $"{totalCapacity / 1024 / (1024 * 1024)}GB";

                comboBoxPartNumber.SelectedIndex = 0;
                comboBoxPartNumber.SelectionChanged += ComboBoxPartNumber_SelectionChanged;
            }
        }

        private bool RefreshPowerTable()
        {
            return cpu.RefreshPowerTable() == SMU.Status.OK;
        }

        private void ReadSVI()
        {
            ushort timeout = 20;
            uint plane1_value;
            do
            {
                plane1_value = cpu.ReadDword(cpu.info.svi2.socAddress);
            } while ((plane1_value & 0xFF00) != 0 && --timeout > 0);

            if (timeout > 0)
            {
                var vddcr_soc = (plane1_value >> 16) & 0xFF;
                textBoxVSOC_SVI2.Text = $"{cpu.utils.VidToVoltage(vddcr_soc):F4}V";
            }
            //uint vcore = (ops.ReadDword(cpu.info.SVI2.CoreAddress) >> 16) & 0xFF;
        }

        private void ReadMemoryConfig()
        {
            var scope = @"root\wmi";
            var className = "AMD_ACPI";

            try
            {
                WMI.Connect($@"{scope}");

                var instanceName = WMI.GetInstanceName(scope, className);

                var classInstance = new ManagementObject(scope,
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
                }
                */


                // Get function names with their IDs
                string[] functionObjects = {"GetObjectID", "GetObjectID2"};
                foreach (var functionObject in functionObjects)
                {
                    try
                    {
                        var pack = WMI.InvokeMethod(classInstance, functionObject, "pack", null, 0);
                        if (pack != null)
                        {
                            var ID = (uint[]) pack.GetPropertyValue("ID");
                            var IDString = (string[]) pack.GetPropertyValue("IDString");
                            var Length = (byte) pack.GetPropertyValue("Length");

                            for (var i = 0; i < Length; ++i)
                            {
                                biosFunctions.Add(new BiosACPIFunction(IDString[i], ID[i]));
                                Debug.WriteLine("{0}: {1:X8}", IDString[i], ID[i]);
                            }
                        }
                    }
                    catch
                    {
                        // ignored
                    }
                }

                // Get APCB config from BIOS. Holds memory parameters.
                BiosACPIFunction cmd = GetFunctionByIdString("Get APCB Config");
                if (cmd == null)
                    throw new Exception();

                var apcbConfig = WMI.RunCommand(classInstance, cmd.ID);

                cmd = GetFunctionByIdString("Get memory voltages");
                if (cmd != null)
                {
                    var voltages = WMI.RunCommand(classInstance, cmd.ID);

                    // MEM_VDDIO is ushort, offset 27
                    // MEM_VTT is ushort, offset 29
                    for (var i = 27; i <= 30; i++)
                    {
                        var value = voltages[i];
                        if (value > 0)
                            apcbConfig[i] = value;
                    }
                }

                BMC.Table = apcbConfig;

                // When ProcODT is 0, then all other resistance values are 0
                // Happens when one DIMM installed in A1 or A2 slot
                if (BMC.Table == null || cpu.utils.AllZero(BMC.Table) || BMC.Config.ProcODT < 1) throw new Exception();

                var vdimm = Convert.ToSingle(Convert.ToDecimal(BMC.Config.MemVddio) / 1000);
                if (vdimm > 0)
                {
                    textBoxMemVddio.Text = $"{vdimm:F4}V";
                }
                else if (AsusWmi != null && AsusWmi.Status == 1)
                {
                    var sensor = AsusWmi.FindSensorByName("DRAM Voltage");
                    if (sensor != null)
                        textBoxMemVddio.Text = sensor.Value;
                    else
                        labelMemVddio.IsEnabled = false;
                }
                else
                {
                    labelMemVddio.IsEnabled = false;
                }

                var vtt = Convert.ToSingle(Convert.ToDecimal(BMC.Config.MemVtt) / 1000);
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

                MessageBox.Show(
                    "Failed to read AMD ACPI. Odt, Setup and Drive strength parameters will be empty.",
                    "Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
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


            MEMCFG.BGS = bgs0 == 0x87654321 && bgs1 == 0x87654321 ? "Disabled" : "Enabled";
            MEMCFG.BGSAlt = cpu.utils.GetBits(bgsa0, 4, 7) > 0 || cpu.utils.GetBits(bgsa1, 4, 7) > 0
                ? "Enabled"
                : "Disabled";
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
            var timer = new Stopwatch();
            timer.Start();

            bool temp;
            // Refresh until driver is opened
            do
            {
                temp = cpu.utils.IsInpOutDriverOpen();
            } while (!temp && timer.Elapsed.TotalMilliseconds < 10000);

            timer.Stop();

            return temp;
        }

        private bool WaitForPowerTable()
        {
            if (cpu.powerTable.DramBaseAddress == 0)
            {
                HandleError("Could not initialize power table.\nClose the application and try again.");
                return false;
            }

            if (WaitForDriverLoad() && cpu.utils.WinIoStatus == Utils.LibStatus.OK)
            {
                var timer = new Stopwatch();
                var timeout = 10000;

                cpu.powerTable.ConfiguredClockSpeed = MEMCFG.Frequency;
                cpu.powerTable.MemRatio = MEMCFG.Ratio;

                timer.Start();

                SMU.Status status;
                // Refresh each 2 seconds until table is transferred to DRAM or timeout
                do
                {
                    status = cpu.RefreshPowerTable();
                    if (status != SMU.Status.OK)
                        // It's ok to block the current thread
                        Thread.Sleep(2000);
                } while (status != SMU.Status.OK && timer.Elapsed.TotalMilliseconds < timeout);

                timer.Stop();

                if (status != SMU.Status.OK)
                {
                    HandleError("Could not get power table.\nSkipping.");
                    return false;
                }

                return true;
            }
            else
            {
                HandleError("I/O driver is not responding or not loaded.");
                return false;
            }
        }

        private void StartAutoRefresh()
        {
            if (settings.AutoRefresh && settings.AdvancedMode && !PowerCfgTimer.IsEnabled)
            {
                PowerCfgTimer.Interval = TimeSpan.FromMilliseconds(settings.AutoRefreshInterval);
                PowerCfgTimer.Start();
            }
        }

        private void StopAutoRefresh()
        {
            if (PowerCfgTimer.IsEnabled)
                PowerCfgTimer.Stop();
        }

        private void PowerCfgTimer_Tick(object sender, EventArgs e)
        {
            // Run refresh operation in a new thread
            try
            {
                new Thread(() =>
                {
                    Thread.CurrentThread.IsBackground = true;

                    if (AsusWmi != null && AsusWmi.Status == 1)
                    {
                        AsusWmi.UpdateSensors();
                        var sensor = AsusWmi.FindSensorByName("DRAM Voltage");
                        if (sensor != null)
                            Dispatcher.Invoke(DispatcherPriority.ApplicationIdle,
                                new Action(() => { textBoxMemVddio.Text = $"{sensor.Value}"; }));
                    }

                    Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() =>
                    {
                        //ReadTimings();
                        //ReadMemoryConfig();
                        RefreshPowerTable();
                        ReadSVI();
                    }));
                }).Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private ImageSource GetIcon(string iconSource, double width)
        {
            var decoder = BitmapDecoder.Create(new Uri(iconSource),
                BitmapCreateOptions.DelayCreation,
                BitmapCacheOption.OnDemand);

            var result = decoder.Frames.SingleOrDefault(f => f.Width == width);
            if (result == default(BitmapFrame)) result = decoder.Frames.OrderBy(f => f.Width).First();

            return result;
        }

        public void HandleError(string message, string title = "Error")
        {
            MessageBox.Show(
                message,
                title,
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }

        private void Restart()
        {
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
                AssemblyTitle = ((AssemblyTitleAttribute) Attribute.GetCustomAttribute(
                    Assembly.GetExecutingAssembly(),
                    typeof(AssemblyTitleAttribute), false)).Title;

            var AssemblyVersion = ((AssemblyFileVersionAttribute) Attribute.GetCustomAttribute(
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
            if (settings.SaveWindowPosition)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = settings.WindowLeft;
                Top = settings.WindowTop;
            }

            SetWindowTitle();
            labelCPU.Text = cpu.systemInfo.CpuName;
            labelMB.Text =
                $"{cpu.systemInfo.MbName} | BIOS {cpu.systemInfo.BiosVersion} | SMU {cpu.systemInfo.GetSmuVersionString()}";
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
                if (parent != null)
                {
                    var debugWnd = new DebugDialog(cpu, modules, MEMCFG, BMC, AsusWmi)
                    {
                        Owner = parent, Width = parent.Width, Height = parent.Height
                    };
                    debugWnd.ShowDialog();
                }
            }
            else
            {
                var messageBox = new MessageBoxModel
                {
                    Text = "Debug functionality requires Advanced Mode.\n\n" +
                           "Do you want to enable it now (the application will restart automatically)?",
                    Caption = "Debug Report",
                    Buttons = MessageBoxButtons.YesNoCancel()
                };

                MessageBox.Show(messageBox);

                if (messageBox.Result == MessageBoxResult.Yes)
                {
                    settings.AdvancedMode = true;
                    Restart();
                }
            }
        }

        private void AdonisWindow_StateChanged(object sender, EventArgs e)
        {
            // Do not refresh if app is minimized
            if (WindowState == WindowState.Minimized)
                StopAutoRefresh();
            else if (WindowState == WindowState.Normal)
                StartAutoRefresh();

            MinimizeFootprint();
        }

        private void AdonisWindow_SizeChanged(object sender, SizeChangedEventArgs e) => MinimizeFootprint();

        private void AdonisWindow_Activated(object sender, EventArgs e) => MinimizeFootprint();

        private void ExitToolStripMenuItem_Click(object sender, RoutedEventArgs e) => ExitApplication();

        private void AdonisWindow_Loaded(object sender, RoutedEventArgs e)
        {
            SplashWindow.Stop();

            Application.Current.MainWindow = this;

            var handle = new WindowInteropHelper(Application.Current.MainWindow).Handle;
            var source = HwndSource.FromHwnd(handle);

            source?.AddHook(WndProc);
        }

        private void OptionsToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var optionsWnd = new OptionsDialog(settings, PowerCfgTimer)
            {
                Owner = Application.Current.MainWindow
            };
            optionsWnd.ShowDialog();
        }

        private void AboutToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var aboutWnd = new AboutDialog()
            {
                Owner = Application.Current.MainWindow
            };
            aboutWnd.ShowDialog();
        }

        private void ButtonScreenshot_Click(object sender, RoutedEventArgs e)
        {
            var screenshot = new Screenshot();
            var bitmap = screenshot.CaptureActiveWindow();

            using (var saveWnd = new SaveWindow(bitmap))
            {
                saveWnd.Owner = Application.Current.MainWindow;
                saveWnd.ShowDialog();
                screenshot.Dispose();
            }
        }

        private void ComboBoxPartNumber_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (sender is ComboBox combo) ReadTimings(modules[combo.SelectedIndex].DctOffset);
        }

        private void SystemInfoToolstripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var siWnd = new SystemInfoWindow(cpu.systemInfo, MEMCFG, AsusWmi?.sensors)
            {
                Owner = this, Width = Width, Height = Height
            };
            siWnd.Show();
        }

        private void AdonisWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (settings.SaveWindowPosition)
            {
                settings.WindowLeft = Left;
                settings.WindowTop = Top;
                settings.Save();
            }
        }
    }

    public class FloatToNAConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value != null && (float) value == 0)
                return "N/A";
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter,
            CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    public class FloatToVoltageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value != null && (float) value == 0)
                return "N/A";
            return $"{value:F4}V";
        }

        public object ConvertBack(object value, Type targetType, object parameter,
            CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    public class FloatToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null && (float) value != 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter,
            CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}