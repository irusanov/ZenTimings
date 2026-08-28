using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using ZenStates.Core.OHWM;
using ZenTimings.Helpers;
using ZenTimings.Windows;

namespace ZenTimings
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        internal const string mutexName = "Local\\ZenTimings";
        private const string cleanupMutexName = "Local\\ZenTimings.DriverCleanup";
        internal static Mutex instanceMutex;
        internal bool createdNew;
        public Updater updater;

        protected override void OnStartup(StartupEventArgs e)
        {
            instanceMutex = new Mutex(true, mutexName, out createdNew);

            if (!createdNew && AppSettings.Instance.SingleInstance)
            {
                // App is already running! Exit the application and show the other window.
                InteropMethods.PostMessage((IntPtr)InteropMethods.HWND_BROADCAST, InteropMethods.WM_SHOWME, IntPtr.Zero, IntPtr.Zero);
                Current.Shutdown();
                Environment.Exit(0);
            }

            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");
            FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(
                        XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)));

            updater = new Updater();

            bool startedFromScheduledTask = Array.Exists(e.Args,
                arg => arg.Equals(StartupHelper.AutostartArgument, StringComparison.OrdinalIgnoreCase));

            GC.KeepAlive(instanceMutex);
            SplashWindow.Start(startedFromScheduledTask && AppSettings.Instance.AutostartWithWindows);
            base.OnStartup(e);
        }

        internal static void CleanupDriverIfLastInstance(bool showNotification = true)
        {
            using (Mutex cleanupMutex =
                new Mutex(false, cleanupMutexName))
            {
                try
                {
                    cleanupMutex.WaitOne();

                    if (IsLastInstance())
                    {
                        DriverHelper.UninstallInpoutx64(showNotification);
                    }
                }
                finally
                {
                    cleanupMutex.ReleaseMutex();
                }
            }
        }

        private static bool IsLastInstance()
        {
            int currentProcessId = Process.GetCurrentProcess().Id;
            Process[] processes = Process.GetProcessesByName("ZenTimings");

            try
            {
                foreach (Process process in processes)
                {
                    try
                    {
                        if (process.Id != currentProcessId)
                        {
                            return false;
                        }
                    }
                    catch
                    {
                        // The process may have exited between
                        // enumeration and inspection.
                    }
                }

                return true;
            }
            finally
            {
                foreach (Process process in processes)
                {
                    process.Dispose();
                }
            }
        }
    }
}