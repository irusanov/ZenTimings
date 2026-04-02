using System;
using System.Windows;
using System.Windows.Threading;

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

        public static void Start()
        {
            splash.Show();
            ApplySettings();
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
            if (appSettings.FirstStart
                && VendorUtils.IsRogMotherboard(CpuSingleton.Instance.systemInfo)
                && int.Parse(appSettings.Version.Replace("1.", "")) >= 12)
            {
                appSettings.AppTheme = AppSettings.Theme.AsusRog;
            }

            appSettings.ApplyTheme();

            if (appSettings.FirstStart) appSettings.FirstStart = false;
            if (appSettings.CheckForUpdates) updater.CheckForUpdate();
        }
    }
}