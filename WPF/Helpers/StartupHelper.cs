using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;

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

        /// <summary>
        /// The task starts the exe elevated at every logon, so whoever can replace it (or a DLL next to it) gets
        /// administrator rights then. Looks at the exe, its folder and the folders above it for write access of a
        /// normal user: Everyone, Users, Authenticated Users, Interactive and the current user, which includes owning
        /// the folder. Returns the first path found that such a user can change, or null. Deny entries are not
        /// weighed, so the answer errs on the side of reporting; null also when the permissions can't be read.
        /// </summary>
        public static string FindUserWritableLocation()
        {
            try
            {
                SecurityIdentifier me = WindowsIdentity.GetCurrent().User;
                var weak = new List<SecurityIdentifier>
                {
                    new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                    new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
                    new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
                    new SecurityIdentifier(WellKnownSidType.InteractiveSid, null),
                };
                if (me != null)
                    weak.Add(me);

                // The exe itself, its folder (files can be added there too), and the folders above it, which matter
                // only as far as they let the folder be removed or its permissions changed
                const FileSystemRights fileRights = FileSystemRights.WriteData | FileSystemRights.AppendData |
                    FileSystemRights.Delete | FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership;
                const FileSystemRights folderRights = fileRights | FileSystemRights.DeleteSubdirectoriesAndFiles;
                const FileSystemRights parentRights = FileSystemRights.DeleteSubdirectoriesAndFiles |
                    FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership;

                var exe = new FileInfo(ExecutablePath);
                if (HasUserAccess(exe.GetAccessControl(), weak, me, fileRights))
                    return exe.FullName;

                bool first = true;
                for (DirectoryInfo dir = exe.Directory; dir != null; dir = dir.Parent)
                {
                    if (HasUserAccess(dir.GetAccessControl(), weak, me, first ? folderRights : parentRights))
                        return dir.FullName;
                    first = false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Could not check the permissions of the install folder: " + ex.Message);
            }

            return null;
        }

        private static bool HasUserAccess(FileSystemSecurity security, List<SecurityIdentifier> weak, SecurityIdentifier me, FileSystemRights rights)
        {
            foreach (FileSystemAccessRule rule in security.GetAccessRules(true, true, typeof(SecurityIdentifier)))
            {
                // Entries that only apply to what is inside the folder do not give access to the folder itself
                if (rule.AccessControlType != AccessControlType.Allow || (rule.PropagationFlags & PropagationFlags.InheritOnly) != 0)
                    continue;

                if ((rule.FileSystemRights & rights) != 0 && weak.Contains((SecurityIdentifier)rule.IdentityReference))
                    return true;
            }

            // The owner can always rewrite the permissions
            return me != null && me.Equals(security.GetOwner(typeof(SecurityIdentifier)));
        }

        /// <summary>
        /// Creates or removes the autostart scheduled task.
        /// Returns false if schtasks.exe reported a failure.
        /// </summary>
        public static bool SetAutostart(bool enable, int delaySeconds = DefaultDelaySeconds)
        {
            try
            {
                if (enable)
                {
                    if (delaySeconds < 0)
                        delaySeconds = 0;

                    // The XML defines what runs elevated at logon, so it must not be
                    // written to a user-writable location such as %TEMP%.
                    string workDir = SecureDirectoryHelper.CreateAdminOnlyDirectory("Task");

                    try
                    {
                        string xmlPath = Path.Combine(workDir, TaskName + ".xml");

                        // The XML declares UTF-16, so write it as UTF-16 (with BOM).
                        File.WriteAllText(xmlPath, BuildTaskXml(delaySeconds), Encoding.Unicode);

                        string arguments =
                            "/Create /F /TN \"" + TaskName + "\" /XML \"" + xmlPath + "\"";

                        int exitCode = RunSchTasks(arguments);
                        if (exitCode != 0)
                        {
                            Debug.WriteLine("schtasks /Create failed with exit code " + exitCode);
                            return false;
                        }
                    }
                    finally
                    {
                        SecureDirectoryHelper.TryDelete(workDir);
                    }
                }
                else
                {
                    // Nothing to remove
                    if (!IsAutostartEnabled())
                        return true;

                    int exitCode = RunSchTasks("/Delete /F /TN \"" + TaskName + "\"");
                    if (exitCode != 0)
                    {
                        Debug.WriteLine("schtasks /Delete failed with exit code " + exitCode);
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return false;
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
            string exePath = ExecutablePath;
            string workingDirectory = Path.GetDirectoryName(exePath) ?? string.Empty;

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
                "      <Command>\"" + SecurityElement.Escape(exePath) + "\"</Command>\n" +
                "      <Arguments>" + SecurityElement.Escape(AutostartArgument) + "</Arguments>\n" +
                "      <WorkingDirectory>" + SecurityElement.Escape(workingDirectory) + "</WorkingDirectory>\n" +
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
                    FileName = Path.Combine(Environment.SystemDirectory, "schtasks.exe"),
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                process.ErrorDataReceived += (s, e) => { };
                process.Start();

                // Drain the redirected streams so schtasks can never block on a full pipe.
                process.BeginErrorReadLine();
                process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return process.ExitCode;
            }
        }
    }
}

