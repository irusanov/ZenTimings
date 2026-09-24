using System;
using System.IO;
using System.IO.Compression;
using System.Security.AccessControl;
using System.Security.Principal;

namespace ZenTimings.Helpers
{
    /// <summary>
    /// Creates private working directories for files that are later executed
    /// with administrator privileges (update script, PawnIO installer, task XML).
    /// The directories are created with an explicit, non-inherited ACL granting
    /// access only to Administrators and SYSTEM, so a non-elevated process cannot
    /// replace or modify their contents between verification and use.
    /// </summary>
    internal static class SecureDirectoryHelper
    {
        public static string CreateAdminOnlyDirectory(string purpose)
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            if (string.IsNullOrEmpty(root))
                root = AppDomain.CurrentDomain.BaseDirectory;

            var security = new DirectorySecurity();
            security.SetAccessRuleProtection(true, false);

            var inheritance = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
                FileSystemRights.FullControl, inheritance, PropagationFlags.None, AccessControlType.Allow));
            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                FileSystemRights.FullControl, inheritance, PropagationFlags.None, AccessControlType.Allow));

            for (int attempt = 0; attempt < 5; attempt++)
            {
                string path = Path.Combine(root, "ZenTimings_" + purpose + "_" + Guid.NewGuid().ToString("N"));

                // Never reuse an existing directory: its ACL is not under our control.
                if (Directory.Exists(path) || File.Exists(path))
                    continue;

                // The ACL is applied atomically at creation time.
                Directory.CreateDirectory(path, security);
                return path;
            }

            throw new IOException("Could not create a private working directory.");
        }

        public static void TryDelete(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch
            {
                // Best effort
            }
        }

        /// <summary>
        /// Writes the verified zip bytes next to <paramref name="targetDir"/> (inside the
        /// protected work directory) and extracts them. ZipFile.ExtractToDirectory rejects
        /// entries that would resolve outside of the target directory.
        /// </summary>
        public static void ExtractZip(byte[] zipData, string targetDir)
        {
            string zipPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(targetDir)), "update.zip");
            File.WriteAllBytes(zipPath, zipData);
            try
            {
                ZipFile.ExtractToDirectory(zipPath, targetDir);
            }
            finally
            {
                File.Delete(zipPath);
            }
        }
    }
}
