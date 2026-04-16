//#define BETA

using AdonisUI.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
using ZenTimings.Controls;
using ZenTimings.Plugin;
using ZenTimings.ViewModels;
using ZenTimings.Windows;
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
    public partial class MainWindow: ThemedAdonisWindow
    {
        private readonly AsusWMI AsusWmi = new AsusWMI();
        private readonly List<BiosACPIFunction> biosFunctions = new List<BiosACPIFunction>();
        private readonly BiosMemController BMC;
        private readonly Cpu cpu;
        private readonly DispatcherTimer PowerCfgTimer = new DispatcherTimer();
        private readonly AppSettings settings = AppSettings.Instance;
        private readonly List<IPlugin> plugins = new List<IPlugin>();
        private SystemInfoWindow siWnd = null;
        internal readonly Forms.NotifyIcon _notifyIcon;
        private bool compatMode;
        private Control timingsPanel;
        private readonly MainViewModel mainViewModel;
        private float lastMclk = 0;
        //private Computer computer;

        private readonly string AssemblyProduct = ((AssemblyProductAttribute)Attribute.GetCustomAttribute(
            Assembly.GetExecutingAssembly(),
            typeof(AssemblyProductAttribute), false)).Product;

        private readonly string AssemblyVersion = ((AssemblyFileVersionAttribute)Attribute.GetCustomAttribute(
            Assembly.GetExecutingAssembly(),
            typeof(AssemblyFileVersionAttribute), false)).Version;


        public void CheckForDriver()
        {
            if (DriverHelper.IsPawnIoInstalled)
            {
                var currentVersion = DriverHelper.Version;
                var newVersion = DriverHelper.BundledVersion;
                var skippedVersion = !string.IsNullOrEmpty(AppSettings.Instance.DriverUpdateLastSkippedVersion)
                    ? new Version(AppSettings.Instance.DriverUpdateLastSkippedVersion)
                    : new Version(0, 0, 0, 0);

                if (skippedVersion < newVersion && currentVersion < newVersion)
                {
                    DriverUpdateWindow driverUpdateWindow = new DriverUpdateWindow(currentVersion, newVersion)
                    {
                        Owner = Application.Current.MainWindow
                    };

                    bool? result = driverUpdateWindow.ShowDialog();

                    if (driverUpdateWindow.IsSkipChecked)
                    {
                        AppSettings.Instance.DriverUpdateLastSkippedVersion = newVersion.ToString();
                        AppSettings.Instance.Save();
                    }

                    if (result == true)
                    {
                        SplashWindow.Stop();
                        DriverHelper.InstallPawnIO();
                        Restart(false);
                    }
                }
            }
            else
            {
                {
                    AdonisUI.Controls.MessageBoxResult result = AdonisUI.Controls.MessageBox.Show(
                        "PawnIO is not installed, do you want to install it?",
                        nameof(ZenTimings),
                        AdonisUI.Controls.MessageBoxButton.OKCancel,
                        AdonisUI.Controls.MessageBoxImage.Warning
                    );

                    if (result == AdonisUI.Controls.MessageBoxResult.OK)
                    {
                        SplashWindow.Stop();
                        DriverHelper.InstallPawnIO();
                        Restart(false);
                    }

                    if (result == AdonisUI.Controls.MessageBoxResult.Cancel)
                    {
                        Application.Current.Shutdown();
                    }
                }
            }
        }

        public MainWindow()
        {
            try
            {
                SplashWindow.Loading("PawnIO");
                CheckForDriver();

                SplashWindow.Loading("Core");
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

                if (!cpu.RyzenSmu.IsLoaded)
                {
                    HandleError("Ryzen SMU module is not loaded.\nMake sure that the PawnIO driver is installed correctly.", "Driver Error");
                    ExitApplication();
                }

                IconSource = GetIcon("pack://application:,,,/ZenTimings;component/Resources/ZenTimings2022.ico", 16);
                _notifyIcon = GetTrayIcon();

                InitializeComponent();
                //SetResourceReference(NativeBorderBrushProperty, "WindowBorderColor");
                SplashWindow.Loading("Memory modules");
                ReadMemoryModulesInfo();

                SplashWindow.Loading("Timings");

                var memoryType = cpu.GetMemoryConfig().Type;

                // Motherboard logo
                var motherboardLogoName = VendorUtils.GetMotherboardLogo(cpu.systemInfo);
                if (motherboardLogoName != null)
                {
                    motherboardLogoImage.SetResourceReference(Image.SourceProperty, motherboardLogoName);
                }

                mainViewModel = new MainViewModel(
                    ReadTimings(),
                    memoryType,
                    compatMode,
                    settings,
                    plugins,
                    motherboardLogoName,
                    GetAgesaVersion(),
                    cpu.GetMemoryConfig()?.SpdInfo?.Values.FirstOrDefault(d => d.IsValid)?.PmicData ?? null
                );

                DataContext = mainViewModel;

                if (cpu != null && settings.AdvancedMode)
                {

                    PowerCfgTimer.Interval = TimeSpan.FromMilliseconds(settings.AutoRefreshInterval);
                    PowerCfgTimer.Tick += PowerCfgTimer_Tick;

                    SplashWindow.Loading("Reading power table");
                    if (!WaitForPowerTable())
                    {
                        SplashWindow.Loading("Power table error!");
                    }

                    SplashWindow.Loading("Plugins");
                    if (memoryType == MemType.DDR4 || memoryType == MemType.LPDDR4)
                    {
                        SplashWindow.Loading("SVI2 Plugin");
                        plugins.Add(new SVI2Plugin(cpu));
                        //ReadSVI();

                        SplashWindow.Loading("Memory controller");
                        BMC = new BiosMemController();
                    }
                    //plugins.Add(new OHWMPlugin());
                    //plugins[1].Open();

                    if (!AsusWmi.Init())
                    {
                        AsusWmi.Dispose();
                        AsusWmi = null;
                    }
                }

                SplashWindow.Loading("Done");

                AddTimingsPanel(memoryType);

                if (settings.AdvancedMode)
                {
                    if (memoryType == MemType.DDR4 || memoryType == MemType.LPDDR4)
                    {
                        ReadSVI();
                        ReadDDR4MemoryConfig();
                    }
                    StartAutoRefresh();
                }
            }
            catch (Exception ex)
            {
                HandleError(ex.Message);
                ExitApplication();
            }
        }

        private void AddTimingsPanel(MemType memoryType)
        {
            // Add timings panel
            switch (memoryType)
            {
                case MemType.DDR4:
                case MemType.LPDDR4:
                    timingsPanel = new DDR4TimingsPanel();
                    break;

                case MemType.LPDDR5:
                    timingsPanel = new LegacyDDR5APUTimingsPanel();
                    break;

                case MemType.DDR5:
                    {
                        if (!cpu.info.apob.IsAvailable || settings.ImpedanceTableSrc == AppSettings.ImpedanceTableSource.AOD)
                        {
                            if (cpu.smu.SMU_TYPE == SMU.SmuType.TYPE_APU2)
                                timingsPanel = new LegacyDDR5APUTimingsPanel();
                            else
                                timingsPanel = new LegacyDDR5TimingsPanel();
                            break;
                        }

                        if (cpu.smu.SMU_TYPE == SMU.SmuType.TYPE_APU2)
                        {
                            timingsPanel = new DDR5APUTimingsPanel();
                        }
                        else if (cpu.info.family == Cpu.Family.FAMILY_1AH)
                        {
                            timingsPanel = new DDR5TimingsPanel1Ah();
                        }
                        else
                        {
                            timingsPanel = new DDR5TimingsPanel19h();
                        }
                        break;
                    }

                default:
                    timingsPanel = null;
                    break;
            }

            if (timingsPanel != null)
            {
                timingsPanelSlot.Children.Add(timingsPanel);
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

        private void Cleanup()
        {
            foreach (IPlugin plugin in plugins)
                plugin?.Close();

            _notifyIcon?.Dispose();
            AsusWmi?.Dispose();
            //cpu?.io?.Close(settings.AutoUninstallDriver);
            cpu?.Dispose();

            //Driver.Cleanup();
        }

        private void ExitApplication(bool save = true)
        {
            if (save) settings.Save();
            Cleanup();
            Application.Current?.Shutdown();
        }

        private BiosACPIFunction GetFunctionByIdString(string name)
        {
            return biosFunctions.Find(x => x.IDString == name);
        }

        private void ReadMemoryModulesInfo()
        {
            var modules = cpu.GetMemoryConfig()?.Modules;
            if (modules?.Count > 0)
            {
                foreach (MemoryModule module in modules)
                {
                    var moduleLogoName = VendorUtils.GetMemoryModuleLogo(module);
                    if (moduleLogoName != null)
                    {
                        var stackPanel = new StackPanel
                        {
                            Orientation = Orientation.Horizontal
                        };

                        var image = new Image
                        {
                            Height = 18,
                            Margin = new Thickness(5, 0, 5, 0)
                        };

                        image.SetResourceReference(Image.SourceProperty, moduleLogoName);

                        var moduleStrings = module.ToString().Split(':');

                        var textBlock = new TextBlock
                        {
                            Text = $"{moduleStrings[0]}: ",
                            VerticalAlignment = VerticalAlignment.Center
                        };

                        var textBlock2 = new TextBlock
                        {
                            Text = moduleStrings[1].Trim(),
                            VerticalAlignment = VerticalAlignment.Center
                        };

                        stackPanel.Children.Add(textBlock);
                        stackPanel.Children.Add(image);
                        stackPanel.Children.Add(textBlock2);
                        comboBoxPartNumber.Items.Add(stackPanel);
                    }
                    else
                    {
                        comboBoxPartNumber.Items.Add(new ComboBoxItem
                        {
                            Content = module.ToString(),
                            Tag = module.PartNumber
                        });
                    }
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
            if ((cpu.memoryConfig.Type == MemType.DDR4 || cpu.memoryConfig.Type == MemType.LPDDR4) && plugins.Count > 0 && plugins[0].Update())
            {
                (timingsPanel as DDR4TimingsPanel).textBoxVSOC_SVI2.Text = $"{plugins[0].Sensors[0].Value:F4}V";
            }
        }

        // TODO: Handle in DLL or replace with read from memory
        private void ReadDDR4MemoryConfig()
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
                var dvaluesPack = WMI.InvokeMethodAndGetValue(classInstance, "Getdvalues", "pack", "ID", 0x20035);
                if (dvaluesPack != null)
                {
                    uint[] DValuesBuffer = (uint[])dvaluesPack.GetPropertyValue("DValuesBuffer");
                    for (var i = 0; i < DValuesBuffer.Length; i++)
                    {
                        Debug.WriteLine("{0}", DValuesBuffer[i]);
                    }
                }*/

                // Get function names with their IDs
                var wmiFunctionsDict = AOD.GetWmiFunctions();
                if (wmiFunctionsDict != null)
                {
                    foreach (var kvp in wmiFunctionsDict)
                    {
                        biosFunctions.Add(new BiosACPIFunction(kvp.Key, kvp.Value));
                    }
                }

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
                    (timingsPanel as DDR4TimingsPanel).textBoxMemVddio.Text = $"{vdimm:F4}V";
                }
                else if (AsusWmi != null && AsusWmi.Status == 1)
                {
                    AsusSensorInfo sensor = AsusWmi.FindSensorByName("DRAM Voltage");
                    float temp = 0;
                    bool valid = sensor != null && float.TryParse(sensor.Value, out temp);

                    if (valid && temp > 0 && temp < 3)
                        (timingsPanel as DDR4TimingsPanel).textBoxMemVddio.Text = sensor.Value;
                    else
                        (timingsPanel as DDR4TimingsPanel).labelMemVddio.IsEnabled = false;
                }
                else
                {
                    (timingsPanel as DDR4TimingsPanel).labelMemVddio.IsEnabled = false;
                }

                float vtt = Convert.ToSingle(Convert.ToDecimal(BMC.Config.MemVtt) / 1000);
                if (vtt > 0)
                    (timingsPanel as DDR4TimingsPanel).textBoxMemVtt.Text = $"{vtt:F4}V";
                else
                    (timingsPanel as DDR4TimingsPanel).labelMemVtt.IsEnabled = false;

                // When ProcODT is 0, then all other resistance values are 0
                // Happens when one DIMM installed in A1 or A2 slot
                if (BMC.Table == null || Utils.AllZero(BMC.Table) || BMC.Config.ProcODT < 1)
                    // throw new Exception("Failed to read AMD ACPI. Odt, Setup and Drive strength parameters will be empty.");
                    return;

                (timingsPanel as DDR4TimingsPanel).labelProcODT.IsEnabled = true;
                (timingsPanel as DDR4TimingsPanel).labelClkDrvStren.IsEnabled = true;
                (timingsPanel as DDR4TimingsPanel).labelAddrCmdDrvStren.IsEnabled = true;
                (timingsPanel as DDR4TimingsPanel).labelCsOdtDrvStren.IsEnabled = true;
                (timingsPanel as DDR4TimingsPanel).labelCkeDrvStren.IsEnabled = true;
                (timingsPanel as DDR4TimingsPanel).labelRttNom.IsEnabled = true;
                (timingsPanel as DDR4TimingsPanel).labelRttWr.IsEnabled = true;
                (timingsPanel as DDR4TimingsPanel).labelRttPark.IsEnabled = true;
                (timingsPanel as DDR4TimingsPanel).labelAddrCmdSetup.IsEnabled = true;
                (timingsPanel as DDR4TimingsPanel).labelCsOdtSetup.IsEnabled = true;
                (timingsPanel as DDR4TimingsPanel).labelCkeSetup.IsEnabled = true;

                (timingsPanel as DDR4TimingsPanel).textBoxProcODT.Text = BMC.GetProcODTString(BMC.Config.ProcODT);

                (timingsPanel as DDR4TimingsPanel).textBoxClkDrvStren.Text = BMC.GetDrvStrenString(BMC.Config.ClkDrvStren);
                (timingsPanel as DDR4TimingsPanel).textBoxAddrCmdDrvStren.Text = BMC.GetDrvStrenString(BMC.Config.AddrCmdDrvStren);
                (timingsPanel as DDR4TimingsPanel).textBoxCsOdtCmdDrvStren.Text = BMC.GetDrvStrenString(BMC.Config.CsOdtCmdDrvStren);
                (timingsPanel as DDR4TimingsPanel).textBoxCkeDrvStren.Text = BMC.GetDrvStrenString(BMC.Config.CkeDrvStren);

                (timingsPanel as DDR4TimingsPanel).textBoxRttNom.Text = BMC.GetRttString(BMC.Config.RttNom);
                (timingsPanel as DDR4TimingsPanel).textBoxRttWr.Text = BMC.GetRttWrString(BMC.Config.RttWr);
                (timingsPanel as DDR4TimingsPanel).textBoxRttPark.Text = BMC.GetRttString(BMC.Config.RttPark);

                (timingsPanel as DDR4TimingsPanel).textBoxAddrCmdSetup.Text = $"{BMC.Config.AddrCmdSetup}";
                (timingsPanel as DDR4TimingsPanel).textBoxCsOdtSetup.Text = $"{BMC.Config.CsOdtSetup}";
                (timingsPanel as DDR4TimingsPanel).textBoxCkeSetup.Text = $"{BMC.Config.CkeSetup}";
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

            BMC?.Dispose();
        }

        //TODO: Replace with a call to DLL
        private BaseDramTimings ReadTimings(uint offset = 0)
        {
            cpu.memoryConfig.ReadTimings(offset);
            var timings = cpu.memoryConfig.Timings;
            BaseDramTimings result = null;

            if (timings.Count == 0)
                return result;

            var index = timings.FindIndex(m => m.Key.Equals(offset));
            return timings[index < 0 ? 0 : index].Value;

            //float configured = mainViewModel?.MemoryFrequency ?? 0;
            //float ratio = result.Ratio;
            //float freqFromRatio = ratio * 200;

            //// Fallback to ratio when ConfiguredClockSpeed fails
            //if ((configured == 0.0f || freqFromRatio > configured) && mainViewModel != null)
            //{
            //    mainViewModel.MemoryFrequency = freqFromRatio;
            //}

            //mainViewModel.MemoryFrequency = result.Frequency;

            //return result;
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
                int timeout = 100000;

                // TODO: Move to Core DLL
                var memoryConfig = cpu.memoryConfig.Timings.FirstOrDefault().Value;
                if (memoryConfig != null)
                {
                    cpu.powerTable.ConfiguredClockSpeed = memoryConfig.Frequency;
                    cpu.powerTable.MemRatio = memoryConfig.Ratio;
                }

                timer.Start();

                SMU.Status status;
                // Refresh each 200ms seconds until table is transferred to DRAM or timeout
                do
                {
                    status = cpu.RefreshPowerTable();
                    if (status != SMU.Status.OK)
                        Thread.Sleep(200);  // It's ok to block the current thread
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
                        AsusSensorInfo sensor = AsusWmi.FindSensorByName("DRAM Voltage");
                        if (sensor != null)
                            Dispatcher.Invoke(DispatcherPriority.ApplicationIdle,
                                new Action(() =>
                                {
                                    (timingsPanel as DDR4TimingsPanel).textBoxMemVddio.Text = sensor.Value;
                                    (timingsPanel as DDR4TimingsPanel).labelMemVddio.IsEnabled = true;
                                }));
                    }

                    //ReadDDR4MemoryConfig();
                    cpu.RefreshPowerTable();
                    var voltagesUpdated = false;
                    if (cpu.memoryConfig?.SpdInfo?.Values != null)
                    {
                        voltagesUpdated = cpu.memoryConfig.RefreshTelemetry(settings.AutoRefreshInterval);
                    }

                    Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() =>
                    {
                        var newMclk = cpu.powerTable.MCLK;

                        if (newMclk != lastMclk)
                        {
                            var modules = cpu.memoryConfig.Modules;
                            int selectedIndex = comboBoxPartNumber?.SelectedIndex ?? 0;
                            MemoryModule module = modules?.Count > 0 ? modules[selectedIndex] : null;
                            mainViewModel.Timings = ReadTimings(module?.DctOffset ?? 0);
                            //Dictionary<byte, Ddr5SpdInfo> results = Ddr5SpdDecoder.ReadAndDecodeAll(CpuSingleton.Instance.SmbusPiix4);
                        }

                        if (voltagesUpdated)
                            mainViewModel.PmicData = cpu.memoryConfig.SpdInfo.Values.ElementAtOrDefault(comboBoxPartNumber?.SelectedIndex ?? 0)?.PmicData ?? null;

                        lastMclk = newMclk;

                        ReadSVI();
                        // SetFrequencyString();
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

        private void Restart(bool save = true)
        {
            if (save)
                settings.Save();

            var location = Application.ResourceAssembly.Location;
            var startInfo = new ProcessStartInfo(location)
            {
                UseShellExecute = true,
                Verb = "runas"
            };

            Cleanup();
            Process.Start(startInfo);
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
            string AssemblyTitle = "ZT";

            if (settings.AdvancedMode)
                AssemblyTitle = ((AssemblyTitleAttribute)Attribute.GetCustomAttribute(
                    Assembly.GetExecutingAssembly(),
                    typeof(AssemblyTitleAttribute), false)).Title;

            string AssemblyVersion = ((AssemblyFileVersionAttribute)Attribute.GetCustomAttribute(
                Assembly.GetExecutingAssembly(),
                typeof(AssemblyFileVersionAttribute), false)).Version;

            Dispatcher.Invoke(() =>
            {
                Title = $"{AssemblyTitle} {AssemblyVersion.Substring(0, AssemblyVersion.LastIndexOf('.'))}";
#if DEBUG && !BETA
                if (settings.AdvancedMode)
                    Title += $@"{AssemblyVersion.Substring(AssemblyVersion.LastIndexOf('.'))} (debug)";
#endif

#if BETA
                Title += $@"{AssemblyVersion.Substring(AssemblyVersion.LastIndexOf('.'))} - beta";
#endif

                if (compatMode && settings.AdvancedMode)
                    Title += @" (compatibility)";
            });
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            //if (settings.SaveWindowPosition)
            //{
            //    WindowStartupLocation = WindowStartupLocation.Manual;

            //    // Get the current screen bounds
            //    System.Windows.Forms.Screen screen = System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(this).Handle);
            //    System.Drawing.Rectangle screenBounds = screen.Bounds;

            //    // Check if the saved window position is outside the screen bounds
            //    if (settings.WindowLeft < screenBounds.Left || settings.WindowLeft + Width > screenBounds.Right ||
            //        settings.WindowTop < screenBounds.Top || settings.WindowTop + Height > screenBounds.Bottom)
            //    {
            //        // Reset the window position to a default value
            //        Left = (screenBounds.Width - Width) / 2 + screenBounds.Left;
            //        Top = (screenBounds.Height - Height) / 2 + screenBounds.Top;
            //    }
            //    else
            //    {
            //        // Set the window position to the saved values
            //        Left = settings.WindowLeft;
            //        Top = settings.WindowTop;
            //    }
            //}            
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
                    debugWnd.Show();
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
            this.Topmost = true;

            RestoreWindowPosition();
            SetWindowTitle();
            //ShowWindow();

            SplashWindow.Stop();

            Application.Current.MainWindow = this;

            this.Topmost = false;

            IntPtr handle = new WindowInteropHelper(Application.Current.MainWindow).Handle;
            HwndSource source = HwndSource.FromHwnd(handle);

            source?.AddHook(WndProc);
            //#if !DEBUG
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
            //#endif
            //#if BETA
            //            MessageBox.Show("This is a BETA version of the application. Some functions might be working incorrectly.\n\n" +
            //                    "Please report if something is not working as expected.", "Beta version", MessageBoxButton.OK);
            //#endif
            MinimizeFootprint();

            //new Thread(() =>
            //{
            //    mainViewModel.AgesaVersion = GetAgesaVersion();
            //}).Start();
        }

        private void OptionsToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            OptionsDialog optionsWnd = new OptionsDialog(PowerCfgTimer)
            {
                Owner = Application.Current.MainWindow
            };
            optionsWnd.Show();
        }

        private void AboutToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            AboutDialog aboutWnd = new AboutDialog()
            {
                Owner = Application.Current.MainWindow
            };
            aboutWnd.Show();
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
            {
                var dctOffset = cpu.memoryConfig.Modules[combo.SelectedIndex].DctOffset;
                mainViewModel.Timings = ReadTimings(dctOffset);
                //mainViewModel.SelectedDctOffset = dctOffset;
            }
        }

        private void SystemInfoToolstripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            double sysInfoWindowWidth = Width;
            double sysInfoWindowHeight = Height;
            double sysInfoWindowTop = 0;
            double sysInfoWindowLeft = 0;
            WindowStartupLocation location = WindowStartupLocation.CenterScreen;

            if (settings.SaveWindowPosition
                && settings?.SysInfoWindowHeight != 0
                && settings?.SysInfoWindowWidth != 0
                && settings?.SysInfoWindowLeft != -1
                && settings?.SysInfoWindowTop != -1)
            {
                location = WindowStartupLocation.Manual;
                sysInfoWindowLeft = settings.SysInfoWindowLeft;
                sysInfoWindowTop = settings.SysInfoWindowTop;
                sysInfoWindowHeight = settings.SysInfoWindowHeight;
                sysInfoWindowWidth = settings.SysInfoWindowWidth;
            }

            siWnd = new SystemInfoWindow(cpu.memoryConfig, BMC?.Config, AsusWmi?.sensors)
            {
                Width = sysInfoWindowWidth,
                Height = sysInfoWindowHeight,
                WindowStartupLocation = location,
                Top = sysInfoWindowTop,
                Left = sysInfoWindowLeft
            };

            siWnd.Show();
        }

        private void TelemetryMonitorToolstripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                double telemetryWindowWidth = 650;
                double telemetryWindowHeight = 550;
                double telemetryWindowTop = 0;
                double telemetryWindowLeft = 0;
                WindowStartupLocation location = WindowStartupLocation.CenterOwner;

                if (settings.SaveWindowPosition
                    && settings?.TelemetryWindowHeight != 0
                    && settings?.TelemetryWindowWidth != 0
                    && settings?.TelemetryWindowLeft != -1
                    && settings?.TelemetryWindowTop != -1)
                {
                    location = WindowStartupLocation.Manual;
                    telemetryWindowLeft = settings.TelemetryWindowLeft;
                    telemetryWindowTop = settings.TelemetryWindowTop;
                    telemetryWindowHeight = settings.TelemetryWindowHeight;
                    telemetryWindowWidth = settings.TelemetryWindowWidth;
                }

                var telemetryWindow = new Windows.TelemetryWindow()
                {
                    Owner = this,
                    Width = telemetryWindowWidth,
                    Height = telemetryWindowHeight,
                    WindowStartupLocation = location,
                    Top = telemetryWindowTop,
                    Left = telemetryWindowLeft
                };

                telemetryWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening Telemetry Monitor:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
        private void MenuItem_Click_4(object sender, RoutedEventArgs e)
        {
            Process.Start("https://docs.google.com/spreadsheets/d/12zg6yT_H7H-W1voyw1ZoIrj0GSE7WI4Ug-uLlv-Asa8/edit?gid=937453961#gid=937453961");
        }

        private void MenuItem_Click_6(object sender, RoutedEventArgs e)
        {
            Process.Start("https://drive.google.com/drive/folders/1HAJO9_jxvQrIkLb4Ws9ZfKHcHFQ_yOqp?usp=sharing");
        }

        private void ExportToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            //Config Config = new Config(cpu.memoryConfig, BMC.Config/*, cpu.powerTable*/);
            //Console.WriteLine(Config.GetXML());
        }

        private void MotherboardLinkButton_Click(object sender, RoutedEventArgs e)
        {
            var link = VendorUtils.GetMotherboardLink(cpu.systemInfo);
            if (link != null && link.Length > 0)
                Process.Start(link);
        }

        private string GetAgesaVersion()
        {
            if (cpu?.systemInfo == null)
                return "";

            if (!string.IsNullOrEmpty(cpu?.systemInfo.AgesaVersion))
            {
                return cpu.systemInfo.AgesaVersion;
            }

            // TODO: Move to core DLL
            string version = AgesaHelper.FindAgesaVersionInMemory();

            if (!string.IsNullOrEmpty(version))
            {
                cpu.systemInfo.AgesaVersion = version;
            }

            return version;
        }

        private void RestoreWindowPosition()
        {
            if (settings.SaveWindowPosition)
            {
                if (settings?.WindowLeft == -1 || settings?.WindowTop == -1)
                {
                    return;
                }

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
        }

        private void ExportAsHtmlMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Generate HTML content
                string htmlContent = mainViewModel.GetHTML();

                // Open SaveFileDialog to save the HTML file
                Forms.SaveFileDialog saveFileDialog = new Forms.SaveFileDialog
                {
                    Filter = "HTML files (*.html)|*.html|All files (*.*)|*.*",
                    DefaultExt = "html",
                    FileName = "ZenTimings-report.html",
                    RestoreDirectory = true
                };

                if (saveFileDialog.ShowDialog() == Forms.DialogResult.OK)
                {
                    // Write the HTML content to the selected file
                    File.WriteAllText(saveFileDialog.FileName, htmlContent);
                    MessageBox.Show("HTML file exported successfully!", "Export as HTML", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while exporting: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MenuItem_Click_5(object sender, RoutedEventArgs e)
        {
            try
            {
                var exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
                var changelogPath = Path.Combine(exeDir, "Changelog.txt");

                if (File.Exists(changelogPath))
                {
                    Process.Start(changelogPath);
                }
                else
                {
                    MessageBox.Show($"Changelog file not found: {changelogPath}", "File not found", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open changelog: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        //protected override void OnSourceInitialized(EventArgs e)
        //{
        //    base.OnSourceInitialized(e);
        //    ApplyNativeBorderBrush(NativeBorderBrush);
        //}

        //private static void OnNativeBorderBrushChanged(
        //    DependencyObject d,
        //    DependencyPropertyChangedEventArgs e)
        //{
        //    var window = (MainWindow)d;
        //    window.ApplyNativeBorderBrush(e.NewValue as Brush);
        //}

        //private void ApplyNativeBorderBrush(Brush brush)
        //{
        //    if (brush is SolidColorBrush scb)
        //    {
        //        uint colorRef = WindowUtils.ToColorRef(
        //            scb.Color.R,
        //            scb.Color.G,
        //            scb.Color.B);

        //        WindowUtils.SetBorderColor(this, colorRef);
        //    }
        //}
    }
}