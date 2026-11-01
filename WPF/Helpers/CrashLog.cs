using System;
using System.Globalization;
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
        private const string Separator = "==================================================";
        private const string StampFormat = "yyyy-MM-dd HH:mm:ss";

        /// <summary>Stands for "crash.log had no terminating entry" where a stamp is stored.</summary>
        public const string NoEntry = "none";

        /// <summary>An entry of crash.log.</summary>
        public sealed class Entry
        {
            /// <summary>When it was written, local time as "yyyy-MM-dd HH:mm:ss" (sorts as text).</summary>
            public string Stamp { get; set; }

            /// <summary>The whole entry.</summary>
            public string Text { get; set; }
        }

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
            sb.AppendLine(Separator);
            // Invariant: a background thread runs with the OS culture, whose time separator may not be ':'
            sb.AppendLine($"{DateTime.Now.ToString(StampFormat, CultureInfo.InvariantCulture)}  {source}{(terminating ? " (terminating)" : "")}");
            sb.AppendLine($"Version: {GetVersion()}");
            sb.AppendLine($"OS:      {Environment.OSVersion} ({(Environment.Is64BitOperatingSystem ? "64" : "32")}-bit)");
            sb.AppendLine($"CLR:     {Environment.Version}");
            sb.AppendLine();
            sb.AppendLine(exception?.ToString() ?? "(no exception object)");
            sb.AppendLine();
            return sb.ToString();
        }

        /// <summary>
        /// The newest entry of a crash that closed the app (written as terminating), from crash.log next to the
        /// executable or in %TEMP%; null when there is none or crash.log can't be read.
        /// </summary>
        public static Entry FindLastTerminating()
        {
            Entry last = null;

            foreach (string directory in new[] { AppDomain.CurrentDomain.BaseDirectory, Path.GetTempPath() })
            {
                try
                {
                    string path = Path.Combine(directory, FileName);
                    if (!File.Exists(path))
                        continue;

                    string content;
                    lock (SyncRoot)
                        content = File.ReadAllText(path, Encoding.UTF8);

                    foreach (string block in content.Split(new[] { Separator }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        string text = block.Trim();
                        int lineEnd = text.IndexOf('\n');
                        string header = (lineEnd < 0 ? text : text.Substring(0, lineEnd)).Trim();
                        if (header.Length < StampFormat.Length || !header.EndsWith("(terminating)", StringComparison.Ordinal))
                            continue;

                        string stamp = header.Substring(0, StampFormat.Length);
                        if (!DateTime.TryParseExact(stamp, StampFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                            continue;

                        if (last == null || string.CompareOrdinal(stamp, last.Stamp) > 0)
                            last = new Entry { Stamp = stamp, Text = text };
                    }
                }
                catch
                {
                    // An unreadable log only means there is nothing to offer
                }
            }

            return last;
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
