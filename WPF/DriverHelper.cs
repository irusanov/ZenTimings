using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using ZenStates.Core;

namespace ZenTimings
{
    internal static class DriverHelper
    {
        public static bool IsPawnIoInstalled => PawnIo.IsInstalled;

        public static Version Version => PawnIo.Version;

        public static bool UninstallWinRing0()
        {
            return true;
        }

        public static void InstallPawnIO()
        {
            string path = ExtractPawnIO();
            if (!string.IsNullOrEmpty(path))
            {
                var process = Process.Start(new ProcessStartInfo(path, "-uninstall -silent"));
                process?.WaitForExit();

                process = Process.Start(new ProcessStartInfo(path, "-install"));
                process?.WaitForExit();

                File.Delete(path);
            }
        }

        public static async Task InstallPawnIOAsync()
        {
            string path = ExtractPawnIO();
            if (string.IsNullOrEmpty(path))
                return;

            await RunProcessAsync(path, "-uninstall -silent");
            await RunProcessAsync(path, "-install");

            File.Delete(path);
        }

        private static Task RunProcessAsync(string file, string args)
        {
            return Task.Run(() =>
            {
                var process = Process.Start(new ProcessStartInfo(file, args)
                {
                    UseShellExecute = true
                });

                process?.WaitForExit();
            });
        }

        static string ExtractPawnIO()
        {
            string destination = Path.Combine(Directory.GetCurrentDirectory(), "PawnIO_setup.exe");

            try
            {
                Stream resourceStream = typeof(MainWindow).Assembly.GetManifestResourceStream("ZenTimings.Resources.PawnIO.PawnIO_setup.exe");
                using (resourceStream)
                using (FileStream fileStream = new FileStream(destination, FileMode.Create, FileAccess.Write))
                {
                    resourceStream.CopyTo(fileStream);
                }

                return destination;
            }
            catch
            {
                return null;
            }
        }
    }
}
