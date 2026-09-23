using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
using ZenStates.Core.OHWM;
using ZenTimings.Helpers;
using ZenTimings.Settings;
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

        public App()
        {
            // Registered here rather than in OnStartup: the constructor runs before App.xaml's
            // resources (themes, AdonisUI) are loaded and before any static settings are read, and
            // in driver-cleanup mode too, so failures there are logged as well.
            RegisterUnhandledExceptionHandlers();
        }

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

        // Every unexpected exception is written to crash.log (see CrashLog). An exception on the UI
        // thread is also shown and marked handled, so a failure in one action (a link that can't be
        // opened, a dialog that throws) doesn't close the app; the normal exit path still runs later.
        // Set while the error box is open: its message loop keeps timers and layout running, so an
        // exception that repeats is only logged instead of stacking more boxes.
        private static bool showingError;

        private void RegisterUnhandledExceptionHandlers()
        {
            DispatcherUnhandledException += (sender, args) =>
            {
                CrashLog.Write("UI thread", args.Exception);

                // Driver-cleanup mode has no UI and must not linger: log and exit.
                if (IsDriverCleanupMode)
                {
                    args.Handled = true;
                    Environment.Exit(1);
                    return;
                }

                args.Handled = true;

                // Until the main window is up (OnStartup, window construction) there is nothing to keep
                // running: carrying on would leave a windowless process holding the single-instance
                // mutex. Show the error, then exit.
                bool started = Current?.MainWindow is global::ZenTimings.MainWindow mainWindow && mainWindow.IsLoaded;

                if (showingError)
                {
                    if (!started)
                        Environment.Exit(1);
                    return;
                }

                showingError = true;
                try
                {
                    string logHint = CrashLog.LastPath != null ? $"\n\nDetails were written to {CrashLog.LastPath}" : "";
                    MessageBox.Show(
                        $"An unexpected error occurred: {args.Exception.Message}{logHint}",
                        "ZenTimings",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                catch
                {
                    // Nothing more can be done if the message box itself fails.
                }
                finally
                {
                    showingError = false;
                }

                if (!started)
                    Environment.Exit(1);
            };

            // Unobserved task exceptions no longer end the process (.NET 4.5+); log them only.
            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                CrashLog.Write("Unobserved task", args.Exception);
                args.SetObserved();
            };

            // Background-thread crashes can't be stopped here; record them before the process ends.
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
                CrashLog.Write("Background thread", args.ExceptionObject, args.IsTerminating);
        }

        private static void StartCleanupProcess(StartupEventArgs e)
        {
            NotificationLevel notificationLevel = GetNotificationLevel(e.Args);

            using (Mutex cleanupMutex = new Mutex(false, cleanupMutexName))
            {
                AcquireMutex(cleanupMutex);

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
                if (AcquireMutex(cleanupMutex, 0))
                {
                    cleanupMutex.ReleaseMutex();
                    return;
                }

                // Defer the update check here; it is performed by the regular
                // SplashWindow.Start() call in OnStartup once cleanup has finished.
                SplashWindow.Start(true);
                SplashWindow.Loading("Waiting for driver cleanup...");

                AcquireMutex(cleanupMutex);
                cleanupMutex.ReleaseMutex();

                // SplashWindow.Stop();
            }
        }

        internal static void CleanupDriverIfLastInstance(NotificationLevel notificationLevel = NotificationLevel.All)
        {
            using (Mutex cleanupMutex = new Mutex(false, cleanupMutexName))
            {
                AcquireMutex(cleanupMutex);

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

        /// <summary>
        /// Waits for the mutex. An abandoned mutex (previous owner exited without
        /// releasing it) is still acquired by the caller, so treat it as success.
        /// </summary>
        private static bool AcquireMutex(Mutex mutex, int millisecondsTimeout = Timeout.Infinite)
        {
            try
            {
                return mutex.WaitOne(millisecondsTimeout);
            }
            catch (AbandonedMutexException)
            {
                return true;
            }
        }

        private static bool IsLastInstance()
        {
            int currentProcessId;
            string currentProcessName;

            using (Process currentProcess = Process.GetCurrentProcess())
            {
                currentProcessId = currentProcess.Id;
                currentProcessName = currentProcess.ProcessName;
            }

            Process[] processes = Process.GetProcessesByName(currentProcessName);

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