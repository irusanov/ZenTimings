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
            //if (!appSettings.NotifiedWinRing0)
            //{
            //    AdonisUI.Controls.MessageBoxResult result = AdonisUI.Controls.MessageBox.Show(
            //        "ZenTimings uses a kernel driver (WinRing0) which is classified as vulnerable."
            //        + Environment.NewLine
            //        + Environment.NewLine
            //        + "In order to successfully run the application you would need to add its folder to Windows Defender (or other AntiVirus software) exclusions list."
            //        + Environment.NewLine
            //        + Environment.NewLine
            //        + "By doing this, you agree that you understand the risks of excluding a folder from Windows Defender scans."
            //        + Environment.NewLine
            //        + "We are working on removing this dependency in future releases."
            //        + Environment.NewLine
            //        + Environment.NewLine
            //        + "If you do not agree, the application will now close.",

            //        nameof(ZenTimings),
            //        AdonisUI.Controls.MessageBoxButton.OKCancel,
            //        AdonisUI.Controls.MessageBoxImage.Warning);

            //    if (result.Equals(MessageBoxResult.OK))
            //    {
            //        appSettings.NotifiedWinRing0 = true;
            //        appSettings.Save();
            //    }
            //    else
            //    {
            //        Application.Current.Shutdown();
            //    }
            //}

            InitializeComponent();

            if (!DriverHelper.IsPawnIoInstalled)
            {
                AdonisUI.Controls.MessageBox.Show(
                    "This is experimental build for testing purposes only." +
                    Environment.NewLine +
                    "PawnIO driver is required to be installed in dev mode " +
                    "and Driver Signature Enforcement to be disabled." +
                    Environment.NewLine + Environment.NewLine +
                    "Please refer to the README_PawnIO.pdf file for instructions." +
                    Environment.NewLine +
                    "The application will now close.",
                    nameof(ZenTimings),
                    AdonisUI.Controls.MessageBoxButton.OK,
                    AdonisUI.Controls.MessageBoxImage.Warning
                );
                Application.Current.Shutdown();
            }

            //if (DriverHelper.IsPawnIoInstalled)
            //{
            //    if (DriverHelper.Version < new Version(2, 0, 0, 0))
            //    {
            //        AdonisUI.Controls.MessageBoxResult result = AdonisUI.Controls.MessageBox.Show(
            //            "PawnIO is outdated, do you want to update it?",
            //            nameof(ZenTimings),
            //            AdonisUI.Controls.MessageBoxButton.OKCancel,
            //            AdonisUI.Controls.MessageBoxImage.Warning
            //        );
            //        if (result == AdonisUI.Controls.MessageBoxResult.OK)
            //            DriverHelper.InstallPawnIO();
            //    }
            //}
            //else
            //{
            //    {
            //        AdonisUI.Controls.MessageBoxResult result = AdonisUI.Controls.MessageBox.Show(
            //            "PawnIO is not installed, do you want to install it?",
            //            nameof(ZenTimings),
            //            AdonisUI.Controls.MessageBoxButton.OKCancel,
            //            AdonisUI.Controls.MessageBoxImage.Warning
            //        );
            //        if (result == AdonisUI.Controls.MessageBoxResult.OK)
            //            DriverHelper.InstallPawnIO();
            //    }
            //}
        }

        public static void Start()
        {
            splash.Show();

            if (appSettings.AppTheme != AppSettings.Theme.Light)
                appSettings.ChangeTheme();

            if (appSettings.CheckForUpdates) updater.CheckForUpdate();
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
    }
}