//#define BETA

using AdonisUI.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ZenStates.Core;
using ZenStates.Core.DRAM;
using ZenTimings.Plugin;
using ZenTimings.Windows;
using static ZenStates.Core.DRAM.MemoryConfig;
using Forms = System.Windows.Forms;
using MessageBox = AdonisUI.Controls.MessageBox;
using MessageBoxButton = AdonisUI.Controls.MessageBoxButton;
using MessageBoxImage = AdonisUI.Controls.MessageBoxImage;
using MessageBoxResult = AdonisUI.Controls.MessageBoxResult;
//using OpenHardwareMonitor.Hardware;

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
        private readonly Cpu cpu;
        private readonly MemoryConfig MEMCFG = new MemoryConfig();
        private readonly List<MemoryModule> modules = new List<MemoryModule>();
        private readonly DispatcherTimer PowerCfgTimer = new DispatcherTimer();
        private readonly AppSettings settings = AppSettings.Instance;
        private readonly List<IPlugin> plugins = new List<IPlugin>();
        private SystemInfoWindow siWnd = null;
        internal readonly Forms.NotifyIcon _notifyIcon;
        private bool compatMode;
        //private Computer computer;

        private readonly string AssemblyProduct = ((AssemblyProductAttribute)Attribute.GetCustomAttribute(
            Assembly.GetExecutingAssembly(),
            typeof(AssemblyProductAttribute), false)).Product;

        private readonly string AssemblyVersion = ((AssemblyFileVersionAttribute)Attribute.GetCustomAttribute(
            Assembly.GetExecutingAssembly(),
            typeof(AssemblyFileVersionAttribute), false)).Version;

        public MainWindow()
        {
            try
            {
                SplashWindow.Loading("CPU");
                cpu = CpuSingleton.Instance;

                if (cpu.info.family.Equals(Cpu.Family.UNSUPPORTED))
                {
                    throw new ApplicationException("CPU is not supported.");
                }
                else if (cpu.info.codeName.Equals(Cpu.CodeName.Unsupported))
                {
                    MessageBox.Show(
                        "CPU model is not supported.\nPlease run a debug report and send to the developer.",
                        "Unsupported CPU Model",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );
                }
                /* else if (cpu.info.codeName.Equals(Cpu.CodeName.Rembrandt) && !settings.NotifiedRembrandt.Equals(AssemblyVersion))
                 {
                     MessageBox.Show(
                         "DDR5 support is experimental and Advanced mode is not supported yet."
                             + Environment.NewLine
                             + Environment.NewLine
                             + "You can still enable it in Tools -> Options, but it will most probably fail."
                             + Environment.NewLine
                             + Environment.NewLine
                             + "If you're not able to turn off the Advanced mode from the UI, edit settings.xml manually and set AdvancedMode to 'false'. "
                             + "You can also delete settings.xml file and it will be regenerated on next application launch.",
                         "Limited Support",
                         MessageBoxButton.OK,
                         MessageBoxImage.Information);
                     settings.AdvancedMode = false;
                     settings.NotifiedRembrandt = AssemblyVersion;
                     settings.Save();
                 }*/

                IconSource = GetIcon("pack://application:,,,/ZenTimings;component/Resources/ZenTimings2022.ico", 16);
                _notifyIcon = GetTrayIcon();

                InitializeComponent();
                SplashWindow.Loading("Memory modules");
                modules = cpu.GetMemoryConfig()?.Modules ?? new List<MemoryModule>();
                ReadMemoryModulesInfo();
                SplashWindow.Loading("Timings");

                // Read from first enabled DCT
                if (modules?.Count > 0)
                {
                    ReadTimings(modules[0].DctOffset);
                }
                else
                {
                    ReadTimings();
                }

                MEMCFG.TotalCapacity = cpu.GetMemoryConfig().TotalCapacity.ToString();

                if (cpu != null && settings.AdvancedMode)
                {

                    PowerCfgTimer.Interval = TimeSpan.FromMilliseconds(2000);
                    PowerCfgTimer.Tick += PowerCfgTimer_Tick;

                    SplashWindow.Loading("Waiting for power table");
                    if (WaitForPowerTable())
                    {
                        SplashWindow.Loading("Reading power table");
                        // refresh the table again, to avoid displaying initial fclk, mclk and uclk values,
                        // which seem to be a little off when transferring the table for the "first" time,
                        // after an idle period
                        cpu.RefreshPowerTable();
                    }
                    else
                    {
                        SplashWindow.Loading("Power table error!");
                    }

                    SplashWindow.Loading("Plugins");
                    SplashWindow.Loading("SVI2 Plugin");
                    plugins.Add(new SVI2Plugin(cpu));
                    //plugins.Add(new OHWMPlugin());
                    //plugins[1].Open();

                    ReadSVI();

                    if (!AsusWmi.Init())
                    {
                        AsusWmi.Dispose();
                        AsusWmi = null;
                    }

                    StartAutoRefresh();

                    SplashWindow.Loading("Memory controller");
                    BMC = new BiosMemController();
                    ReadMemoryConfig();
                }

                SplashWindow.Loading("Done");

                DataContext = new
                {
                    timings = MEMCFG,
                    cpu.powerTable,
                    cpu.info.codeName,
                    WMIPresent = !compatMode && cpu.GetMemoryConfig().Type == MemType.DDR4,
                    settings,
                    plugins,
                    IsRogMotherboard = IsRogMotherboard(cpu.systemInfo),
                    RogLogoTooltip = $"Click to visit {cpu.systemInfo.MbName} page",
                };
            }
            catch (Exception ex)
            {
                HandleError(ex.Message);
                ExitApplication();
            }
        }

        private Forms.NotifyIcon GetTrayIcon()
        {
            Forms.NotifyIcon notifyIcon = new Forms.NotifyIcon
            {
                Icon = Properties.Resources.ZenTimings2022
            };

            notifyIcon.MouseClick += NotifyIcon_MouseClick;
            notifyIcon.ContextMenuStrip = new Forms.ContextMenuStrip();
            notifyIcon.ContextMenuStrip.Items.Add($"{AssemblyProduct} {AssemblyVersion}", null, OnAppContextMenuItemClick);
            notifyIcon.ContextMenuStrip.Items.Add("-");
            notifyIcon.ContextMenuStrip.Items.Add("Exit", null, (object sender, EventArgs e) => ExitApplication());

            return notifyIcon;
        }

        private void OnAppContextMenuItemClick(object sender, EventArgs e)
        {
            WindowState = WindowState.Normal;
        }

        private void NotifyIcon_MouseClick(object sender, Forms.MouseEventArgs e)
        {
            if (e.Button == Forms.MouseButtons.Left)
            {
                WindowState = WindowState.Normal;
                Activate();
            }

            // else, default = show context menu
        }

        private void ExitApplication()
        {
            foreach (IPlugin plugin in plugins)
                plugin?.Close();

            _notifyIcon?.Dispose();
            AsusWmi?.Dispose();
            //cpu?.io?.Close(settings.AutoUninstallDriver);
            cpu?.Dispose();
            settings.Save();

            //Driver.Cleanup();

            Application.Current.Shutdown();
        }

        private BiosACPIFunction GetFunctionByIdString(string name)
        {
            return biosFunctions.Find(x => x.IDString == name);
        }

        private void ReadMemoryModulesInfo()
        {
            if (modules?.Count > 0)
            {
                foreach (MemoryModule module in modules)
                {
                    comboBoxPartNumber.Items.Add(module.ToString());
                }

                if (comboBoxPartNumber.Items.Count > 0)
                {
                    comboBoxPartNumber.SelectedIndex = 0;
                    comboBoxPartNumber.SelectionChanged += ComboBoxPartNumber_SelectionChanged;
                }
            }
        }

        private void RefreshSensors()
        {
            plugins[1].Update();
            /*
            foreach (var sensor in plugins[1].Sensors)
            {
                Console.WriteLine($"----Name: {sensor.Name}, Value: {sensor.Value}");
            }
            */
        }

        private void ReadSVI()
        {
            if (plugins[0].Update())
            {
                textBoxVSOC_SVI2.Text = $"{plugins[0].Sensors[0].Value:F4}V";
            }
        }

        private void ReadMemoryConfig()
        {
            string scope = @"root\wmi";
            string className = "AMD_ACPI";

            try
            {
                WMI.Connect($@"{scope}");

                string instanceName = WMI.GetInstanceName(scope, className);

                ManagementObject classInstance = new ManagementObject(scope,
                    $"{className}.InstanceName='{instanceName}'",
                    null);

                /* // Get possible values (index) of a memory option in BIOS
                 var dvaluesPack = WMI.InvokeMethod(classInstance, "Getdvalues", "pack", "ID", 0x20035);
                 if (dvaluesPack != null)
                 {
                     uint[] DValuesBuffer = (uint[])dvaluesPack.GetPropertyValue("DValuesBuffer");
                     for (var i = 0; i < DValuesBuffer.Length; i++)
                     {
                         Debug.WriteLine("{0}", DValuesBuffer[i]);
                     }
                 }*/

                // Get function names with their IDs
                string[] functionObjects = { "GetObjectID", "GetObjectID2" };
                foreach (string functionObject in functionObjects)
                {
                    try
                    {
                        ManagementBaseObject pack = WMI.InvokeMethodAndGetValue(classInstance, functionObject, "pack", null, 0);
                        if (pack != null)
                        {
                            uint[] ID = (uint[])pack.GetPropertyValue("ID");
                            string[] IDString = (string[])pack.GetPropertyValue("IDString");
                            byte Length = (byte)pack.GetPropertyValue("Length");

                            for (int i = 0; i < Length; ++i)
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

                AOD aod = cpu.info.aod;

                if (cpu.GetMemoryConfig().Type == MemType.DDR4)
                {

                    // Get APCB config from BIOS. Holds memory parameters.
                    BiosACPIFunction cmd = GetFunctionByIdString("Get APCB Config");
                    if (cmd == null)
                    {
                        // throw new Exception("Could not get memory controller config");
                        // Use AOD table as an alternative path for now
                        BMC.Table = cpu.info.aod.Table.RawAodTable;
                    }
                    else
                    {
                        byte[] apcbConfig = WMI.RunCommand(classInstance, cmd.ID);
                        // BiosACPIFunction cmd = new BiosACPIFunction("Get APCB Config", 0x00010001);
                        cmd = GetFunctionByIdString("Get memory voltages");
                        if (cmd != null)
                        {
                            byte[] voltages = WMI.RunCommand(classInstance, cmd.ID);

                            // MEM_VDDIO is ushort, offset 27
                            // MEM_VTT is ushort, offset 29
                            for (int i = 27; i <= 30; i++)
                            {
                                byte value = voltages[i];
                                if (value > 0)
                                    apcbConfig[i] = value;
                            }
                        }

                        BMC.Table = apcbConfig ?? new byte[] { };
                    }

                    float vdimm = Convert.ToSingle(Convert.ToDecimal(BMC.Config.MemVddio) / 1000);
                    if (vdimm > 0 && vdimm < 3)
                    {
                        textBoxMemVddio.Text = $"{vdimm:F4}V";
                    }
                    else if (AsusWmi != null && AsusWmi.Status == 1)
                    {
                        AsusSensorInfo sensor = AsusWmi.FindSensorByName("DRAM Voltage");
                        float temp = 0;
                        bool valid = sensor != null && float.TryParse(sensor.Value, out temp);

                        if (valid && temp > 0 && temp < 3)
                            textBoxMemVddio.Text = sensor.Value;
                        else
                            labelMemVddio.IsEnabled = false;
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

                    // When ProcODT is 0, then all other resistance values are 0
                    // Happens when one DIMM installed in A1 or A2 slot
                    if (BMC.Table == null || Utils.AllZero(BMC.Table) || BMC.Config.ProcODT < 1)
                        // throw new Exception("Failed to read AMD ACPI. Odt, Setup and Drive strength parameters will be empty.");
                        return;

                    labelProcODT.IsEnabled = true;
                    labelClkDrvStren.IsEnabled = true;
                    labelAddrCmdDrvStren.IsEnabled = true;
                    labelCsOdtDrvStren.IsEnabled = true;
                    labelCkeDrvStren.IsEnabled = true;
                    labelRttNom.IsEnabled = true;
                    labelRttWr.IsEnabled = true;
                    labelRttPark.IsEnabled = true;
                    labelAddrCmdSetup.IsEnabled = true;
                    labelCsOdtSetup.IsEnabled = true;
                    labelCkeSetup.IsEnabled = true;

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
                    if (cpu.info.aod == null || Utils.AllZero(cpu.info.aod.Table.RawAodTable))
                        return;

                    AodData Data = cpu.info.aod.Table.Data;

                    labelMemVdd.IsEnabled = true;
                    labelMemVddq.IsEnabled = true;
                    labelMemVpp.IsEnabled = true;
                    labelApuVddio.IsEnabled = true;

                    labelProcCaDs.IsEnabled = true;
                    labelProcDqDs.IsEnabled = true;
                    labelDramDqDs.IsEnabled = true;
                    labelRttWrD5.IsEnabled = true;
                    labelRttNomWr.IsEnabled = true;
                    labelRttNomRd.IsEnabled = true;
                    labelRttParkD5.IsEnabled = true;
                    labelRttParkDqs.IsEnabled = true;

                    textBoxMemVddio.Text = Data.MemVddio.ToString();
                    textBoxMemVddq.Text = Data.MemVddq.ToString();
                    textBoxMemVpp.Text = Data.MemVpp.ToString();
                    textBoxApuVddio.Text = Data.ApuVddio.ToString();

                    try
                    {
                        Cpu.CodeName codeName = cpu.info.codeName;
                        if (codeName == Cpu.CodeName.Phoenix || codeName == Cpu.CodeName.Phoenix2 || codeName == Cpu.CodeName.HawkPoint)
                        {
                            labelProcCaOdt.IsEnabled = true;
                            labelProcCkOdt.IsEnabled = true;
                            labelProcDqOdt.IsEnabled = true;
                            labelProcDqsOdt.IsEnabled = true;
                            textBoxProcCaOdt.Text = Data.ProcCaOdt.ToString();
                            textBoxProcCkOdt.Text = Data.ProcCkOdt.ToString();
                            textBoxProcDqOdt.Text = Data.ProcDqOdt.ToString();
                            textBoxProcDqsOdt.Text = Data.ProcDqsOdt.ToString();
                        }
                        else if (cpu.info.family == Cpu.Family.FAMILY_1AH && Data.ProcOdtPullUp != null)
                        {
                            labelProcODT.Visibility = Visibility.Collapsed;
                            textBoxProcODT.Visibility = Visibility.Collapsed;
                            procOdtDivider1.Visibility = Visibility.Collapsed;
                            procOdtDivider2.Visibility = Visibility.Collapsed;
                            labelProcOdtPullUp.Visibility = Visibility.Visible;
                            labelProcOdtPullUp.IsEnabled = true;
                            labelProcOdtPullDown.Visibility = Visibility.Visible;
                            labelProcOdtPullDown.IsEnabled = true;
                            textBoxProcOdtPullUp.Visibility = Visibility.Visible;
                            textBoxProcOdtPullDown.Visibility = Visibility.Visible;
                            textBoxProcOdtPullUp.Text = Data.ProcOdtPullUp.ToString();
                            textBoxProcOdtPullDown.Text = Data.ProcOdtPullDown.ToString();
                        }
                        else
                        {
                            labelProcODT.IsEnabled = true;
                            textBoxProcODT.Text = Data.ProcOdt.ToString();
                        }
                    }
                    catch { }

                    textBoxCadBusDrvStren.Text = Data.CadBusDrvStren.ToString();
                    textBoxDramDataDrvStren.Text = Data.DramDataDrvStren.ToString();
                    textBoxProcDataDrvStren.Text = Data.ProcDataDrvStren.ToString();

                    textBoxRttWrD5.Text = Data.RttWr.ToString();
                    textBoxRttNomWr.Text = Data.RttNomWr.ToString();
                    textBoxRttNomRd.Text = Data.RttNomRd.ToString();
                    textBoxRttParkD5.Text = Data.RttPark.ToString();
                    textBoxRttParkDqs.Text = Data.RttParkDqs.ToString();
                }
            }
            catch (Exception ex)
            {
                compatMode = true;

                MessageBox.Show(
                    ex.Message,
                    "Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                Console.WriteLine(ex.Message);
            }

            BMC.Dispose();
        }

        private void ReadTimings(uint offset = 0)
        {
            uint config = cpu.ReadDword(offset | 0x50100);

            MEMCFG.Type = (MemoryConfig.MemType)(MemType)Utils.GetBits(config, 0, 2);

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
            uint trfcTimings0 = cpu.ReadDword(offset | 0x50260);
            uint trfcTimings1 = cpu.ReadDword(offset | 0x50264);
            uint trfcTimings2 = cpu.ReadDword(offset | 0x50268);
            uint trfcTimings3 = cpu.ReadDword(offset | 0x5026C);
            uint timings22 = cpu.ReadDword(offset | 0x5028C);
            uint trfcRegValue = 0;

            if (cpu.GetMemoryConfig().Type == MemType.DDR4)
            {
                trfcRegValue = trfcTimings0 != trfcTimings1 ? (trfcTimings0 != 0x21060138 ? trfcTimings0 : trfcTimings1) : trfcTimings0;
            }
            else if (cpu.GetMemoryConfig().Type >= MemType.DDR5)
            {
                uint[] ddr5Regs = { trfcTimings0, trfcTimings1, trfcTimings2, trfcTimings3 };
                foreach (uint reg in ddr5Regs)
                {
                    if (reg != 0x00C00138)
                    {
                        trfcRegValue = reg;
                        break;
                    }
                }
            }

            float configured = MEMCFG.Frequency;
            float ratio = cpu.GetMemoryConfig().Type == MemType.DDR4 ? Utils.GetBits(umcBase, 0, 7) / 3.0f : Utils.GetBits(umcBase, 0, 16) / 100.0f;
            float freqFromRatio = ratio * 200;

            MEMCFG.Ratio = ratio;

            // Fallback to ratio when ConfiguredClockSpeed fails
            if (configured == 0.0f || freqFromRatio > configured)
            {
                MEMCFG.Frequency = freqFromRatio;
            }


            MEMCFG.BGS = bgs0 == 0x87654321 && bgs1 == 0x87654321 ? "Disabled" : "Enabled";
            MEMCFG.BGSAlt = Utils.GetBits(bgsa0, 4, 7) > 0 || Utils.GetBits(bgsa1, 4, 7) > 0
                ? "Enabled"
                : "Disabled";
            int GDM_BIT = cpu.GetMemoryConfig().Type == MemType.DDR4 ? 11 : 18;
            MEMCFG.GDM = Utils.GetBits(umcBase, GDM_BIT, 1) > 0 ? "Enabled" : "Disabled";
            int CMD2T_BIT = cpu.GetMemoryConfig().Type == MemType.DDR4 ? 10 : 17;
            MEMCFG.Cmd2T = Utils.GetBits(umcBase, CMD2T_BIT, 1) > 0 ? "2T" : "1T";

            MEMCFG.CL = Utils.GetBits(timings5, 0, 6);
            MEMCFG.RAS = Utils.GetBits(timings5, 8, 7);
            MEMCFG.RCDRD = Utils.GetBits(timings5, 16, 6);
            MEMCFG.RCDWR = Utils.GetBits(timings5, 24, 6);

            MEMCFG.RC = Utils.GetBits(timings6, 0, 8);
            MEMCFG.RP = Utils.GetBits(timings6, 16, 6);

            MEMCFG.RRDS = Utils.GetBits(timings7, 0, 5);
            MEMCFG.RRDL = Utils.GetBits(timings7, 8, 5);
            MEMCFG.RTP = Utils.GetBits(timings7, 24, 5);

            MEMCFG.FAW = Utils.GetBits(timings8, 0, 7);

            MEMCFG.CWL = Utils.GetBits(timings9, 0, 6);
            MEMCFG.WTRS = Utils.GetBits(timings9, 8, 5);
            MEMCFG.WTRL = Utils.GetBits(timings9, 16, 7);

            MEMCFG.WR = Utils.GetBits(timings10, 0, 7);

            MEMCFG.TRCPAGE = Utils.GetBits(timings11, 20, 12);

            MEMCFG.RDRDDD = Utils.GetBits(timings12, 0, 4);
            MEMCFG.RDRDSD = Utils.GetBits(timings12, 8, 4);
            MEMCFG.RDRDSC = Utils.GetBits(timings12, 16, 4);
            MEMCFG.RDRDSCL = Utils.GetBits(timings12, 24, 6);

            MEMCFG.WRWRDD = Utils.GetBits(timings13, 0, 4);
            MEMCFG.WRWRSD = Utils.GetBits(timings13, 8, 4);
            MEMCFG.WRWRSC = Utils.GetBits(timings13, 16, 4);
            MEMCFG.WRWRSCL = Utils.GetBits(timings13, 24, 6);

            MEMCFG.RDWR = Utils.GetBits(timings14, 8, 6);
            MEMCFG.WRRD = Utils.GetBits(timings14, 0, 4);

            MEMCFG.REFI = Utils.GetBits(timings15, 0, 16);

            MEMCFG.MODPDA = Utils.GetBits(timings16, 24, 6);
            MEMCFG.MRDPDA = Utils.GetBits(timings16, 16, 6);
            MEMCFG.MOD = Utils.GetBits(timings16, 8, 6);
            MEMCFG.MRD = Utils.GetBits(timings16, 0, 6);

            MEMCFG.STAG = Utils.GetBits(timings17, 16, 11);

            MEMCFG.XP = Utils.GetBits(timings18, 0, 6);
            MEMCFG.CKE = Utils.GetBits(timings18, 24, 5);

            MEMCFG.PHYWRL = Utils.GetBits(timings19, 8, 8);
            MEMCFG.PHYRDL = Utils.GetBits(timings19, 16, 8);
            MEMCFG.PHYWRD = Utils.GetBits(timings19, 24, 3);

            if (cpu.GetMemoryConfig().Type == MemType.DDR4)
            {
                MEMCFG.RFC = Utils.GetBits(trfcRegValue, 0, 11);
                MEMCFG.RFC2 = Utils.GetBits(trfcRegValue, 11, 11);
                MEMCFG.RFC4 = Utils.GetBits(trfcRegValue, 22, 11);
            }

            if (cpu.GetMemoryConfig().Type >= MemType.DDR5)
            {
                MEMCFG.RFC = Utils.GetBits(trfcRegValue, 0, 16);
                MEMCFG.RFC2 = Utils.GetBits(trfcRegValue, 16, 16);
                uint[] temp = {
                    Utils.GetBits(cpu.ReadDword(offset | 0x502c0), 0, 11),
                    Utils.GetBits(cpu.ReadDword(offset | 0x502c4), 0, 11),
                    Utils.GetBits(cpu.ReadDword(offset | 0x502c8), 0, 11),
                    Utils.GetBits(cpu.ReadDword(offset | 0x502cc), 0, 11),
                };
                foreach (uint value in temp)
                {
                    if (value != 0)
                    {
                        MEMCFG.RFCsb = value;
                        break;
                    }
                }
            }

            MEMCFG.PowerDown = Utils.GetBits(powerDown, 28, 1) == 1 ? "Enabled" : "Disabled";

            cpu.memoryConfig.ReadTimings(offset);
        }

        private bool WaitForDriverLoad()
        {
            Stopwatch timer = new Stopwatch();
            timer.Start();

            bool temp;
            // Refresh until driver is opened
            do
            {
                temp = cpu.io.IsInpOutDriverOpen();
            } while (!temp && timer.Elapsed.TotalMilliseconds < 10000);

            timer.Stop();

            return temp;
        }

        private void SetFrequencyString()
        {
            if (cpu.powerTable != null && cpu.powerTable.MCLK > 0)
            {
                MEMCFG.Frequency = cpu.powerTable.MCLK * 2;
            }
        }

        private bool WaitForPowerTable()
        {
            if (cpu.powerTable == null || cpu.powerTable.DramBaseAddress == 0)
            {
                HandleError("Could not initialize power table.\n\nClose the application and try again. If the issue persists, you might want to try a system restart.");
                return false;
            }

            if (WaitForDriverLoad())
            {
                Stopwatch timer = new Stopwatch();
                int timeout = 10000;

                cpu.powerTable.ConfiguredClockSpeed = MEMCFG.Frequency;
                cpu.powerTable.MemRatio = MEMCFG.Ratio;

                timer.Start();

                SMU.Status status;
                // Refresh each 2 seconds until table is transferred to DRAM or timeout
                do
                {
                    status = cpu.RefreshPowerTable();
                    if (status != SMU.Status.OK)
                        Thread.Sleep(2000);  // It's ok to block the current thread
                } while (status != SMU.Status.OK && timer.Elapsed.TotalMilliseconds < timeout);

                timer.Stop();

                if (status != SMU.Status.OK)
                {
                    HandleError("Could not get power table.\nSkipping.");
                    return false;
                }

                SetFrequencyString();

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
                        AsusSensorInfo sensor = AsusWmi.FindSensorByName("DRAM Voltage");
                        if (sensor != null)
                            Dispatcher.Invoke(DispatcherPriority.ApplicationIdle,
                                new Action(() =>
                                {
                                    textBoxMemVddio.Text = sensor.Value;
                                    labelMemVddio.IsEnabled = true;
                                }));
                    }

                    Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() =>
                    {
                        int selectedIndex = comboBoxPartNumber?.SelectedIndex ?? 0;
                        MemoryModule module = modules?.Count > 0 ? modules[selectedIndex] : null;
                        ReadTimings(module?.DctOffset ?? 0);
                        ReadMemoryConfig();
                        cpu.RefreshPowerTable();
                        ReadSVI();
                        SetFrequencyString();
                        // RefreshSensors();
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
            BitmapDecoder decoder = BitmapDecoder.Create(new Uri(iconSource),
                BitmapCreateOptions.DelayCreation,
                BitmapCacheOption.OnDemand);

            BitmapFrame result = decoder.Frames.SingleOrDefault(f => f.Width == width);
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
            ExitApplication();
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
            string AssemblyTitle = "ZT";

            if (settings.AdvancedMode)
                AssemblyTitle = ((AssemblyTitleAttribute)Attribute.GetCustomAttribute(
                    Assembly.GetExecutingAssembly(),
                    typeof(AssemblyTitleAttribute), false)).Title;

            string AssemblyVersion = ((AssemblyFileVersionAttribute)Attribute.GetCustomAttribute(
                Assembly.GetExecutingAssembly(),
                typeof(AssemblyFileVersionAttribute), false)).Version;

            Title = $"{AssemblyTitle} {AssemblyVersion.Substring(0, AssemblyVersion.LastIndexOf('.'))}";
#if BETA
            Title += @" beta";

            MessageBox.Show("This is a BETA version of the application. Some functions might be working incorrectly.\n\n" +
                "Please report if something is not working as expected.", "Beta version", MessageBoxButton.OK);
#else
#if DEBUG
            Title += $@"{AssemblyVersion.Substring(AssemblyVersion.LastIndexOf('.'))}";

            if (settings.AdvancedMode)
            {
                Title += @" (debug)";
            }
#endif
#endif
            if (compatMode && settings.AdvancedMode)
                Title += @" (compatibility)";
        }

        private static string GetCpuNameString(SystemInfo info)
        {
            try
            {
                var name = info.CpuName;
                if (name.Contains("Eng Sample"))
                {
                    return $"{name} | {info.CodeName} | 0x{info.CpuId:X6}";
                }
                return name;
            }
            catch
            {
                return "Error getting CPU name";
            }
        }

        private static bool IsRogMotherboard(SystemInfo info)
        {
            return info.MbVendor.ToLower().Contains("asus") && info.MbName.ToLower().StartsWith("rog");
        }

        private static string GetMotherboardLink(SystemInfo info)
        {
            if (!IsRogMotherboard(info))
                return null;

            string[] mbNameParts = info.MbName.ToLower().Split(' ');
            string series = $"{mbNameParts[0]}-{mbNameParts[1]}";
            string name = string.Join("-", mbNameParts);

            if (!(info.MbName.Contains("X870") || info.MbName.Contains("B850") || info.MbName.Contains("B840")))
            {
                name = $"{name}-model";
            }

            return $"https://rog.asus.com/motherboards/{series}/{name}";
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            if (settings.SaveWindowPosition)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;

                // Get the current screen bounds
                System.Windows.Forms.Screen screen = System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(this).Handle);
                System.Drawing.Rectangle screenBounds = screen.Bounds;

                // Check if the saved window position is outside the screen bounds
                if (settings.WindowLeft < screenBounds.Left || settings.WindowLeft + Width > screenBounds.Right ||
                    settings.WindowTop < screenBounds.Top || settings.WindowTop + Height > screenBounds.Bottom)
                {
                    // Reset the window position to a default value
                    Left = (screenBounds.Width - Width) / 2 + screenBounds.Left;
                    Top = (screenBounds.Height - Height) / 2 + screenBounds.Top;
                }
                else
                {
                    // Set the window position to the saved values
                    Left = settings.WindowLeft;
                    Top = settings.WindowTop;
                }
            }

            SetWindowTitle();
            if (cpu?.systemInfo != null)
            {
                labelCPU.Text = GetCpuNameString(cpu.systemInfo);
                //string mbLabel = $@"{cpu.systemInfo.MbName} | BIOS {cpu.systemInfo.BiosVersion} | SMU {cpu.systemInfo.GetSmuVersionString()}";

                //if (mbLabel.Length > 58)
                //{
                //    mbLabel = $@"{cpu.systemInfo.MbName} | BIOS {cpu.systemInfo.BiosVersion} ({cpu.systemInfo.GetSmuVersionString()})";
                //}
                if (String.IsNullOrEmpty(cpu.systemInfo.AgesaVersion))
                {
                    labelMB.Text = $@"{cpu.systemInfo.MbName} | BIOS {cpu.systemInfo.BiosVersion} ({cpu.systemInfo.GetSmuVersionString()})";
                    labelAgesaVersion.Visibility = Visibility.Collapsed;
                }
                else
                {
                    labelMB.Text = $@"{cpu.systemInfo.MbName} | BIOS {cpu.systemInfo.BiosVersion}";
                    labelAgesaVersion.Text = $"AGESA {cpu.systemInfo.AgesaVersion} (SMU version {cpu.systemInfo.GetSmuVersionString()})";
                }
            }
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
                Window parent = Application.Current.MainWindow;
                if (parent != null)
                {
                    DebugDialog debugWnd = new DebugDialog(BMC, AsusWmi)
                    {
                        Owner = parent,
                        Width = parent.Width,
                        Height = parent.Height
                    };
                    debugWnd.ShowDialog();
                }
            }
            else
            {
                MessageBoxModel messageBox = new MessageBoxModel
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
            if (WindowState == WindowState.Minimized && (siWnd == null || !siWnd.IsLoaded))
                StopAutoRefresh();
            else if (WindowState == WindowState.Normal)
                StartAutoRefresh();

            if (WindowState == WindowState.Minimized)
            {
                if (settings.MinimizeToTray)
                {
                    _notifyIcon.Visible = true;
                    ShowInTaskbar = false;
                }
            }
            else
            {
                _notifyIcon.Visible = false;
                ShowInTaskbar = true;
            }

            MinimizeFootprint();
        }

        private void AdonisWindow_SizeChanged(object sender, SizeChangedEventArgs e) => MinimizeFootprint();

        private void AdonisWindow_Activated(object sender, EventArgs e) => MinimizeFootprint();

        private void ExitToolStripMenuItem_Click(object sender, RoutedEventArgs e) => ExitApplication();

        private void AdonisWindow_Loaded(object sender, RoutedEventArgs e)
        {
            SplashWindow.Stop();

            Application.Current.MainWindow = this;

            IntPtr handle = new WindowInteropHelper(Application.Current.MainWindow).Handle;
            HwndSource source = HwndSource.FromHwnd(handle);

            source?.AddHook(WndProc);
#if !DEBUG
            if (!settings.NotifiedChangelog.Equals(AssemblyVersion))
            {
                Changelog changelogWindow = new Changelog()
                {
                    Owner = Application.Current.MainWindow
                };
                changelogWindow.ShowDialog();
                settings.NotifiedChangelog = AssemblyVersion;
                settings.Save();
            }
#endif
        }

        private void OptionsToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            OptionsDialog optionsWnd = new OptionsDialog(PowerCfgTimer)
            {
                Owner = Application.Current.MainWindow
            };
            optionsWnd.ShowDialog();
        }

        private void AboutToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            AboutDialog aboutWnd = new AboutDialog()
            {
                Owner = Application.Current.MainWindow
            };
            aboutWnd.ShowDialog();
        }

        private void ButtonScreenshot_Click(object sender, RoutedEventArgs e)
        {
            Screenshot screenshot = new Screenshot();
            System.Drawing.Bitmap bitmap = (settings.ScreenshotMode == AppSettings.ScreenshotType.Desktop)
                ? screenshot.CaptureDekstop()
                : screenshot.CaptureActiveWindow();

            using (SaveWindow saveWnd = new SaveWindow(bitmap))
            {
                saveWnd.Owner = Application.Current.MainWindow;
                saveWnd.ShowDialog();
                screenshot.Dispose();
            }
        }

        private void ComboBoxPartNumber_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (sender is ComboBox combo && combo.Items.Count > 0)
                ReadTimings(modules[combo.SelectedIndex].DctOffset);
        }

        private void SystemInfoToolstripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            double sysInfoWindowWidth = Width;
            double sysInfoWindowHeight = Height;
            double sysInfoWindowTop = 0;
            double sysInfoWindowLeft = 0;
            WindowStartupLocation location = WindowStartupLocation.CenterScreen;

            if (settings.SaveWindowPosition && settings.SysInfoWindowHeight != 0 && settings.SysInfoWindowWidth != 0)
            {
                location = WindowStartupLocation.Manual;
                sysInfoWindowLeft = settings.SysInfoWindowLeft;
                sysInfoWindowTop = settings.SysInfoWindowTop;
                sysInfoWindowHeight = settings.SysInfoWindowHeight;
                sysInfoWindowWidth = settings.SysInfoWindowWidth;
            }

            siWnd = new SystemInfoWindow(MEMCFG, BMC.Config, AsusWmi?.sensors)
            {
                Width = sysInfoWindowWidth,
                Height = sysInfoWindowHeight,
                WindowStartupLocation = location,
                Top = sysInfoWindowTop,
                Left = sysInfoWindowLeft
            };

            siWnd.Show();
        }

        private void AdonisWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            siWnd?.Close();

            if (settings.SaveWindowPosition)
            {
                settings.WindowLeft = Left;
                settings.WindowTop = Top;
                settings.Save();
            }

            ExitApplication();
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://www.paypal.com/donate/?hosted_button_id=NLSRLE9MVDPCW");
        }

        private void MenuItem_Click_1(object sender, RoutedEventArgs e)
        {
            Process.Start("https://revolut.me/ivanrusanov");
        }

        private void MenuItem_Click_2(object sender, RoutedEventArgs e)
        {
            Process.Start("https://discord.gg/8cfR3UZ");
        }

        private void MenuItem_Click_3(object sender, RoutedEventArgs e)
        {
            Process.Start("https://github.com/irusanov/ZenTimings");
        }

        private void ExportToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Config Config = new Config(MEMCFG, BMC.Config/*, cpu.powerTable*/);
            Console.WriteLine(Config.GetXML());
        }

        private void RogLogoButton_Click(object sender, RoutedEventArgs e)
        {
            var link = GetMotherboardLink(cpu.systemInfo);
            if (link != null && link.Length > 0)
                Process.Start(link);
        }
    }
}