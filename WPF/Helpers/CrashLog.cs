using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace ZenTimings.Helpers
{
    /// <summary>
    /// Appends unexpected exceptions to crash.log next to the executable (the app is portable and
    /// keeps its settings there), or in %TEMP% when that folder isn't writable. Never throws.
    /// </summary>
    internal static class CrashLog
    {
        private const string FileName = "crash.log";
        private const string OldFileName = "crash.old.log";

        // Past this size the log is moved to crash.old.log, so it can't grow without limit.
        private const long MaxSizeBytes = 1024 * 1024;

        private static readonly object SyncRoot = new object();

        /// <summary>Where the last entry was written; null if none could be written.</summary>
        public static string LastPath { get; private set; }

        public static void Write(string source, object exception, bool terminating = false)
        {
            try
            {
                string entry = BuildEntry(source, exception, terminating);

                lock (SyncRoot)
                {
                    if (TryAppend(AppDomain.CurrentDomain.BaseDirectory, entry))
                        return;

                    TryAppend(Path.GetTempPath(), entry);
                }
            }
            catch
            {
                // Logging a crash must never cause another one.
            }
        }

        private static string BuildEntry(string source, object exception, bool terminating)
        {
            var sb = new StringBuilder();
            sb.AppendLine("==================================================");
            sb.AppendLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {source}{(terminating ? " (terminating)" : "")}");
            sb.AppendLine($"Version: {GetVersion()}");
            sb.AppendLine($"OS:      {Environment.OSVersion} ({(Environment.Is64BitOperatingSystem ? "64" : "32")}-bit)");
            sb.AppendLine($"CLR:     {Environment.Version}");
            sb.AppendLine();
            sb.AppendLine(exception?.ToString() ?? "(no exception object)");
            sb.AppendLine();
            return sb.ToString();
        }

        private static string GetVersion()
        {
            try
            {
                var attribute = (AssemblyFileVersionAttribute)Attribute.GetCustomAttribute(
                    Assembly.GetExecutingAssembly(), typeof(AssemblyFileVersionAttribute), false);
                return attribute?.Version ?? "unknown";
            }
            catch
            {
                return "unknown";
            }
        }

        private static bool TryAppend(string directory, string entry)
        {
            try
            {
                string path = Path.Combine(directory, FileName);

                var info = new FileInfo(path);
                if (info.Exists && info.Length > MaxSizeBytes)
                {
                    string oldPath = Path.Combine(directory, OldFileName);
                    if (File.Exists(oldPath))
                        File.Delete(oldPath);
                    File.Move(path, oldPath);
                }

                File.AppendAllText(path, entry, Encoding.UTF8);
                LastPath = path;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
