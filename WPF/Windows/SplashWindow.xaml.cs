using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using ZenTimings.Common;
using ZenTimings.Helpers;
using ZenTimings.Settings;
using ZenTimings.Utils;

namespace ZenTimings.Windows
{
    /// <summary>
    /// Interaction logic for SplashWindow.xaml
    /// </summary>
    public partial class SplashWindow
    {
        internal static readonly AppSettings appSettings = AppSettings.Instance;
        // Resolved at use time: the updater may not exist yet when this type is first initialized.
        internal static Updater updater => (Application.Current as App)?.updater;
        public static readonly SplashWindow splash = new SplashWindow();

        // To refresh the UI immediately
        private delegate void RefreshDelegate();

        private static void Refresh(DependencyObject obj)
        {
            obj.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Render,
                (RefreshDelegate)delegate { });
        }

        private static bool isClosed;

        // False once the splash window has been closed (e.g. after the main window opened).
        public static bool IsOpen => !isClosed;

        public SplashWindow()
        {
            InitializeComponent();
            Closed += (s, e) => isClosed = true;
        }

        // True when the app was launched by the scheduled task created for
        // AutostartWithWindows (i.e. right after user logon). In this case the
        // update check is deferred until the main window is opened, instead of
        // being performed on the splash screen.
        public static bool DeferUpdateCheck { get; private set; }

        public static void Start(bool deferUpdateCheck = false)
        {
            ApplySettings();
            DeferUpdateCheck = deferUpdateCheck;
            splash.Show();

            if (appSettings.CheckForUpdates && !DeferUpdateCheck)
                updater?.CheckForUpdate();
        }

        public static void Stop()
        {
            if (!isClosed)
                splash.Close();
        }

        public static void HideIfOpen()
        {
            if (!isClosed && splash.IsVisible)
                splash.Hide();
        }

        public static void ShowIfOpen()
        {
            if (!isClosed && !splash.IsVisible)
                splash.Show();
        }

        public static void Loading(string status)
        {
            if (isClosed)
                return;

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