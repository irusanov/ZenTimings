using Microsoft.Win32;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace ZenTimings.DriverCleaner
{
    internal class Program
    {
        private const string ServiceName = "inpoutx64";

        private const string RegistryKeyPath = @"SYSTEM\CurrentControlSet\Services\" + ServiceName;

        private static readonly string DriverFilePath = @"C:\Windows\System32\drivers\" + ServiceName + ".sys";

        /*
         * Maximum time we wait for the driver to stop.
         *
         * This process is independent from ZenTimings, so the
         * application itself does not have to wait for this.
         */
        private const int StopTimeoutMs = 10000;

        /*
         * Maximum time spent retrying deletion of the driver file.
         */
        private const int DeleteRetryTimeoutMs = 5000;

        private const int DeleteRetryIntervalMs = 250;

        private const uint SC_MANAGER_CONNECT = 0x0001;

        private const uint SERVICE_QUERY_STATUS = 0x0004;
        private const uint SERVICE_STOP = 0x0020;
        private const uint DELETE = 0x00010000;

        private const uint SERVICE_CONTROL_STOP = 0x00000001;

        private const uint SERVICE_STOPPED = 0x00000001;
        private const uint SERVICE_START_PENDING = 0x00000002;
        private const uint SERVICE_STOP_PENDING = 0x00000003;
        private const uint SERVICE_RUNNING = 0x00000004;
        private const uint SERVICE_CONTINUE_PENDING = 0x00000005;
        private const uint SERVICE_PAUSE_PENDING = 0x00000006;
        private const uint SERVICE_PAUSED = 0x00000007;

        private const uint SC_STATUS_PROCESS_INFO = 0;

        private const int ERROR_SERVICE_NOT_ACTIVE = 1062;
        private const int ERROR_SERVICE_CANNOT_ACCEPT_CTRL = 1061;
        private const int ERROR_SERVICE_DOES_NOT_EXIST = 1060;
        private const int ERROR_SERVICE_MARKED_FOR_DELETE = 1072;
        private const int ERROR_SERVICE_REQUEST_TIMEOUT = 1053;

        [StructLayout(LayoutKind.Sequential)]
        private struct SERVICE_STATUS
        {
            public uint dwServiceType;
            public uint dwCurrentState;
            public uint dwControlsAccepted;
            public uint dwWin32ExitCode;
            public uint dwServiceSpecificExitCode;
            public uint dwCheckPoint;
            public uint dwWaitHint;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SERVICE_STATUS_PROCESS
        {
            public uint dwServiceType;
            public uint dwCurrentState;
            public uint dwControlsAccepted;
            public uint dwWin32ExitCode;
            public uint dwServiceSpecificExitCode;
            public uint dwCheckPoint;
            public uint dwWaitHint;
            public uint dwProcessId;
            public uint dwServiceFlags;
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern IntPtr OpenSCManager(string lpMachineName, string lpDatabaseName, uint dwDesiredAccess);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern IntPtr OpenService(IntPtr hSCManager, string lpServiceName, uint dwDesiredAccess);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool CloseServiceHandle(IntPtr hSCObject);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool QueryServiceStatusEx(IntPtr hService, uint InfoLevel, out SERVICE_STATUS_PROCESS lpBuffer, uint cbBufSize, out uint pcbBytesNeeded);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool ControlService(IntPtr hService, uint dwControl, out SERVICE_STATUS lpServiceStatus);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool DeleteService(IntPtr hService);

        private static int Main(string[] args)
        {
            bool showNotifications = true;

            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "/silent", StringComparison.OrdinalIgnoreCase))
                {
                    showNotifications = false;
                    break;
                }
            }

            try
            {
                Cleanup(showNotifications);
            }
            catch (Exception ex)
            {
                if (showNotifications)
                    Notification.Show("ZenTimings", "Driver cleanup failed: " + ex.Message, true);
            }

            return 0;
        }

        private static void Cleanup(bool showNotifications = true)
        {
            IntPtr scm = IntPtr.Zero;
            IntPtr service = IntPtr.Zero;

            try
            {
                /*
                 * Open Service Control Manager.
                 */
                scm = OpenSCManager(null, null, SC_MANAGER_CONNECT);

                if (scm == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    if (showNotifications)
                        Notification.Show("ZenTimings", "Could not open the Service Control Manager. Error: " + error, true);

                    return;
                }

                /*
                 * Open the driver service.
                 */
                service = OpenService(scm, ServiceName, SERVICE_QUERY_STATUS | SERVICE_STOP | DELETE);

                if (service == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();

                    /*
                     * Service no longer exists.
                     * Still try to remove the driver file.
                     */
                    if (error == ERROR_SERVICE_DOES_NOT_EXIST)
                    {
                        if (TryDeleteDriverFile())
                        {
                            if (showNotifications)
                                Notification.Show("ZenTimings", "The inpoutx64 driver was successfully removed.");
                        }
                        else
                        {
                            if (showNotifications)
                                Notification.Show("ZenTimings", "The service was removed, but the driver file could not be deleted.", true);
                        }

                        return;
                    }

                    if (showNotifications)
                        Notification.Show("ZenTimings", "Could not open the inpoutx64 service. Error: " + error, true);

                    return;
                }

                SERVICE_STATUS_PROCESS status;

                /*
                 * Get initial status.
                 */
                if (!QueryStatus(service, out status))
                {
                    int error = Marshal.GetLastWin32Error();
                    if (showNotifications)
                        Notification.Show("ZenTimings", "Could not query the inpoutx64 service. Error: " + error, true);

                    return;
                }

                /*
                 * If another process/service operation is already
                 * stopping the driver, do not interfere with it.
                 */
                if (status.dwCurrentState == SERVICE_STOP_PENDING)
                {
                    if (showNotifications)
                        Notification.Show("ZenTimings", "The inpoutx64 driver is already being stopped or is currently in use. Cleanup was aborted.", true);

                    return;
                }

                /*
                 * Already stopped.
                 */
                if (status.dwCurrentState == SERVICE_STOPPED)
                {
                    DeleteServiceNoThrow(service);

                    CloseServiceHandle(service);
                    service = IntPtr.Zero;

                    CloseServiceHandle(scm);
                    scm = IntPtr.Zero;

                    TryDeleteRegistryKey();

                    if (!TryDeleteDriverFile())
                    {
                        if (showNotifications)
                            Notification.Show("ZenTimings", "The inpoutx64 service was removed, but the driver file could not be deleted.", true);

                        return;
                    }

                    if (showNotifications)
                        Notification.Show("ZenTimings", "The inpoutx64 driver was successfully removed.");

                    return;
                }

                /*
                 * If the service is currently starting, continuing or
                 * pausing, wait until it reaches a stable state.
                 */
                if (status.dwCurrentState == SERVICE_START_PENDING || status.dwCurrentState == SERVICE_CONTINUE_PENDING || status.dwCurrentState == SERVICE_PAUSE_PENDING)
                {
                    DateTime deadline = DateTime.UtcNow.AddMilliseconds(StopTimeoutMs);

                    WaitUntilStable(service, deadline);

                    if (!QueryStatus(service, out status))
                    {
                        int error = Marshal.GetLastWin32Error();

                        if (showNotifications)
                            Notification.Show("ZenTimings", "Could not query the inpoutx64 service. Error: " + error, true);

                        return;
                    }

                    /*
                     * It may have become STOP_PENDING while we waited.
                     */
                    if (status.dwCurrentState == SERVICE_STOP_PENDING)
                    {
                        if (showNotifications)
                            Notification.Show("ZenTimings", "The inpoutx64 driver is already being stopped or is currently in use. Cleanup was aborted.", true);

                        return;
                    }

                    if (status.dwCurrentState == SERVICE_STOPPED)
                    {
                        DeleteServiceNoThrow(service);

                        CloseServiceHandle(service);
                        service = IntPtr.Zero;

                        CloseServiceHandle(scm);
                        scm = IntPtr.Zero;

                        TryDeleteRegistryKey();

                        if (TryDeleteDriverFile())
                        {
                            if (showNotifications)
                                Notification.Show("ZenTimings", "The inpoutx64 driver was successfully removed.");
                        }
                        else
                        {
                            if (showNotifications)
                                Notification.Show("ZenTimings", "The inpoutx64 service was removed, but the driver file could not be deleted.", true);
                        }

                        return;
                    }
                }

                /*
                 * Only request STOP if the service is actually running.
                 */
                if (status.dwCurrentState == SERVICE_RUNNING)
                {
                    SERVICE_STATUS controlStatus;

                    if (!ControlService(service, SERVICE_CONTROL_STOP, out controlStatus))
                    {
                        int error = Marshal.GetLastWin32Error();

                        /*
                         * ERROR_SERVICE_NOT_ACTIVE means it stopped
                         * between our status query and ControlService.
                         */
                        if (error == ERROR_SERVICE_NOT_ACTIVE)
                        {
                            /*
                             * Re-query below.
                             */
                        }
                        else
                        {
                            if (showNotifications)
                                Notification.Show("ZenTimings", "Could not stop the inpoutx64 driver. Error: " + error, true);

                            return;
                        }
                    }
                }

                /*
                 * Wait for STOPPED.
                 */
                DateTime stopDeadline = DateTime.UtcNow.AddMilliseconds(StopTimeoutMs);

                if (!WaitUntilStopped(service, stopDeadline))
                {
                    if (showNotifications)
                        Notification.Show("ZenTimings", "The inpoutx64 driver could not be stopped. It may be in use by another application. Cleanup was aborted.", true);

                    return;
                }

                /*
                 * The service is definitely stopped now.
                 */
                if (!DeleteServiceNoThrow(service))
                {
                    if (showNotifications)
                        Notification.Show("ZenTimings", "The inpoutx64 driver stopped, but the service could not be deleted.", true);

                    return;
                }
            }
            finally
            {
                if (service != IntPtr.Zero)
                {
                    CloseServiceHandle(service);
                    service = IntPtr.Zero;
                }

                if (scm != IntPtr.Zero)
                {
                    CloseServiceHandle(scm);
                    scm = IntPtr.Zero;
                }
            }

            /*
             * DeleteService() has been called and all service handles
             * have been closed.
             */
            TryDeleteRegistryKey();

            if (!TryDeleteDriverFile())
            {
                if (showNotifications)
                    Notification.Show("ZenTimings", "The inpoutx64 service was removed, but the driver file could not be deleted.", true);

                return;
            }

            if (showNotifications)
                Notification.Show("ZenTimings", "The inpoutx64 driver was successfully removed.");
        }

        private static bool QueryStatus(IntPtr service, out SERVICE_STATUS_PROCESS status)
        {
            uint bytesNeeded;

            return QueryServiceStatusEx(service, SC_STATUS_PROCESS_INFO, out status, (uint)Marshal.SizeOf(typeof(SERVICE_STATUS_PROCESS)), out bytesNeeded);
        }

        private static void WaitUntilStable(IntPtr service, DateTime deadline)
        {
            SERVICE_STATUS_PROCESS status;

            while (DateTime.UtcNow < deadline)
            {
                if (!QueryStatus(service, out status))
                    return;

                if (status.dwCurrentState != SERVICE_START_PENDING && status.dwCurrentState != SERVICE_CONTINUE_PENDING && status.dwCurrentState != SERVICE_PAUSE_PENDING)
                    return;

                SleepFromWaitHint(status.dwWaitHint);
            }
        }

        private static bool WaitUntilStopped(IntPtr service, DateTime deadline)
        {
            SERVICE_STATUS_PROCESS status;

            while (DateTime.UtcNow < deadline)
            {
                if (!QueryStatus(service, out status))
                    return false;

                if (status.dwCurrentState == SERVICE_STOPPED)
                    return true;

                /*
                 * If it changes to something other than
                 * STOP_PENDING, stop waiting.
                 */
                if (status.dwCurrentState != SERVICE_STOP_PENDING)
                    return false;

                SleepFromWaitHint(status.dwWaitHint);
            }

            return false;
        }

        private static void SleepFromWaitHint(uint waitHint)
        {
            int sleep = (int)(waitHint / 10);

            if (sleep < 100)
                sleep = 100;

            if (sleep > 500)
                sleep = 500;

            System.Threading.Thread.Sleep(sleep);
        }

        private static bool DeleteServiceNoThrow(IntPtr service)
        {
            try
            {
                if (DeleteService(service))
                    return true;

                int error = Marshal.GetLastWin32Error();

                /*
                 * Already marked for deletion is effectively
                 * successful for our purposes.
                 */
                if (error == ERROR_SERVICE_MARKED_FOR_DELETE)
                    return true;
            }
            catch
            {
            }

            return false;
        }

        private static void TryDeleteRegistryKey()
        {
            try
            {
                using (RegistryKey services = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services", true))
                {
                    if (services == null)
                        return;

                    try
                    {
                        services.DeleteSubKeyTree(ServiceName, false);
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }

        private static bool TryDeleteDriverFile()
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(DeleteRetryTimeoutMs);

            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    if (!File.Exists(DriverFilePath))
                        return true;

                    File.Delete(DriverFilePath);

                    if (!File.Exists(DriverFilePath))
                        return true;
                }
                catch
                {
                }

                System.Threading.Thread.Sleep(DeleteRetryIntervalMs);
            }

            return !File.Exists(DriverFilePath);
        }
    }
}