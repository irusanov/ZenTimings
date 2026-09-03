using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using ZenStates.Core.OHWM;
using ZenTimings.Helpers;
using ZenTimings.Windows;
using static ZenTimings.Helpers.DriverCleaner;

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

        internal static bool IsDriverCleanupMode { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            IsDriverCleanupMode = e.Args.Any(
                a => string.Equals(a, "/driver-cleanup", StringComparison.OrdinalIgnoreCase));

            if (IsDriverCleanupMode)
            {
                StartCleanupProcess(e);
                return;
            }

            WaitForDriverCleanup();

            instanceMutex = new Mutex(true, mutexName, out createdNew);

            if (!createdNew && AppSettings.Instance.SingleInstance)
            {
                // App is already running! Exit the application and
                // show the other window.
                InteropMethods.PostMessage(
                    (IntPtr)InteropMethods.HWND_BROADCAST,
                    InteropMethods.WM_SHOWME,
                    IntPtr.Zero,
                    IntPtr.Zero);

                Current.Shutdown();
                Environment.Exit(0);
            }

            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");

            FrameworkElement.LanguageProperty.OverrideMetadata(
                typeof(FrameworkElement),
                new FrameworkPropertyMetadata(
                    XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)));

            updater = new Updater();

            bool startedFromScheduledTask = Array.Exists(e.Args, arg => arg.Equals(StartupHelper.AutostartArgument, StringComparison.OrdinalIgnoreCase));

            GC.KeepAlive(instanceMutex);

            SplashWindow.Start(startedFromScheduledTask && AppSettings.Instance.AutostartWithWindows);

            base.OnStartup(e);
        }

        private static void StartCleanupProcess(StartupEventArgs e)
        {
            NotificationLevel notificationLevel = GetNotificationLevel(e.Args);

            using (Mutex cleanupMutex = new Mutex(false, cleanupMutexName))
            {
                cleanupMutex.WaitOne();

                try
                {
                    DriverCleaner.Cleanup(notificationLevel);
                }
                finally
                {
                    cleanupMutex.ReleaseMutex();
                }
            }

            Environment.Exit(0);
        }

        private static NotificationLevel GetNotificationLevel(string[] args)
        {
            string value = args.FirstOrDefault(a => a.StartsWith("/notifications:", StringComparison.OrdinalIgnoreCase))?.Substring("/notifications:".Length);

            if (Enum.TryParse(value, true, out NotificationLevel level))
                return level;

            return NotificationLevel.All;
        }

        private static void WaitForDriverCleanup()
        {
            using (Mutex cleanupMutex = new Mutex(false, cleanupMutexName))
            {
                if (cleanupMutex.WaitOne(0))
                {
                    cleanupMutex.ReleaseMutex();
                    return;
                }

                SplashWindow.Start(false);
                SplashWindow.Loading("Waiting for driver cleanup...");

                cleanupMutex.WaitOne();
                cleanupMutex.ReleaseMutex();

                // SplashWindow.Stop();
            }
        }

        internal static void CleanupDriverIfLastInstance(NotificationLevel notificationLevel = NotificationLevel.All)
        {
            using (Mutex cleanupMutex = new Mutex(false, cleanupMutexName))
            {
                cleanupMutex.WaitOne();

                try
                {
                    if (!IsLastInstance())
                        return;

                    if (!StartDriverCleanup(notificationLevel))
                        return;
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
                            return false;
                    }
                    catch
                    {
                    }
                }

                return true;
            }
            finally
            {
                foreach (Process process in processes)
                    process.Dispose();
            }
        }

        internal static bool StartDriverCleanup(NotificationLevel notificationLevel = NotificationLevel.All)
        {
            if (IsDriverCleanupMode)
                return false;

            try
            {
                string exePath = Process.GetCurrentProcess().MainModule.FileName;
                string arguments = "/driver-cleanup /notifications:" + notificationLevel.ToString().ToLowerInvariant();

                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = arguments,
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });

                return true;
            }
            catch
            {
                /*
                 * Cleanup must never prevent ZenTimings from closing.
                 */
                return false;
            }
        }
    }
}