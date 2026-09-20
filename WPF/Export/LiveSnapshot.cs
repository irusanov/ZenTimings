using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ZenTimings.Settings;

namespace ZenTimings.Export
{
    /// <summary>
    /// Keeps a snapshot file up to date so that local tools can read the current state without talking to the application.
    /// Off by default. The file goes next to the application unless the user picks another folder.
    /// </summary>
    public static class LiveSnapshot
    {
        public const int MinIntervalMs = 1000;
        public const string DefaultFileName = "live_snapshot_device_config";

        private const int MaxFileNameLength = 100;

        private static readonly object FileLock = new object();
        private static int lastWriteTick;
        private static bool hasWritten;
        private static int writing;
        private static int generation;
        private static volatile bool stopped;
        private static string lastWrittenPath;

        public static string DefaultDirectory => AppDomain.CurrentDomain.BaseDirectory;

        public static string LastError { get; private set; }

        /// <summary>
        /// The file this process has written, null when there is none.
        /// </summary>
        public static string LastWrittenPath => lastWrittenPath;

        public static string GetFilePath(string directory, string fileName, SnapshotFormat format)
        {
            if (string.IsNullOrWhiteSpace(directory))
                directory = DefaultDirectory;

            string extension;
            switch (format)
            {
                case SnapshotFormat.Text:
                    extension = ".txt";
                    break;
                case SnapshotFormat.Html:
                    extension = ".html";
                    break;
                default:
                    extension = ".json";
                    break;
            }

            return Path.Combine(directory, CleanFileName(fileName) + extension);
        }

        /// <summary>
        /// Returns a plain file name without folders and without extension, or the default name when nothing usable is left.
        /// The name comes from a settings file, so it is never trusted to be a safe path.
        /// </summary>
        public static string CleanFileName(string fileName)
        {
            string name = (fileName ?? string.Empty).Trim();
            if (name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(0, name.LastIndexOf('.'));
            }

            var sb = new StringBuilder(name.Length);
            char[] invalid = Path.GetInvalidFileNameChars();
            foreach (char c in name)
            {
                if (Array.IndexOf(invalid, c) < 0)
                    sb.Append(c);
            }

            name = sb.ToString().Trim(' ', '.');
            if (name.Length > MaxFileNameLength)
                name = name.Substring(0, MaxFileNameLength).Trim(' ', '.');

            return name.Length == 0 || IsReservedName(name) ? DefaultFileName : name;
        }

        private static bool IsReservedName(string name)
        {
            string upper = name.ToUpperInvariant();
            int dot = upper.IndexOf('.');
            if (dot >= 0)
                upper = upper.Substring(0, dot);

            if (upper == "CON" || upper == "PRN" || upper == "AUX" || upper == "NUL")
                return true;

            return upper.Length == 4 && (upper.StartsWith("COM") || upper.StartsWith("LPT")) && char.IsDigit(upper[3]);
        }

        // A file is only replaced or removed when it is a snapshot this application wrote
        private static bool IsSnapshotFile(string path)
        {
            try
            {
                var buffer = new char[SnapshotWriter.SignatureLength];
                using (var reader = new StreamReader(path, Encoding.UTF8))
                {
                    int read = reader.Read(buffer, 0, buffer.Length);
                    return SnapshotWriter.HasSignature(new string(buffer, 0, read));
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// True when the configured interval has elapsed since the last write.
        /// </summary>
        public static bool IsDue
        {
            get
            {
                if (stopped)
                    return false;

                int interval = Math.Max(MinIntervalMs, ExportSettings.Instance.LiveSnapshotIntervalMs);
                return !hasWritten || unchecked(Environment.TickCount - lastWriteTick) >= interval;
            }
        }

        /// <summary>
        /// Called after an auto refresh. The snapshot is built in place from the values that were just refreshed,
        /// the disk write is handed over to the thread pool so that a slow disk never delays the next refresh.
        /// </summary>
        public static void Update(SnapshotSource source)
        {
            // Checked again here: the call may have been queued before the user turned the feature off
            ExportSettings settings = ExportSettings.Instance;
            int now = Environment.TickCount;
            if (!settings.LiveSnapshotEnabled || !IsDue)
                return;

            // The previous write is still in progress, skip this one
            if (Interlocked.CompareExchange(ref writing, 1, 0) != 0)
                return;

            try
            {
                string content = Build(source, settings, out string target);
                int current = generation;
                lastWriteTick = now;
                hasWritten = true;

                Task.Run(() =>
                {
                    try
                    {
                        Write(content, target, current);
                    }
                    finally
                    {
                        Interlocked.Exchange(ref writing, 0);
                    }
                });
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Interlocked.Exchange(ref writing, 0);
            }
        }

        /// <summary>
        /// Builds and writes the snapshot on the calling thread. Returns false and sets LastError on failure.
        /// </summary>
        public static bool WriteNow(SnapshotSource source)
        {
            if (stopped)
                return false;

            try
            {
                string content = Build(source, ExportSettings.Instance, out string target);
                lastWriteTick = Environment.TickCount;
                hasWritten = true;
                return Write(content, target, generation);
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return false;
            }
        }

        private static string Build(SnapshotSource source, ExportSettings settings, out string target)
        {
            // Serial numbers are never written to a file other processes can read
            var options = new SnapshotOptions
            {
                Sections = settings.LiveSnapshotSections,
                IncludeSerialNumbers = false,
                IncludeLegend = settings.IncludeLegend,
            };

            target = GetFilePath(settings.LiveSnapshotDirectory, settings.LiveSnapshotFileName, settings.LiveSnapshotFormat);
            return SnapshotWriter.Write(SnapshotBuilder.Build(source, options), settings.LiveSnapshotFormat);
        }

        // The application runs elevated and the folder can be writable by anyone, so nothing is written through
        // a name somebody else could have prepared: the data goes to a new file with a random name first.
        private static bool Write(string content, string target, int expectedGeneration)
        {
            lock (FileLock)
            {
                // Remove() was called after this snapshot had been queued
                if (expectedGeneration != generation)
                    return false;

                string temp = null;
                try
                {
                    // The name is free to choose, never replace a file that is not a snapshot
                    if (target != lastWrittenPath && File.Exists(target) && !IsSnapshotFile(target))
                        throw new IOException($"{target} already exists and is not a ZenTimings snapshot, choose another file name.");

                    string directory = Path.GetDirectoryName(target);
                    Directory.CreateDirectory(directory);
                    if ((new DirectoryInfo(directory).Attributes & FileAttributes.ReparsePoint) != 0)
                        throw new IOException($"{directory} is a link, refusing to write into it.");

                    temp = Path.Combine(directory, Path.GetRandomFileName() + ".tmp");
                    byte[] bytes = new UTF8Encoding(false).GetBytes(content);
                    using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        stream.Write(bytes, 0, bytes.Length);

                    if (File.Exists(target) && (File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0)
                        File.Delete(target);

                    if (File.Exists(target))
                        File.Replace(temp, target, null);
                    else
                        File.Move(temp, target);

                    lastWrittenPath = target;
                    LastError = null;
                    return true;
                }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                    TryDelete(temp);
                    return false;
                }
            }
        }

        /// <summary>
        /// Removes the file for good, used when the application exits.
        /// </summary>
        public static void Stop()
        {
            stopped = true;
            Remove();
        }

        /// <summary>
        /// Stops tracking the written file and leaves it on disk. A snapshot that is still queued is dropped.
        /// </summary>
        public static void Detach()
        {
            lock (FileLock)
            {
                generation++;
                hasWritten = false;
                lastWrittenPath = null;
            }
        }

        /// <summary>
        /// Removes the snapshot written by this process, including one left in a folder or format that is no longer selected.
        /// </summary>
        public static void Remove()
        {
            lock (FileLock)
            {
                generation++;
                hasWritten = false;

                TryDelete(lastWrittenPath);
                lastWrittenPath = null;

                ExportSettings settings = ExportSettings.Instance;
                foreach (SnapshotFormat format in Enum.GetValues(typeof(SnapshotFormat)))
                {
                    string path = GetFilePath(settings.LiveSnapshotDirectory, settings.LiveSnapshotFileName, format);
                    if (File.Exists(path) && IsSnapshotFile(path))
                        TryDelete(path);
                }
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path))
                    File.Delete(path);
            }
            catch
            {
                // ignored
            }
        }
    }
}
