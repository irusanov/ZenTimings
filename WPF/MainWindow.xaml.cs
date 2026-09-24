using AdonisUI.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ZenStates.Core;
using ZenStates.Core.Hardware;
using ZenStates.Core.Hardware.Aod;
using ZenStates.Core.Hardware.DRAM;
using ZenStates.Core.Hardware.DRAM.DDR5.Pmic;
using ZenStates.Core.Hardware.DRAM.DDR5.Spd;
using ZenStates.Core.Hardware.Mock;
using ZenStates.Core.OHWM;
using ZenTimings.Common;
using ZenTimings.Controls;
using ZenTimings.Export;
using ZenTimings.Helpers;
using ZenTimings.Plugin;
using ZenTimings.Settings;
using ZenTimings.Utils;
using ZenTimings.ViewModels;
using ZenTimings.Windows;
using static ZenTimings.Helpers.DriverCleaner;
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
    public partial class MainWindow : ThemedAdonisWindow
    {
        private readonly AsusWMI AsusWmi = new AsusWMI();
        private readonly List<BiosACPIFunction> biosFunctions = new List<BiosACPIFunction>();
        private readonly BiosMemController BMC;
        private readonly Cpu cpu;
        private readonly DispatcherTimer PowerCfgTimer = new DispatcherTimer();
        private readonly AppSettings settings = AppSettings.Instance;
        private readonly WheaErrorCounter wheaErrorCounter = new WheaErrorCounter();
        private CpuTemperatureSensors cpuTemperatureSensors = null;
        private readonly List<IPlugin> plugins = new List<IPlugin>();
        private SystemInfoWindow siWnd = null;
        private AdvancedTimingsWindow advancedTimingsWnd = null;
        private SensorsWindow sensorsWindw = null;
        private OptionsDialog optionsWnd = null;
        private ExportDialog exportWnd = null;
        private AboutDialog aboutWnd = null;
        internal readonly Forms.NotifyIcon _notifyIcon;
        private bool compatMode;
        private Control timingsPanel;
        private readonly MainViewModel mainViewModel;
        private float lastMclk = 0;
        private readonly bool isMockWindow = false;
        private readonly MockSystemData mockData;

        // TODO: Refactor DDR4 to use view model only
        private static readonly string[] Ddr4DramSensorNames =
        {
            "DRAM Voltage",
            "CPU VDDIO",
            "VDIMM",
            "VDDIO",
            "CPU VDDIO Memory"
        };

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
                        // Restart even if the update failed: the previous driver may still be usable,
                        // and if it was removed the new instance offers to install it again.
                        DriverHelper.InstallPawnIO();
                        Restart(false);
                        TerminateStartup();
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
                        if (!DriverHelper.InstallPawnIO())
                        {
                            // InstallPawnIO has already shown the error; restarting would only ask again
                            Application.Current.Shutdown();
                            TerminateStartup();
                        }
                        Restart(false);
                        TerminateStartup();
                    }
                    else
                    {
                        // Cancel, or the dialog closed without an answer: there is no driver to run with
                        Application.Current.Shutdown();
                        TerminateStartup();
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
                    throw new ApplicationException("CPU family is not supported.");
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
                    TerminateStartup();
                }

                // TODO: Add crash-logger and dump any info available to a file for debugging.
                var memoryConfig = cpu.GetMemoryConfig() ?? throw new ApplicationException("Could not read the memory controller configuration.");
                var memoryType = memoryConfig.Type;

                IconSource = GetIcon("pack://application:,,,/ZenTimings;component/Resources/ZenTimings2022.ico", 16);
                _notifyIcon = GetTrayIcon();

                InitializeComponent();
                //SetResourceReference(NativeBorderBrushProperty, "WindowBorderColor");

                SplashWindow.Loading("Timings");
                var timings = ReadTimings();

                SplashWindow.Loading("Memory modules");
                ReadMemoryModulesInfo(memoryConfig?.Modules);

                SplashWindow.Loading("Sensors");
                cpu.systemInfo?.UpdateSensors();

                // Motherboard logo
                SplashWindow.Loading("Resources");
                var motherboardLogoName = VendorUtils.GetMotherboardLogo(cpu.systemInfo);
                if (motherboardLogoName != null)
                {
                    motherboardLogoImage.SetResourceReference(Image.SourceProperty, motherboardLogoName);
                }

                if (settings.AdvancedMode)
                {
                    PowerCfgTimer.Interval = TimeSpan.FromMilliseconds(settings.AutoRefreshInterval);
                    PowerCfgTimer.Tick += PowerCfgTimer_Tick;

                    SplashWindow.Loading("Reading power table");
                    if (!WaitForPowerTable())
                    {
                        SplashWindow.Loading("Power table error!");
                    }
                    // I/O driver currently used for APOB, AOD and Agesa version
                    SplashWindow.Loading("IO Driver");
                    if (!WaitForInpoutDriverLoad())
                    {
                        HandleError("I/O driver is not responding or not loaded.");
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

                mainViewModel = new MainViewModel(
                    timings,
                    memoryType,
                    compatMode,
                    settings,
                    plugins,
                    motherboardLogoName,
                    GetAgesaVersion(),
                    ModulePmicData(0)
                );

                DataContext = mainViewModel;

                SplashWindow.Loading("Done");

                AddTimingsPanel(memoryType);

                // This blocks needs to be after the timings panel is added, because DDR4 still targets the actual elements for some of the timings
                // TODO: Check if works now with the new MVVM approach
                if (settings.AdvancedMode)
                {
                    if (memoryType == MemType.DDR4 || memoryType == MemType.LPDDR4)
                    {
                        ReadDDR4MemoryConfig();
                        ReadSVI();
                    }
                    StartAutoRefresh();
                }
                SetWindowTitle();
                UpdateLiveSnapshotIndicator();
                RestoreWindowPosition();
            }
            catch (Exception ex)
            {
                HandleError(ex.Message);
                ExitApplication();
                TerminateStartup();
            }
        }

        private static void TerminateStartup()
        {
            Environment.Exit(0);
        }

        private MainWindow(MainViewModel viewModel, MockSystemData mockData)
        {
            cpu = CpuSingleton.Instance;
            this.isMockWindow = true;
            this.mockData = mockData;
            mainViewModel = viewModel;

            IconSource = GetIcon("pack://application:,,,/ZenTimings;component/Resources/ZenTimings2022.ico", 16);
            InitializeComponent();

            DataContext = viewModel;
            AddTimingsPanel(viewModel.MemoryType, mockData.CpuInfo.family, mockData.CpuInfo.smuType, mockData.Apob != null && mockData.Apob.IsValid);

            if (timingsPanel is DDR4TimingsPanel ddr4Panel)
                ApplyDdr4Vsoc(ddr4Panel, false);

            // DDR4 takes its ODT/RTT/drive strength fields from the BIOS memory controller config; a
            // report carries it as a byte dump. Too short a dump would read past the Resistances layout.
            if ((viewModel.MemoryType == MemType.DDR4 || viewModel.MemoryType == MemType.LPDDR4) &&
                mockData.BiosMemControllerTable != null &&
                mockData.BiosMemControllerTable.Length >= Marshal.SizeOf(typeof(BiosMemController.Resistances)))
            {
                BMC = new BiosMemController { Table = mockData.BiosMemControllerTable };

                try
                {
                    ApplyDdr4MemoryConfig(timingsPanel as DDR4TimingsPanel);
                    ddr4MemoryConfigApplied = true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Debug report: could not apply the BIOS memory controller config: {ex.Message}");
                }
            }

            // The window already shows the report's CPU and board, so the title only marks it as a report and names
            // the build that wrote it. The commit hash is left out: anything longer widens this SizeToContent
            // window in simple mode, as its title bar does not trim.
            string version = mockData.ReportVersion?.Split('+')[0];
            Title = version != null ? $"Debug Report v{version}" : "Debug Report";

            // A debug report is a read-only snapshot: the menu and the screenshot act on the live machine. Module
            // selection needs every channel's timings, which only reports carrying the register dump provide.
            MainMenu.IsEnabled = false;
        }

        private void AddTimingsPanel(MemType memoryType)
        {
            var apob = cpu.info.apob;
            AddTimingsPanel(memoryType, cpu.info.family, cpu.smu.SMU_TYPE, apob != null && apob.IsValid);
        }

        // The AOD-driven panels show the report's AOD fields in a mock window, never the live machine's.
        private LegacyDDR5APUTimingsPanel CreateLegacyApuPanel()
        {
            return mockData != null
                ? new LegacyDDR5APUTimingsPanel(mockData.AodData)
                : new LegacyDDR5APUTimingsPanel();
        }

        // Creates the timings panel the main window shows. The All DIMMs window creates its per-channel panels
        // through the same factory, so they match the main one (AOD source, report data in a mock window).
        private Func<Control> timingsPanelFactory;

        private void AddTimingsPanel(MemType memoryType, Cpu.Family family, SMU.SmuType smuType, bool apobValid)
        {
            // Decided once, so panels created later match this one even if the setting changes meanwhile.
            bool useAodPanel = !apobValid || settings.ImpedanceTableSrc == AppSettings.ImpedanceTableSource.AOD;
            timingsPanelFactory = () => CreateTimingsPanel(memoryType, family, smuType, useAodPanel);

            // Add timings panel
            timingsPanel = timingsPanelFactory();

            if (timingsPanel != null)
            {
                timingsPanelSlot.Children.Add(timingsPanel);
            }
        }

        private Control CreateTimingsPanel(MemType memoryType, Cpu.Family family, SMU.SmuType smuType, bool useAodPanel)
        {
            switch (memoryType)
            {
                case MemType.DDR4:
                case MemType.LPDDR4:
                    return new DDR4TimingsPanel();

                case MemType.LPDDR5:
                    return CreateLegacyApuPanel();

                case MemType.DDR5:
                    {
                        if (useAodPanel)
                        {
                            if (smuType == SMU.SmuType.TYPE_APU2)
                                return CreateLegacyApuPanel();

                            return mockData != null
                                ? new LegacyDDR5TimingsPanel(mockData.AodData, family)
                                : new LegacyDDR5TimingsPanel();
                        }

                        if (smuType == SMU.SmuType.TYPE_APU2)
                            return new DDR5APUTimingsPanel();

                        if (family == Cpu.Family.FAMILY_1AH)
                            return new DDR5TimingsPanel1Ah();

                        return new DDR5TimingsPanel19h();
                    }

                default:
                    return null;
            }
        }

        // A panel for the All DIMMs window: created like the main one, with the DDR4 rows the code-behind fills
        // (VSOC, VDIMM, VTT, ODT, RTT, drive strengths, setups) filled the same way.
        private FrameworkElement CreateChannelTimingsPanel()
        {
            Control panel = timingsPanelFactory?.Invoke();

            if (panel is DDR4TimingsPanel ddr4Panel)
            {
                ApplyDdr4Vsoc(ddr4Panel, false);

                if (ddr4MemoryConfigApplied && BMC != null)
                {
                    try
                    {
                        ApplyDdr4MemoryConfig(ddr4Panel);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"All DIMMs: could not apply the BIOS memory controller config: {ex.Message}");
                    }
                }
            }

            return panel;
        }

        private Forms.NotifyIcon GetTrayIcon()
        {
            Forms.NotifyIcon notifyIcon = new Forms.NotifyIcon
            {
                Icon = Properties.Resources.ZenTimings2022
            };

            notifyIcon.MouseClick += NotifyIcon_MouseClick;
            notifyIcon.ContextMenuStrip = new Forms.ContextMenuStrip();
            notifyIcon.ContextMenuStrip.Items.Add($"{AssemblyProduct} v{AssemblyVersion}", null, OnAppContextMenuItemClick);
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

        // Read by the refresh task on a background thread.
        private volatile bool cleanedUp;

        // Set while the refresh task is reading the hardware (not while it updates the UI).
        private int refreshReadingHardware;
        // Longer than the refresh's own longest wait (the SMBus mutex, 5 s), so a refresh that got
        // the bus always finishes before the core is disposed.
        private const int CleanupRefreshWaitMs = 6000;

        private void Cleanup()
        {
            // Restart cleans up before shutting down, and the shutdown closes the window, which exits again.
            if (cleanedUp)
                return;
            cleanedUp = true;
            // Publish cleanedUp before reading the refresh flag (the task sets its flag, then reads
            // cleanedUp), so one of the two always sees the other.
            Thread.MemoryBarrier();

            // Nothing may tick into the core once it is disposed.
            StopAutoRefresh();

            // A refresh that already started keeps reading the hardware on a background thread. Let it
            // finish before the core goes away; it doesn't need the UI thread for that part.
            for (int waited = 0; Volatile.Read(ref refreshReadingHardware) != 0 && waited < CleanupRefreshWaitMs; waited += RefreshWaitStepMs)
                Thread.Sleep(RefreshWaitStepMs);

            // Still reading: disposing the core or removing the driver under it could fault. The
            // process is exiting anyway, and Windows releases what it holds.
            bool refreshStillRunning = Volatile.Read(ref refreshReadingHardware) != 0;

            // Each step on its own, so one failure doesn't skip the rest (or the shutdown after it).
            foreach (IPlugin plugin in plugins)
                TryCleanup(() => plugin?.Close());

            TryCleanup(() => sensorsWindw?.Close());
            TryCleanup(() => optionsWnd?.Close());
            TryCleanup(() => exportWnd?.Close());
            TryCleanup(() => _notifyIcon?.Dispose());
            TryCleanup(() => wheaErrorCounter.Dispose());

            if (refreshStillRunning)
                return;

            TryCleanup(() => AsusWmi?.Dispose());
            TryCleanup(() => cpu?.Dispose());

            if (settings.AutoUninstallDriver)
            {
                TryCleanup(() => App.CleanupDriverIfLastInstance((NotificationLevel)settings.AutoUninstallDriverNotificationLevel));
            }
        }

        private static void TryCleanup(Action step)
        {
            try
            {
                step();
            }
            catch (Exception ex)
            {
                CrashLog.Write("Cleanup", ex);
            }
        }

        private void ExitApplication(bool save = true)
        {
            if (isMockWindow)
            {
                Close();
                return;
            }

            try
            {
                if (save) settings.Save();
                Cleanup();
            }
            finally
            {
                Application.Current?.Shutdown();
            }
        }

        private BiosACPIFunction GetFunctionByIdString(string name)
        {
            return biosFunctions.Find(x => x.IDString == name);
        }

        private void ReadMemoryModulesInfo(List<MemoryModule> modules)
        {
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

                if (modules.Count > 1 && HasChannelTimings)
                {
                    buttonAllDimms.Visibility = Visibility.Visible;
                    comboBoxPartNumber.IsEnabled = true;
                }
            }
        }

        private void RefreshSensors()
        {
            plugins[1].Update();
            /*
            foreach (var sensor in plugins[1].Sensors)
            {
                Debug.WriteLine($"----Name: {sensor.Name}, Value: {sensor.Value}");
            }
            */
        }

        private void ReadSVI()
        {
            if ((cpu.memoryConfig.Type == MemType.DDR4 || cpu.memoryConfig.Type == MemType.LPDDR4) && timingsPanel is DDR4TimingsPanel ddr4Panel)
            {
                ApplyDdr4Vsoc(ddr4Panel, true);
            }
        }

        /// <summary>
        /// Fills a DDR4 panel's VSOC row. The live window reads VSOC from SVI2 telemetry through its plugin,
        /// reading it anew when <paramref name="update"/> is set and taking the last reading otherwise; a debug
        /// report stands in the power table's VDDCR_SOC, which the SMU reports from the same rail.
        /// </summary>
        private void ApplyDdr4Vsoc(DDR4TimingsPanel panel, bool update)
        {
            if (mockData != null)
            {
                float reportVsoc = mockData.PowerTable?.VDDCR_SOC ?? 0;
                if (reportVsoc > 0)
                    panel.rowVSOC_SVI2.Value = $"{reportVsoc:F4}V";
                return;
            }

            if (plugins.Count == 0)
                return;

            IPlugin svi2 = plugins[0];
            bool hasValue = update
                ? svi2.Update()
                : svi2.Sensors?.Count > 0 && svi2.Sensors[0].Value > 0;

            if (hasValue)
                panel.rowVSOC_SVI2.Value = $"{svi2.Sensors[0].Value:F4}V";
        }

        private bool ddr4BmcVddioValid;
        private bool ddr4MemoryConfigApplied;
        private Sensor[] ddr4DramSensors;
        private bool ddr4DramSensorsDetected;

        private bool TryReadDdr4SuperIoDramVoltage(out float voltage)
        {
            voltage = 0;

            if (!ddr4DramSensorsDetected)
            {
                // A debug report's window reads the SuperIO sensors replayed from the report.
                IEnumerable<SensorGroup> groups = mockData != null ? mockData.SensorGroups : cpu?.systemInfo?.SensorGroups;
                ddr4DramSensors = groups?
                    .Where(g => g.HardwareType == HardwareType.SuperIO)
                    .SelectMany(g => g.Sensors)
                    .Where(s => Ddr4DramSensorNames.Contains(s.Name, StringComparer.OrdinalIgnoreCase))
                    .ToArray();
                ddr4DramSensorsDetected = ddr4DramSensors != null;
            }

            var sensor = ddr4DramSensors?.FirstOrDefault(s => s.Value > 0 && s.Value < 3);

            if (sensor == null)
                return false;

            voltage = sensor.Value ?? 0;
            return true;
        }

        // TODO: Handle in DLL or replace with read from memory
        /// <summary>
        /// Fills the DDR4 panel's rails, ODT, RTT, drive strength and setup fields from
        /// <see cref="BMC"/>. The live window loads BMC.Table over WMI first; a debug report's
        /// window loads it from the report's "BIOS: Memory Controller Config" dump.
        /// </summary>
        private void ApplyDdr4MemoryConfig(DDR4TimingsPanel panel)
        {
            if (panel == null)
                return;

            float vdimm = Convert.ToSingle(Convert.ToDecimal(BMC.Config.MemVddio) / 1000);
            ddr4BmcVddioValid = vdimm > 0 && vdimm < 3;

            float memVddio = vdimm;
            bool hasMemVddio = ddr4BmcVddioValid;

            // ASUS WMI reads the machine this runs on, so it is live only; the SuperIO fallback reads
            // the report's replayed sensors in a debug report's window.
            if (!hasMemVddio && mockData == null && AsusWmi != null && AsusWmi.Status == 1)
            {
                AsusSensorInfo sensor = AsusWmi.FindSensorByName("DRAM Voltage");
                hasMemVddio = sensor != null && AsusWMI.TryParseSensorValue(sensor.Value, out memVddio) && memVddio > 0 && memVddio < 3;
            }

            if (!hasMemVddio)
                hasMemVddio = TryReadDdr4SuperIoDramVoltage(out memVddio);

            if (hasMemVddio)
            {
                panel.rowMemVddio.Value = $"{memVddio:F4}V";
                panel.rowMemVddio.IsEnabled = true;
            }
            else
            {
                panel.rowMemVddio.IsEnabled = false;
            }

            // Enabled explicitly, like VDIMM above: the label's default binding is WMIPresent, which a
            // debug report's window never has.
            float vtt = Convert.ToSingle(Convert.ToDecimal(BMC.Config.MemVtt) / 1000);
            if (vtt > 0)
            {
                panel.rowMemVtt.Value = $"{vtt:F4}V";
                panel.rowMemVtt.IsEnabled = true;
            }
            else
            {
                panel.rowMemVtt.IsEnabled = false;
            }

            // When ProcODT is 0, then all other resistance values are 0
            // Happens when one DIMM installed in A1 or A2 slot
            if (BMC.Table == null || ZenStates.Core.Utils.AllZero(BMC.Table) || BMC.Config.ProcODT < 1)
                // throw new Exception("Failed to read AMD ACPI. Odt, Setup and Drive strength parameters will be empty.");
                return;

            panel.rowProcODT.IsEnabled = true;
            panel.rowClkDrvStren.IsEnabled = true;
            panel.rowAddrCmdDrvStren.IsEnabled = true;
            panel.rowCsOdtDrvStren.IsEnabled = true;
            panel.rowCkeDrvStren.IsEnabled = true;
            panel.rowRttNom.IsEnabled = true;
            panel.rowRttWr.IsEnabled = true;
            panel.rowRttPark.IsEnabled = true;
            panel.rowAddrCmdSetup.IsEnabled = true;
            panel.rowCsOdtSetup.IsEnabled = true;
            panel.rowCkeSetup.IsEnabled = true;

            panel.rowProcODT.Value = BMC.GetProcODTString(BMC.Config.ProcODT);

            panel.rowClkDrvStren.Value = BMC.GetDrvStrenString(BMC.Config.ClkDrvStren);
            panel.rowAddrCmdDrvStren.Value = BMC.GetDrvStrenString(BMC.Config.AddrCmdDrvStren);
            panel.rowCsOdtDrvStren.Value = BMC.GetDrvStrenString(BMC.Config.CsOdtCmdDrvStren);
            panel.rowCkeDrvStren.Value = BMC.GetDrvStrenString(BMC.Config.CkeDrvStren);

            panel.rowRttNom.Value = BMC.GetRttString(BMC.Config.RttNom);
            panel.rowRttWr.Value = BMC.GetRttWrString(BMC.Config.RttWr);
            panel.rowRttPark.Value = BMC.GetRttString(BMC.Config.RttPark);

            panel.rowAddrCmdSetup.Value = $"{BMC.Config.AddrCmdSetup}";
            panel.rowCsOdtSetup.Value = $"{BMC.Config.CsOdtSetup}";
            panel.rowCkeSetup.Value = $"{BMC.Config.CkeSetup}";
        }

        private void ReadDDR4MemoryConfig()
        {
            string scope = @"root\wmi";
            string className = "AMD_ACPI";

            try
            {
                WMI.Connect($@"{scope}");

                string instanceName = WMI.GetInstanceName(scope, className);

                using (ManagementObject classInstance = new ManagementObject(scope, $"{className}.InstanceName='{instanceName}'", null))
                {
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
                    // A table shorter than the Resistances layout would be read past its end, so it is skipped
                    // and the ODT, RTT, drive strength and setup rows stay empty, as in a debug report.
                    int minTableLength = Marshal.SizeOf(typeof(BiosMemController.Resistances));
                    BiosACPIFunction cmd = GetFunctionByIdString("Get APCB Config");
                    if (cmd == null)
                    {
                        // throw new Exception("Could not get memory controller config");
                        // Use AOD table as an alternative path for now
                        byte[] aodTable = cpu.info.aod?.Table?.RawAodTable;
                        if (aodTable != null && aodTable.Length >= minTableLength)
                            BMC.Table = aodTable;
                    }
                    else
                    {
                        byte[] apcbConfig = WMI.RunCommand(classInstance, cmd.ID);
                        // BiosACPIFunction cmd = new BiosACPIFunction("Get APCB Config", 0x00010001);
                        cmd = GetFunctionByIdString("Get memory voltages");
                        if (cmd != null && apcbConfig != null && apcbConfig.Length > 30)
                        {
                            byte[] voltages = WMI.RunCommand(classInstance, cmd.ID);

                            // MEM_VDDIO is ushort, offset 27
                            // MEM_VTT is ushort, offset 29
                            for (int i = 27; voltages != null && i <= 30 && i < voltages.Length; i++)
                            {
                                byte value = voltages[i];
                                if (value > 0)
                                    apcbConfig[i] = value;
                            }
                        }

                        if (apcbConfig != null && apcbConfig.Length >= minTableLength)
                            BMC.Table = apcbConfig;
                    }
                }

                ApplyDdr4MemoryConfig(timingsPanel as DDR4TimingsPanel);
                ddr4MemoryConfigApplied = true;
            }
            catch (Exception ex)
            {
                compatMode = true;

                MessageBox.Show(
                    ex.Message,
                    "Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                Debug.WriteLine(ex.Message);
            }

            BMC?.Dispose();
        }

        // The live machine's modules, or those of the debug report a mock window shows.
        private List<MemoryModule> MemoryModules => mockData?.Modules ?? cpu.memoryConfig.Modules;

        // Decoded SPD per DIMM - read from SMBus live, parsed out of the report in a mock window.
        // Both carry the PMIC block, so anything reading rails from here works either way.
        private IDictionary<byte, Ddr5SpdInfo> ModuleSpdInfo =>
            mockData != null ? mockData.SpdInfo : cpu?.memoryConfig?.SpdInfo;

        // PMIC of the module at the given index in MemoryModules; SPD entries line up with modules by index.
        private Ddr5PmicData ModulePmicData(int moduleIndex)
        {
            if (mockData != null)
                return mockData.GetPmicData(moduleIndex);

            return ModuleSpdInfo?.Values
                .Where(d => d.IsValid)
                .ElementAtOrDefault(moduleIndex)?.PmicData;
        }

        // Always true live; a debug report has every channel only when it carries the register dump.
        private bool HasChannelTimings =>
            mockData == null || MemoryModules.All(module => mockData.Timings.Any(channel => channel.Key == module.DctOffset));

        private BaseDramTimings ChannelTimings(uint offset)
        {
            return mockData == null ? ReadTimings(offset) : mockData.Timings.FirstOrDefault(channel => channel.Key == offset).Value;
        }

        private BaseDramTimings ReadTimings(uint offset = 0)
        {
            return cpu.memoryConfig.ReadTimings(offset);
        }

        private bool WaitForInpoutDriverLoad()
        {
            Stopwatch timer = new Stopwatch();
            timer.Start();

            if (cpu.io == null)
                return false;

            bool temp;
            // Refresh until driver is opened
            do
            {
                temp = cpu.io.IsInpOutDriverOpen();
            } while (!temp && timer.Elapsed.TotalMilliseconds < 5000);

            timer.Stop();

            return temp;
        }

        private bool WaitForPowerTable()
        {
            // A PM table setup delayed by a PCI bus lock timeout is retried on refresh; give it one chance here.
            if (cpu.powerTable != null && cpu.powerTable.DramBaseAddress == 0)
                cpu.RefreshPowerTable();

            if (cpu.powerTable == null || cpu.powerTable.DramBaseAddress == 0)
            {
                HandleError("Could not initialize power table.\n\nClose the application and try again. If the issue persists, you might want to try a system restart.");
                return false;
            }

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

        private int isRefreshing = 0;
        private void PowerCfgTimer_Tick(object sender, EventArgs e)
        {
            if (Interlocked.Exchange(ref isRefreshing, 1) == 1) return;

            bool readCpuTemperature = settings.ShowCpuTemperature || (sensorsWindw != null && sensorsWindw.IsLoaded);

            // Run refresh operation in a new task
            Task.Run(() =>
            {
                // isRefreshing stays set until the UI update queued below has run.
                bool uiUpdateQueued = false;
                try
                {
                    Thread.CurrentThread.IsBackground = true;

                    // Flag first, then check: Cleanup sets cleanedUp first, then waits on the flag, so
                    // either this sees cleanedUp or Cleanup sees the flag.
                    Interlocked.Exchange(ref refreshReadingHardware, 1);

                    if (cleanedUp)
                        return;

                    var hasAsusDramVoltage = false;
                    float asusDramVoltage = 0;
                    if (AsusWmi != null && AsusWmi.Status == 1)
                    {
                        AsusWmi.UpdateSensors();
                        AsusSensorInfo sensor = AsusWmi.FindSensorByName("DRAM Voltage");
                        hasAsusDramVoltage = sensor != null && AsusWMI.TryParseSensorValue(sensor.Value, out asusDramVoltage) && asusDramVoltage > 0 && asusDramVoltage < 3;
                    }

                    //ReadDDR4MemoryConfig();
                    cpu.RefreshPowerTable();
                    cpu.systemInfo?.UpdateSensors();
                    var hasSuperIoDramVoltage = TryReadDdr4SuperIoDramVoltage(out var superIoDramVoltage);

                    var voltagesUpdated = false;
                    if (cpu.memoryConfig?.SpdInfo?.Values != null)
                    {
                        voltagesUpdated = cpu.memoryConfig.RefreshTelemetry(settings.AutoRefreshInterval);
                    }

                    // Not part of the refresh, read only while the readout or the Sensors window shows it
                    float? cpuTemperature = readCpuTemperature ? cpu.GetCpuTemperature() : null;

                    Interlocked.Exchange(ref refreshReadingHardware, 0);

                    if (cleanedUp)
                        return;

                    Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
                    {
                        try
                        {
                            if (cleanedUp)
                                return;

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
                                mainViewModel.PmicData = ModulePmicData(comboBoxPartNumber?.SelectedIndex ?? 0);

                            if (cpu.memoryConfig.Type == MemType.DDR4 || cpu.memoryConfig.Type == MemType.LPDDR4)
                            {
                                if (hasAsusDramVoltage)
                                {
                                    (timingsPanel as DDR4TimingsPanel).rowMemVddio.Value = $"{asusDramVoltage:F4}V";
                                    (timingsPanel as DDR4TimingsPanel).rowMemVddio.IsEnabled = true;
                                }
                                else if (hasSuperIoDramVoltage)
                                {
                                    (timingsPanel as DDR4TimingsPanel).rowMemVddio.Value = $"{superIoDramVoltage:F4}V";
                                }
                                else if (!ddr4BmcVddioValid)
                                {
                                    (timingsPanel as DDR4TimingsPanel).rowMemVddio.IsEnabled = false;
                                }
                            }

                            mainViewModel.RefreshSensors();
                            mainViewModel.RefreshReadouts(settings.ShowCpuTemperature ? cpuTemperature : null, wheaErrorCounter.Count);
                            cpuTemperatureSensors?.Update(cpuTemperature, cpu.powerTable?.Table);

                            lastMclk = newMclk;

                            ReadSVI();
                            // SetFrequencyString();
                            // RefreshSensors();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex.Message);
                        }
                        finally
                        {
                            Interlocked.Exchange(ref isRefreshing, 0);
                        }
                    }));
                    uiUpdateQueued = true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
                finally
                {
                    Interlocked.Exchange(ref refreshReadingHardware, 0);
                    if (!uiUpdateQueued)
                        Interlocked.Exchange(ref isRefreshing, 0);
                }
            });
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

        private AllDimmsWindow allDimmsWnd;

        private void ButtonAllDimms_Click(object sender, RoutedEventArgs e)
        {
            if (allDimmsWnd != null)
            {
                if (allDimmsWnd.WindowState == WindowState.Minimized)
                    allDimmsWnd.WindowState = WindowState.Normal;
                allDimmsWnd.Activate();
                return;
            }

            if (timingsPanel == null)
                return;

            try
            {
                allDimmsWnd = new AllDimmsWindow(
                    () => AllDimmsCapture.Run(MemoryModules, ModuleSpdInfo, ChannelTimings),
                    CreateChannelTimingsPanel,
                    mainViewModel,
                    this);

                // Opened from a debug report, it carries the report's title so it is not mistaken for the live machine.
                if (mockData != null)
                    allDimmsWnd.Title = $"{Title} - All DIMMs";

                allDimmsWnd.Closed += (s, args) => allDimmsWnd = null;
                allDimmsWnd.Show();
            }
            catch (Exception ex)
            {
                HandleError(ex.Message);
            }
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

            // The new instance must not find this one's single-instance mutex, or it takes itself for a second
            // copy and exits.
            ReleaseInstanceMutex();

            try
            {
                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                HandleError($"Could not restart {nameof(ZenTimings)}.\n{ex.Message}");
            }

            Application.Current.Shutdown();
        }

        private static void ReleaseInstanceMutex()
        {
            Mutex mutex = App.instanceMutex;
            if (mutex == null)
                return;

            App.instanceMutex = null;

            try
            {
                // Owned only when this instance created it; the app starts it on the UI thread, where this runs.
                if ((Application.Current as App)?.createdNew == true)
                    mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Not owned by this thread
            }
            finally
            {
                mutex.Dispose();
            }
        }

        private void ShowWindow()
        {
            Show();
            Activate();
            BringIntoView();
            WindowState = WindowState.Normal;
            MinimizeFootprint();
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
                Title = $"{AssemblyTitle} v{AssemblyVersion.Substring(0, AssemblyVersion.LastIndexOf('.'))}";
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
                        Height = parent.Height,
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
            // A debug report's window has no tray icon and nothing to refresh.
            if (isMockWindow || _notifyIcon == null)
            {
                MinimizeFootprint();
                return;
            }

            // Do not refresh if app is minimized and sensors window is not open, to save CPU usage
            if (WindowState == WindowState.Minimized && (sensorsWindw == null || !sensorsWindw.IsLoaded))
            {
                StopAutoRefresh();
            }
            else if (WindowState == WindowState.Normal)
            {
                StartAutoRefresh();
            }

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

            if (isMockWindow)
            {
                //SetWindowTitle();
                this.Topmost = false;
                MinimizeFootprint();
                return;
            }

            //ShowWindow();

            if (settings.StartMinimized)
            {
                WindowState = WindowState.Minimized;
            }

            SplashWindow.Stop();

            Application.Current.MainWindow = this;

            this.Topmost = false;

            if (settings.CheckForUpdates && SplashWindow.DeferUpdateCheck)
            {
                ((App)Application.Current).updater.CheckForUpdate(suppressNetworkErrorDialog: true);
            }

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
            InitLiveSnapshot();
            UpdateWheaErrorCounter();

            if (settings.AdvancedMode && settings.AutoOpenTelemetry)
                OpenSensorsWindowAfterFirstRender();

            //new Thread(() =>
            //{
            //    mainViewModel.AgesaVersion = GetAgesaVersion();
            //}).Start();
        }

        private void OptionsToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (optionsWnd == null || !optionsWnd.IsLoaded)
            {
                optionsWnd = new OptionsDialog(PowerCfgTimer, mainViewModel);
                optionsWnd.Show();
            }
            else
            {
                optionsWnd.Activate();
            }
        }

        private void AboutToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (aboutWnd == null || !aboutWnd.IsLoaded)
            {
                aboutWnd = new AboutDialog()
                {
                    Owner = this
                };
                aboutWnd.Show();
            }
            else
            {
                aboutWnd.Activate();
            }
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
                var dctOffset = MemoryModules[combo.SelectedIndex].DctOffset;
                mainViewModel.Timings = ChannelTimings(dctOffset);
                //mainViewModel.SelectedDctOffset = dctOffset;

                // The rails are per DIMM, so they follow the selection - live and from a report alike.
                mainViewModel.PmicData = ModulePmicData(combo.SelectedIndex);
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
                && settings?.SysInfoWindowTop != -1
                && IsPositionOnScreen(settings.SysInfoWindowLeft, settings.SysInfoWindowTop, settings.SysInfoWindowWidth, settings.SysInfoWindowHeight))
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

        private void AdvancedTimingsToolstripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (advancedTimingsWnd == null || !advancedTimingsWnd.IsLoaded)
            {
                advancedTimingsWnd = new AdvancedTimingsWindow
                {
                    Owner = this
                };
                advancedTimingsWnd.Show();
            }
            else
            {
                advancedTimingsWnd.Activate();
            }
        }

        private void DumpApobTable_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (cpu?.info.apob == null || cpu.info.apob.RawTable == null)
                {
                    MessageBoxModel messageBox = new MessageBoxModel
                    {
                        Text = "APOB table is not available on this system.",
                        Caption = "APOB Dump",
                        Buttons = new[] { MessageBoxButtons.Ok() }
                    };
                    MessageBox.Show(messageBox);
                    return;
                }

                Forms.SaveFileDialog saveFileDialog = new Forms.SaveFileDialog
                {
                    Filter = "Binary files (*.bin)|*.bin|APOB files (*.apob)|*.apob|All files (*.*)|*.*",
                    DefaultExt = "bin",
                    FileName = $"APOB_{cpu.info.codeName}_{cpu.systemInfo.CpuId:X8}_{DateTime.Now:yyyyMMdd_HHmmss}.bin"
                };

                if (saveFileDialog.ShowDialog() == Forms.DialogResult.OK)
                {
                    try
                    {
                        File.WriteAllBytes(saveFileDialog.FileName, cpu.info.apob.RawTable);
                        MessageBoxModel successBox = new MessageBoxModel
                        {
                            Text = $"APOB table successfully exported to:\n{saveFileDialog.FileName}",
                            Caption = "APOB Dump",
                            Buttons = new[] { MessageBoxButtons.Ok() }
                        };
                        MessageBox.Show(successBox);
                    }
                    catch (Exception ex)
                    {
                        MessageBoxModel errorBox = new MessageBoxModel
                        {
                            Text = $"Error writing file:\n{ex.Message}",
                            Caption = "APOB Dump Error",
                            Buttons = new[] { MessageBoxButtons.Ok() }
                        };
                        MessageBox.Show(errorBox);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBoxModel errorBox = new MessageBoxModel
                {
                    Text = $"Error during APOB dump:\n{ex.Message}",
                    Caption = "APOB Dump Error",
                    Buttons = new[] { MessageBoxButtons.Ok() }
                };
                MessageBox.Show(errorBox);
            }
        }

        private bool hasRendered;

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            hasRendered = true;
        }

        // Opens the sensors window at startup once this window is on screen, so the two don't draw
        // over each other while both are still loading. This window keeps the focus.
        private void OpenSensorsWindowAfterFirstRender()
        {
            if (WindowState == WindowState.Minimized)
            {
                // A minimized window isn't rendered until it is restored.
                OpenSensorsWindow(true);
                return;
            }

            Action open = () => Dispatcher.BeginInvoke(
                new Action(() => OpenSensorsWindow(settings.StartMinimized, false)),
                DispatcherPriority.ApplicationIdle);

            // Already rendered when a dialog shown from Loaded (the changelog) ran its own message loop.
            if (hasRendered)
            {
                open();
                return;
            }

            EventHandler onRendered = null;
            onRendered = (s, e) =>
            {
                ContentRendered -= onRendered;
                open();
            };
            ContentRendered += onRendered;
        }

        private void OpenSensorsWindow(bool startMinimized = false, bool activate = true)
        {
            try
            {
                double telemetryWindowWidth = 390;
                double telemetryWindowHeight = 625;
                double telemetryWindowTop = 0;
                double telemetryWindowLeft = 0;
                WindowStartupLocation location = WindowStartupLocation.CenterScreen;

                if (settings.SaveWindowPosition
                    && settings?.SensorsWindowHeight != 0
                    && settings?.SensorsWindowWidth != 0
                    && settings?.SensorsWindowLeft != -1
                    && settings?.SensorsWindowTop != -1
                    && IsPositionOnScreen(settings.SensorsWindowLeft, settings.SensorsWindowTop, settings.SensorsWindowWidth, settings.SensorsWindowHeight))
                {
                    location = WindowStartupLocation.Manual;
                    telemetryWindowLeft = settings.SensorsWindowLeft;
                    telemetryWindowTop = settings.SensorsWindowTop;
                    telemetryWindowHeight = settings.SensorsWindowHeight;
                    telemetryWindowWidth = settings.SensorsWindowWidth;
                }

                if (sensorsWindw == null || !sensorsWindw.IsLoaded)
                {
                    // Read once here, the window takes its first values as the start of its min and max
                    if (mockData == null)
                    {
                        if (cpuTemperatureSensors == null)
                            cpuTemperatureSensors = new CpuTemperatureSensors(cpu.systemInfo.SmuTableVersion);

                        cpuTemperatureSensors.Update(cpu.GetCpuTemperature(), cpu.powerTable?.Table);
                    }

                    sensorsWindw = new Windows.SensorsWindow()
                    {
                        CpuTemperatures = cpuTemperatureSensors,
                        Width = telemetryWindowWidth,
                        Height = telemetryWindowHeight,
                        WindowStartupLocation = location,
                        Top = telemetryWindowTop,
                        Left = telemetryWindowLeft,
                        ShowActivated = activate
                    };
                    sensorsWindw.Show();

                    if (startMinimized)
                        sensorsWindw.WindowState = WindowState.Minimized;
                }
                else
                {
                    sensorsWindw.Activate();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening Sensors window:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TelemetryMonitorToolstripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            OpenSensorsWindow();
        }

        private void UclkRatioMenuItem_Click(object sender, RoutedEventArgs e)
        {
            settings.Save();
            mainViewModel.RefreshUclkLabel();
        }

        private void SpdInfoToolstripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var spdWindow = new SpdInfoWindow
                {
                    Owner = this
                };
                spdWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening SPD Info:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AdonisWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (isMockWindow)
            {
                return;
            }

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
            OpenUrl("https://www.paypal.com/donate/?hosted_button_id=NLSRLE9MVDPCW");
        }

        private void MenuItem_Click_1(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://revolut.me/ivanrusanov");
        }

        private void MenuItem_Click_2(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://discord.gg/8cfR3UZ");
        }

        private void MenuItem_Click_3(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://github.com/irusanov/ZenTimings");
        }
        private void MenuItem_Click_4(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://docs.google.com/spreadsheets/d/12zg6yT_H7H-W1voyw1ZoIrj0GSE7WI4Ug-uLlv-Asa8/edit?gid=937453961#gid=937453961");
        }

        private void MenuItem_Click_6(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://drive.google.com/drive/folders/1HAJO9_jxvQrIkLb4Ws9ZfKHcHFQ_yOqp?usp=sharing");
        }

        private static void OpenUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return;

            try
            {
                using (Process.Start(url)) { }
            }
            catch (Exception ex)
            {
                // e.g. no default browser associated
                MessageBox.Show($"Could not open {url} {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            //Config Config = new Config(cpu.memoryConfig, BMC.Config/*, cpu.powerTable*/);
            //Debug.WriteLine(Config.GetXML());
        }

        private void MotherboardLinkButton_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl(VendorUtils.GetMotherboardLink(cpu.systemInfo));
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

        // Checks whether the given window rectangle is fully within the combined bounds of all
        // monitors (the virtual screen). Used to detect saved positions that are no longer valid,
        // e.g. after a monitor was disconnected or the display layout changed.
        private static bool IsPositionOnScreen(double left, double top, double width, double height)
        {
            double virtualLeft = SystemParameters.VirtualScreenLeft;
            double virtualTop = SystemParameters.VirtualScreenTop;
            double virtualRight = virtualLeft + SystemParameters.VirtualScreenWidth;
            double virtualBottom = virtualTop + SystemParameters.VirtualScreenHeight;

            return left >= virtualLeft && top >= virtualTop &&
                   left + width <= virtualRight && top + height <= virtualBottom;
        }

        // The smallest part of the window, from its top-left corner, that must be on a screen for a
        // saved position to be used: enough of the title bar to grab it.
        private const double MinVisibleWindowWidth = 200;
        private const double MinVisibleWindowHeight = 40;

        // Called before the window is first shown. Its size isn't known yet (SizeToContent), so only
        // its top-left corner has to be on a screen.
        private void RestoreWindowPosition()
        {
            if (settings.SaveWindowPosition)
            {
                if (settings?.WindowLeft == -1 || settings?.WindowTop == -1)
                {
                    return;
                }

                if (IsPositionOnScreen(settings.WindowLeft, settings.WindowTop, MinVisibleWindowWidth, MinVisibleWindowHeight))
                {
                    WindowStartupLocation = WindowStartupLocation.Manual;
                    Left = settings.WindowLeft;
                    Top = settings.WindowTop;
                }
            }
        }

        private const int RefreshWaitStepMs = 20;
        private const int RefreshWaitLimitMs = 5000;

        private DateTime? lastRefreshUtc;

        private void ReadoutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            settings.Save();
            UpdateWheaErrorCounter();
        }

        // The event log is only watched while the WHEA readout can be seen
        private void UpdateWheaErrorCounter()
        {
            if (settings.AdvancedMode && settings.ShowWheaErrors)
                wheaErrorCounter.Start();
            else
                wheaErrorCounter.Stop();
        }

        // Called once the live window is loaded, a debug report window has no live data to export
        private void InitLiveSnapshot()
        {
            // All values were read during startup, right before the window was shown
            lastRefreshUtc = DateTime.UtcNow;

            UpdateLiveSnapshotIndicator();

            // The application's own auto refresh timer drives the live snapshot, with its interval and its start/stop rules
            PowerCfgTimer.Tick += ExportTimer_Tick;
            Application.Current.Exit += (s, e) =>
            {
                if (!ExportSettings.Instance.LiveSnapshotEnabled)
                    return;

                if (ExportSettings.Instance.LiveSnapshotDeleteOnExit)
                    LiveSnapshot.Stop();
                else
                    LiveSnapshot.Detach();
            };

            if (ExportSettings.Instance.LiveSnapshotEnabled)
                UpdateLiveSnapshot();
        }

        // Runs right after the regular tick handler, which has just started the refresh task
        private void ExportTimer_Tick(object sender, EventArgs e)
        {
            lastRefreshUtc = DateTime.UtcNow;
            if (!ExportSettings.Instance.LiveSnapshotEnabled || !LiveSnapshot.IsDue)
                return;

            Task.Run(async () =>
            {
                // Wait for that refresh to finish so that the snapshot holds the new values
                for (int waited = 0; Volatile.Read(ref isRefreshing) != 0 && waited < RefreshWaitLimitMs; waited += RefreshWaitStepMs)
                    await Task.Delay(RefreshWaitStepMs);

                lastRefreshUtc = DateTime.UtcNow;
                LiveSnapshot.Update(GetSnapshotSource(true));
            });
        }

        // The menu item is checked and the button next to the screenshot button is shown while the live snapshot is on
        private void UpdateLiveSnapshotIndicator()
        {
            ExportSettings exportSettings = ExportSettings.Instance;
            bool enabled = exportSettings.LiveSnapshotEnabled;

            menuItemLiveSnapshot.IsChecked = enabled;
            buttonLiveSnapshot.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
            if (!enabled)
            {
                buttonLiveSnapshot.BeginAnimation(UIElement.OpacityProperty, null);
                return;
            }

            buttonLiveSnapshot.BeginAnimation(
                UIElement.OpacityProperty,
                new DoubleAnimation(0.45, 1.0, TimeSpan.FromSeconds(2.0))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever
                });

            string path = LiveSnapshot.GetFilePath(exportSettings.LiveSnapshotDirectory, exportSettings.LiveSnapshotFileName, exportSettings.LiveSnapshotFormat);
            double seconds = Math.Max(LiveSnapshot.MinIntervalMs, exportSettings.LiveSnapshotIntervalMs) / 1000.0;
            buttonLiveSnapshot.ToolTip = $"Live snapshot is on, click to change\n{path}\nWritten with auto refresh, every {seconds:0.#} s at most";
        }

        private SnapshotSource GetSnapshotSource(bool? autoRefreshActive = null)
        {
            return new SnapshotSource
            {
                BiosMemConfig = BMC?.Config,
                AsusSensors = AsusWmi?.sensors,
                Plugins = plugins,
                LastRefreshUtc = lastRefreshUtc,
                AutoRefreshActive = autoRefreshActive ?? PowerCfgTimer.IsEnabled,
            };
        }

        private void UpdateLiveSnapshot()
        {
            SnapshotSource source = GetSnapshotSource();
            Task.Run(() =>
            {
                if (!LiveSnapshot.WriteNow(source))
                    Dispatcher.Invoke(() => HandleError($"Could not write the live snapshot file.\n{LiveSnapshot.LastError}", "Live snapshot"));
            });
        }

        private void ExportSnapshotMenuItem_Click(object sender, RoutedEventArgs e)
        {
            object tagValue = (sender as MenuItem)?.Tag;
            SnapshotFormat format;
            if (tagValue is SnapshotFormat typed)
            {
                format = typed;
            }
            else if (tagValue is string text && text.Equals("Html", StringComparison.OrdinalIgnoreCase))
            {
                format = SnapshotFormat.Html;
            }
            else
            {
                format = SnapshotFormat.Json;
            }
            if (exportWnd != null && exportWnd.IsLoaded)
            {
                exportWnd.SelectFormat(format);
                exportWnd.Activate();
                return;
            }

            exportWnd = new ExportDialog(
                (options, selectedFormat) => SnapshotWriter.Write(SnapshotBuilder.Build(GetSnapshotSource(), options), selectedFormat),
                format,
                false,
                SnapshotBuilder.GetUnavailableSections(GetSnapshotSource()))
            {
                Owner = this
            };
            exportWnd.Closed += (s, args) => exportWnd = null;
            exportWnd.Show();
        }

        private void LiveSnapshotMenuItem_Click(object sender, RoutedEventArgs e)
        {
            bool wasEnabled = ExportSettings.Instance.LiveSnapshotEnabled;
            ExportDialog liveSnapshotWnd = new ExportDialog(null, ExportSettings.Instance.LiveSnapshotFormat, true,
                SnapshotBuilder.GetUnavailableSections(GetSnapshotSource()))
            {
                Owner = this
            };

            if (liveSnapshotWnd.ShowDialog() != true)
                return;

            UpdateLiveSnapshotIndicator();

            // Turned off: the file is the user's to keep. A kept file is simply overwritten if the same
            // folder and name are used again later.
            string written = LiveSnapshot.LastWrittenPath;
            if (wasEnabled && !ExportSettings.Instance.LiveSnapshotEnabled && written != null && System.IO.File.Exists(written))
            {
                MessageBoxResult answer = MessageBox.Show(
                    $"The live snapshot is now off.\n\nDelete the file that was being written?\n{written}",
                    "Live snapshot",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (answer != MessageBoxResult.Yes)
                {
                    LiveSnapshot.Detach();
                    return;
                }
            }

            // Remove the previous file, the folder, the name or the format may have changed
            LiveSnapshot.Remove();
            if (ExportSettings.Instance.LiveSnapshotEnabled)
                UpdateLiveSnapshot();
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

        private void OpenDebugLogAsMockWindow_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Forms.OpenFileDialog openFileDialog = new Forms.OpenFileDialog
                {
                    Filter = "Text files (*.txt;*.log)|*.txt;*.log|All files (*.*)|*.*",
                    Title = "Open ZenTimings debug report"
                };

                if (openFileDialog.ShowDialog() != Forms.DialogResult.OK)
                    return;

                string debugReportText = File.ReadAllText(openFileDialog.FileName);
                MockSystemData mockData = MockSystemData.CreateFromDebugReport(debugReportText);

                if (mockData.Warnings.Count > 0)
                {
                    Debug.WriteLine($"MockSystemData warnings for {openFileDialog.FileName}:");
                    foreach (string warning in mockData.Warnings)
                        Debug.WriteLine(" - " + warning);
                }

                BaseDramTimings mockTimings = mockData.Timings.Count > 0 ? mockData.Timings[0].Value : null;

                var viewModel = new MainViewModel(
                    mockTimings,
                    mockData.MemoryType,
                    compatMode: false,
                    settings,
                    new List<IPlugin>(),
                    null,
                    mockData.AgesaVersion,
                    mockData.PmicData,
                    mockData
                );

                MainWindow mockWindow = new MainWindow(viewModel, mockData)
                {
                    Owner = this,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };

                mockWindow.ReadMemoryModulesInfo(mockData.Modules);
                mockWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening debug report:\n{ex.Message}", "Mock Window", MessageBoxButton.OK, MessageBoxImage.Error);
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
