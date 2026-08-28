using System;
using System.Diagnostics;
using System.IO;

namespace ZenTimings.Helpers
{
    // ZenTimings requires administrator privileges (see app.manifest), so a normal
    // HKCU\...\Run registry entry will not work: Windows cannot silently elevate an
    // app launched from the Run key at logon and will just skip it. Instead we
    // register a Scheduled Task configured to run at logon with the highest
    // privileges, which is the supported way to autostart an elevated app.
    internal static class StartupHelper
    {
        private const string TaskName = "ZenTimings";
        private const int DefaultDelaySeconds = 5;
        public const string AutostartArgument = "/autostart";

        private static string ExecutablePath => Process.GetCurrentProcess().MainModule.FileName;

        public static void SetAutostart(bool enable, int delaySeconds = DefaultDelaySeconds)
        {
            try
            {
                if (enable)
                {
                    if (delaySeconds < 0)
                        delaySeconds = 0;

                    string tempXmlPath = Path.Combine(Path.GetTempPath(), TaskName + ".xml");

                    try
                    {
                        File.WriteAllText(tempXmlPath, BuildTaskXml(delaySeconds));

                        string arguments =
                            "/Create /F /TN \"" + TaskName + "\" /XML \"" + tempXmlPath + "\"";

                        RunSchTasks(arguments);
                    }
                    finally
                    {
                        if (File.Exists(tempXmlPath))
                            File.Delete(tempXmlPath);
                    }
                }
                else
                {
                    RunSchTasks("/Delete /F /TN \"" + TaskName + "\"");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        public static bool IsAutostartEnabled()
        {
            try
            {
                int exitCode = RunSchTasks("/Query /TN \"" + TaskName + "\"");
                return exitCode == 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return false;
            }
        }

        private static string BuildTaskXml(int delaySeconds)
        {
            string delay = "PT" + delaySeconds + "S";

            return
                "<?xml version=\"1.0\" encoding=\"UTF-16\"?>\n" +
                "<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">\n" +
                "  <Triggers>\n" +
                "    <LogonTrigger>\n" +
                "      <Enabled>true</Enabled>\n" +
                "      <Delay>" + delay + "</Delay>\n" +
                "    </LogonTrigger>\n" +
                "  </Triggers>\n" +
                "  <Principals>\n" +
                "    <Principal id=\"Author\">\n" +
                "      <RunLevel>HighestAvailable</RunLevel>\n" +
                "    </Principal>\n" +
                "  </Principals>\n" +
                "  <Settings>\n" +
                "    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>\n" +
                "    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>\n" +
                "    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>\n" +
                "    <AllowHardTerminate>true</AllowHardTerminate>\n" +
                "    <StartWhenAvailable>false</StartWhenAvailable>\n" +
                "    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>\n" +
                "    <IdleSettings>\n" +
                "      <Duration>PT10M</Duration>\n" +
                "      <WaitTimeout>PT1H</WaitTimeout>\n" +
                "      <StopOnIdleEnd>true</StopOnIdleEnd>\n" +
                "      <RestartOnIdle>false</RestartOnIdle>\n" +
                "    </IdleSettings>\n" +
                "    <AllowStartOnDemand>true</AllowStartOnDemand>\n" +
                "    <Enabled>true</Enabled>\n" +
                "    <Hidden>false</Hidden>\n" +
                "    <RunOnlyIfIdle>false</RunOnlyIfIdle>\n" +
                "    <WakeToRun>false</WakeToRun>\n" +
                "    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>\n" +
                "    <Priority>3</Priority>\n" +
                "  </Settings>\n" +
                "  <Actions Context=\"Author\">\n" +
                "    <Exec>\n" +
                "      <Command>\"" + ExecutablePath + "\"</Command>\n" +
                "      <Arguments>" + AutostartArgument + "</Arguments>\n" +
                "    </Exec>\n" +
                "  </Actions>\n" +
                "</Task>";
        }

        private static int RunSchTasks(string arguments)
        {
            using (Process process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                process.Start();
                process.WaitForExit();
                return process.ExitCode;
            }
        }
    }
}

