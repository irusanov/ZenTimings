using System;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace ZenTimings.Helpers
{
    /// <summary>
    /// Counts the WHEA-Logger events in the System log since Windows started. The log is read once
    /// when counting starts; after that the count follows the log's own notifications, nothing is polled.
    /// </summary>
    internal sealed class WheaErrorCounter : IDisposable
    {
        private const string LogName = "System";
        private const string ProviderName = "Microsoft-Windows-WHEA-Logger";
        private const string ProviderQuery = "*[System[Provider[@Name='" + ProviderName + "']]]";

        private readonly object SyncRoot = new object();
        private EventLogWatcher watcher;
        private bool started;
        private int generation;
        private int count = -1;

        /// <summary>Events since boot, or -1 while the log has not been read, so a failure never shows as zero.</summary>
        public int Count => Volatile.Read(ref count);

        public void Start()
        {
            lock (SyncRoot)
            {
                if (started)
                    return;

                started = true;
                int current = ++generation;
                Task.Run(() => Open(current));
            }
        }

        public void Stop()
        {
            lock (SyncRoot)
            {
                generation++;
                started = false;
                watcher?.Dispose();
                watcher = null;
                Volatile.Write(ref count, -1);
            }
        }

        public void Dispose() => Stop();

        private void Open(int current)
        {
            try
            {
                int found = 0;
                EventBookmark last = null;
                string sinceBoot = $"*[System[Provider[@Name='{ProviderName}'] and TimeCreated[timediff(@SystemTime) <= {GetTickCount64()}]]]";

                using (var reader = new EventLogReader(new EventLogQuery(LogName, PathType.LogName, sinceBoot)))
                {
                    for (EventRecord record = reader.ReadEvent(); record != null; record = reader.ReadEvent())
                    {
                        using (record)
                        {
                            found++;
                            last = record.Bookmark;
                        }
                    }
                }

                lock (SyncRoot)
                {
                    if (current != generation)
                        return;

                    // Starting after the last counted event also picks up any logged while the count was read
                    watcher = new EventLogWatcher(new EventLogQuery(LogName, PathType.LogName, ProviderQuery), last);
                    watcher.EventRecordWritten += Watcher_EventRecordWritten;
                    Volatile.Write(ref count, found);
                    watcher.Enabled = true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        private void Watcher_EventRecordWritten(object sender, EventRecordWrittenEventArgs e)
        {
            if (e.EventRecord == null)
                return;

            e.EventRecord.Dispose();
            Interlocked.Increment(ref count);
        }

        [DllImport("kernel32.dll")]
        private static extern ulong GetTickCount64();
    }
}
