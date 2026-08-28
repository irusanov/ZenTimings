using System;
using System.Diagnostics;
using System.IO;

namespace ZenTimings.Helpers
{
    internal static class DriverCleaner
    {
        public static void Clean(bool showNotifications = true)
        {
            try
            {
                string helperPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "DriverCleaner.exe");

                if (!File.Exists(helperPath))
                    return;

                ProcessStartInfo startInfo = new ProcessStartInfo();

                string arguments = showNotifications ? "/notify" : string.Empty;

                startInfo.FileName = helperPath;
                startInfo.UseShellExecute = true;
                startInfo.Verb = "runas";
                startInfo.Arguments = arguments;
                startInfo.WindowStyle = ProcessWindowStyle.Hidden;

                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                // UAC cancelled or helper could not be started.
            }
        }
    }
}
