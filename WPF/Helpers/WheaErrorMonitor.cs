using System;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;

namespace ZenTimings.Helpers
{
    /// <summary>
    /// Counts the WHEA errors logged since boot (Microsoft-Windows-WHEA-Logger events in the System log: corrected and
    /// uncorrected machine check, PCIe and memory errors), as cheaply as possible: the events since boot are counted once
    /// on a background thread, then an event log subscription adds each new one as Windows logs it. Nothing is polled;
    /// <see cref="Count"/> only reads a field. Started on first use and kept for the life of the process, so reopening the
    /// Sensors window does not count again.
    /// </summary>
    internal sealed class WheaErrorMonitor
    {
        private const string LogName = "System";
        private const string ProviderName = "Microsoft-Windows-WHEA-Logger";

        private static readonly object StartLock = new object();
        private static WheaErrorMonitor _instance;

        private EventLogWatcher _watcher;
        private int _count;
        private volatile bool _ready;
        private volatile bool _failed;

        private WheaErrorMonitor()
        {
        }

        /// <summary>The monitor, started on the first call.</summary>
        public static WheaErrorMonitor Instance
        {
            get
            {
                lock (StartLock)
                {
                    if (_instance == null)
                    {
                        _instance = new WheaErrorMonitor();
                        _instance.Start();
                    }

                    return _instance;
                }
            }
        }

        /// <summary>WHEA errors logged since boot; null until the first count is done, or when the System log can't be read.</summary>
        public int? Count => _ready && !_failed ? Volatile.Read(ref _count) : (int?)null;

        /// <summary>False when the System log could not be read or subscribed to.</summary>
        public bool IsAvailable => !_failed;

        [DllImport("kernel32.dll")]
        private static extern ulong GetTickCount64();

        private void Start()
        {
            var thread = new Thread(Initialize)
            {
                IsBackground = true,
                Name = "WHEA counter",
                Priority = ThreadPriority.BelowNormal,
            };
            thread.Start();
        }

        private void Initialize()
        {
            try
            {
                DateTime boot = DateTime.UtcNow - TimeSpan.FromMilliseconds(GetTickCount64());
                string xpath = string.Format(CultureInfo.InvariantCulture,
                    "*[System[Provider[@Name='{0}'] and TimeCreated[@SystemTime>='{1:yyyy-MM-ddTHH:mm:ss.fffZ}']]]",
                    ProviderName, boot);

                // The events since boot, once. The bookmark of the last one starts the subscription right after it, so an
                // event logged in between is neither missed nor counted twice.
                int count = 0;
                EventBookmark bookmark = null;
                using (var reader = new EventLogReader(new EventLogQuery(LogName, PathType.LogName, xpath)))
                {
                    EventRecord record;
                    while ((record = reader.ReadEvent()) != null)
                    {
                        using (record)
                        {
                            count++;
                            bookmark = record.Bookmark;
                        }
                    }
                }

                Interlocked.Add(ref _count, count);

                var query = new EventLogQuery(LogName, PathType.LogName, xpath);
                _watcher = bookmark != null ? new EventLogWatcher(query, bookmark, false) : new EventLogWatcher(query);
                _watcher.EventRecordWritten += Watcher_EventRecordWritten;
                _watcher.Enabled = true;
                _ready = true;
            }
            catch (Exception ex)
            {
                // No access to the System log, or the event log service is not running
                Debug.WriteLine("WHEA counter: " + ex.Message);
                _failed = true;
            }
        }

        private void Watcher_EventRecordWritten(object sender, EventRecordWrittenEventArgs e)
        {
            using (e.EventRecord)
            {
                if (e.EventRecord != null)
                    Interlocked.Increment(ref _count);
            }
        }
    }
}
