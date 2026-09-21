using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using ZenStates.Core.PawnIo;

namespace ZenTimings.Helpers
{
    internal static class DriverHelper
    {
        public static bool IsPawnIoInstalled => PawnIo.IsInstalled;

        public static Version Version => PawnIo.Version;

        public static Version BundledVersion => new Version(PawnIOBuildInfo.Version);

        public static bool UninstallWinRing0()
        {
            return true;
        }

        // Installer exit codes treated as success (ERROR_SUCCESS, ERROR_SUCCESS_REBOOT_REQUIRED)
        private static bool IsSuccessExitCode(int exitCode) => exitCode == 0 || exitCode == 3010;

        /// <summary>
        /// Runs the bundled PawnIO installer. Returns false (after informing the user)
        /// if the installer could not be extracted, started or reported an error.
        /// </summary>
        public static bool InstallPawnIO()
        {
            string workDir = null;

            try
            {
                workDir = SecureDirectoryHelper.CreateAdminOnlyDirectory("PawnIO");
                string path = ExtractPawnIO(workDir);

                RunProcess(path, "-uninstall -silent");
                int exitCode = RunProcess(path, "-install");

                if (!IsSuccessExitCode(exitCode))
                {
                    ShowInstallError($"The PawnIO installer exited with code {exitCode}.");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                ShowInstallError(ex.Message);
                return false;
            }
            finally
            {
                SecureDirectoryHelper.TryDelete(workDir);
            }
        }

        public static async Task<bool> InstallPawnIOAsync()
        {
            string workDir = null;

            try
            {
                workDir = SecureDirectoryHelper.CreateAdminOnlyDirectory("PawnIO");
                string path = ExtractPawnIO(workDir);

                await Task.Run(() => RunProcess(path, "-uninstall -silent"));
                int exitCode = await Task.Run(() => RunProcess(path, "-install"));

                if (!IsSuccessExitCode(exitCode))
                {
                    ShowInstallError($"The PawnIO installer exited with code {exitCode}.");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                ShowInstallError(ex.Message);
                return false;
            }
            finally
            {
                SecureDirectoryHelper.TryDelete(workDir);
            }
        }

        private static void ShowInstallError(string details)
        {
            try
            {
                AdonisUI.Controls.MessageBox.Show(
                    "PawnIO could not be installed." + Environment.NewLine + details,
                    "PawnIO",
                    AdonisUI.Controls.MessageBoxButton.OK,
                    AdonisUI.Controls.MessageBoxImage.Error);
            }
            catch
            {
                // Not on a UI thread / no application; the caller still gets false.
            }
        }

        private static int RunProcess(string file, string args)
        {
            using (var process = Process.Start(new ProcessStartInfo(file, args)
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(file)
            }))
            {
                if (process == null)
                    throw new InvalidOperationException("Could not start the PawnIO installer.");

                process.WaitForExit();
                return process.ExitCode;
            }
        }

        // Extracts the embedded installer into a directory only Administrators/SYSTEM
        // can write to, so it cannot be replaced before it is executed elevated.
        private static string ExtractPawnIO(string workDir)
        {
            string destination = Path.Combine(workDir, "PawnIO_setup.exe");

            using (Stream resourceStream = typeof(MainWindow).Assembly.GetManifestResourceStream("ZenTimings.Resources.PawnIO.PawnIO_setup.exe"))
            {
                if (resourceStream == null)
                    throw new FileNotFoundException("The bundled PawnIO installer is missing.");

                using (FileStream fileStream = new FileStream(destination, FileMode.CreateNew, FileAccess.Write))
                {
                    resourceStream.CopyTo(fileStream);
                }
            }

            return destination;
        }
    }
}
