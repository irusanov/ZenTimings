using System;
using System.Diagnostics;
using System.IO;
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
                var process = Process.Start(new ProcessStartInfo(path, "-install"));
                process?.WaitForExit();

                File.Delete(path);
            }
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
