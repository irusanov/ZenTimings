using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using ZenTimings.Helpers;

namespace ZenTimings.Windows
{
    /// <summary>
    /// Interaction logic for SplashWindow.xaml
    /// </summary>
    public partial class SplashWindow
    {
        internal static readonly AppSettings appSettings = AppSettings.Instance;
        internal static readonly Updater updater = (Application.Current as App)?.updater;
        public static readonly SplashWindow splash = new SplashWindow();

        // To refresh the UI immediately
        private delegate void RefreshDelegate();

        private static void Refresh(DependencyObject obj)
        {
            obj.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Render,
                (RefreshDelegate)delegate { });
        }

        public SplashWindow()
        {
            InitializeComponent();
        }

        // True when the app was launched by the scheduled task created for
        // AutostartWithWindows (i.e. right after user logon). In this case the
        // update check is deferred until the main window is opened, instead of
        // being performed on the splash screen.
        public static bool DeferUpdateCheck { get; private set; }

        public static void Start(bool deferUpdateCheck = false)
        {
            DeferUpdateCheck = deferUpdateCheck;

            splash.Show();
            ApplySettings();
            if (appSettings.CheckForUpdates && !DeferUpdateCheck)
                updater.CheckForUpdate();
        }

        public static void Stop() => splash.Close();

        public static void Loading(string status)
        {
            splash.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                splash.status.Content = status;
                Refresh(splash.status);
            }));
        }

        private static void ApplySettings()
        {
            if (DriverHelper.IsPawnIoInstalled)
            {
                try
                {
                    if (appSettings.FirstStart
                        && VendorUtils.IsRogMotherboard(CpuSingleton.Instance.systemInfo)
                        && int.Parse(appSettings.Version.Replace("1.", "")) >= 12)
                    {
                        appSettings.AppTheme = AppSettings.Theme.AsusRog;
                    }
                }
                catch (Exception ex)
                {
                    // Something went wrong, but it's not critical, so just log it and continue
                    Debug.WriteLine(ex.Message);
                }

                if (appSettings.FirstStart) appSettings.FirstStart = false;
            }

            appSettings.ApplyTheme();
        }
    }
}