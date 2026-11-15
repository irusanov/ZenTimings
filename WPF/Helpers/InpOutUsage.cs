using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace ZenTimings.Helpers
{
    /// <summary>
    /// Tells whether another program has the inpoutx64 device open, so the driver cleanup can
    /// leave the driver installed.
    /// </summary>
    /// <remarks>
    /// There is no handshake with other programs: the inpoutx64 library installs, starts and
    /// (depending on the version) removes the driver on its own. This scans the system handle
    /// table for any other process holding \Device\inpoutx64. That works for any program,
    /// whatever it is called or which copy of the library it uses, and a program that does not
    /// load the driver is not affected.
    ///
    /// A program that starts right after the check can still be missed. If the scan cannot run
    /// (no access) or does not finish in time, the result is "not in use", so the cleanup behaves
    /// as it did before this check existed.
    ///
    /// The scan is the expensive part, so it runs on several threads. The name of a handle can
    /// only be queried by asking the object itself, and for a few handle types (e.g. a synchronous
    /// pipe with a pending read) that call never returns. A watchdog replaces a thread that has
    /// been inside such a call for too long and abandons it.
    /// </remarks>
    internal static class InpOutUsage
    {
        private const string DeviceName = @"\Device\inpoutx64";

        // Overall limit for the scan.
        private const int ScanTimeoutMs = 5000;

        // A name query taking longer than this is considered blocked.
        private const int NameQueryTimeoutMs = 100;

        private const int MaxScanThreads = 8;

        private const int NameBufferSize = 2048;

        private const int SystemExtendedHandleInformation = 0x40;
        private const int ObjectNameInformation = 1;

        private const int STATUS_INFO_LENGTH_MISMATCH = unchecked((int)0xC0000004);

        private const uint PROCESS_DUP_HANDLE = 0x0040;
        private const uint DUPLICATE_SAME_ACCESS = 0x00000002;

        // SYSTEM_HANDLE_INFORMATION_EX (64-bit): two pointer-sized header fields, then 40-byte entries.
        private const int HeaderSize = 16;
        private const int EntrySize = 40;

        [DllImport("ntdll.dll")]
        private static extern int NtQuerySystemInformation(int informationClass, IntPtr buffer, int length, out int returnLength);

        [DllImport("ntdll.dll")]
        private static extern int NtQueryObject(IntPtr handle, int informationClass, IntPtr buffer, int length, out int returnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DuplicateHandle(IntPtr sourceProcess, IntPtr sourceHandle, IntPtr targetProcess, out IntPtr targetHandle, uint desiredAccess, bool inheritHandle, uint options);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern void OutputDebugString(string message);

        private struct Candidate
        {
            public int Pid;
            public IntPtr Process;
            public IntPtr Handle;
        }

        private sealed class ScanThread
        {
            public volatile bool Retired;

            // Stopwatch timestamp of the name query in progress, 0 when not in one.
            public long QueryStart;
        }

        private sealed class ScanState
        {
            public readonly object Sync = new object();
            public readonly List<ScanThread> Threads = new List<ScanThread>();
            public List<Candidate> Candidates;
            public DateTime Deadline;
            public volatile bool Stop;
            public int Next = -1;
            public int FoundPid;
            public int Active;
            public int Checked;
            public int TimedOut;
        }

        /// <summary>
        /// Returns true if another process has the driver's device open. <paramref name="user"/>
        /// is the name of that process, for the notification.
        /// </summary>
        /// <param name="ignoreProcessId">
        /// The ZenTimings process that started the cleanup. It is usually still exiting and still
        /// has the driver open, which the cleanup is meant to cope with. Ignored only if it is
        /// still a process with our own name, in case its id has been reused.
        /// </param>
        internal static bool IsUsedByOtherProcess(int ignoreProcessId, out string user)
        {
            user = null;

            if (!Environment.Is64BitProcess)
                return false;

            Stopwatch timer = Stopwatch.StartNew();

            try
            {
                return HasDeviceOpen(ValidateIgnoredProcess(ignoreProcessId), timer, out user);
            }
            catch
            {
                // Can't tell: don't block the cleanup on that.
                return false;
            }
        }

        private static int ValidateIgnoredProcess(int processId)
        {
            if (processId <= 0)
                return 0;

            try
            {
                using (Process current = Process.GetCurrentProcess())
                using (Process other = Process.GetProcessById(processId))
                {
                    if (string.Equals(other.ProcessName, current.ProcessName, StringComparison.OrdinalIgnoreCase))
                        return processId;
                }
            }
            catch
            {
                // Not running (anymore): nothing to ignore.
            }

            return 0;
        }

        private static bool HasDeviceOpen(int ignoreId, Stopwatch timer, out string user)
        {
            user = null;

            int currentId;
            string exePath;

            using (Process current = Process.GetCurrentProcess())
            {
                currentId = current.Id;
                exePath = current.MainModule.FileName;
            }

            // Handles are matched by object type, and the index of the File type is not fixed
            // between Windows versions. Open a file here and look up its type index.
            using (FileStream probe = new FileStream(exePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                IntPtr table = QueryHandleTable();

                if (table == IntPtr.Zero)
                    return false;

                try
                {
                    ushort fileTypeIndex;

                    if (!TryFindTypeIndex(table, currentId, probe.SafeFileHandle.DangerousGetHandle(), out fileTypeIndex))
                        return false;

                    ScanState state = new ScanState();
                    state.Deadline = DateTime.UtcNow.AddMilliseconds(ScanTimeoutMs);

                    Dictionary<int, IntPtr> processHandles = new Dictionary<int, IntPtr>();

                    try
                    {
                        state.Candidates = CollectCandidates(table, currentId, ignoreId, fileTypeIndex, processHandles);

                        int found = RunScan(state);

                        Trace(string.Format("InpOutUsage: {0} ms, {1} candidates, {2} checked, {3} blocked, {4}",
                            timer.ElapsedMilliseconds, state.Candidates.Count, state.Checked, state.TimedOut, found != 0 ? "in use" : "not in use"));

                        if (found != 0)
                        {
                            user = ProcessName(found);
                            return true;
                        }

                        return false;
                    }
                    finally
                    {
                        // Threads that are still running may still be using these.
                        if (Volatile.Read(ref state.Active) <= 0)
                        {
                            foreach (IntPtr process in processHandles.Values)
                            {
                                if (process != IntPtr.Zero)
                                    CloseHandle(process);
                            }
                        }
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(table);
                }
            }
        }

        private static void Trace(string message)
        {
            try
            {
                OutputDebugString(message);
            }
            catch
            {
            }
        }

        // ---------------------------------------------------------------- handle table

        private static IntPtr QueryHandleTable()
        {
            int size = 4 * 1024 * 1024;

            for (int attempt = 0; attempt < 8; attempt++)
            {
                IntPtr buffer = Marshal.AllocHGlobal(size);
                int returned;
                int status = NtQuerySystemInformation(SystemExtendedHandleInformation, buffer, size, out returned);

                if (status >= 0)
                    return buffer;

                Marshal.FreeHGlobal(buffer);

                if (status != STATUS_INFO_LENGTH_MISMATCH)
                    return IntPtr.Zero;

                // The table grows while we are asking; leave room for that.
                size = Math.Max(size * 2, returned + 4 * 1024 * 1024);
            }

            return IntPtr.Zero;
        }

        private static long EntryCount(IntPtr table)
        {
            return Marshal.ReadInt64(table, 0);
        }

        private static IntPtr EntryAt(IntPtr table, long index)
        {
            return IntPtr.Add(table, HeaderSize + (int)(index * EntrySize));
        }

        private static bool TryFindTypeIndex(IntPtr table, int processId, IntPtr handleValue, out ushort typeIndex)
        {
            long count = EntryCount(table);

            for (long i = 0; i < count; i++)
            {
                IntPtr entry = EntryAt(table, i);

                if (Marshal.ReadInt64(entry, 8) == processId && Marshal.ReadInt64(entry, 16) == handleValue.ToInt64())
                {
                    typeIndex = (ushort)Marshal.ReadInt16(entry, 30);
                    return true;
                }
            }

            typeIndex = 0;
            return false;
        }

        // File handles of other processes, one per file object, whose owner can be opened.
        private static List<Candidate> CollectCandidates(IntPtr table, int currentId, int ignoreId, ushort fileTypeIndex, Dictionary<int, IntPtr> processHandles)
        {
            List<Candidate> candidates = new List<Candidate>();
            HashSet<long> seenObjects = new HashSet<long>();
            long count = EntryCount(table);

            for (long i = 0; i < count; i++)
            {
                IntPtr entry = EntryAt(table, i);

                if ((ushort)Marshal.ReadInt16(entry, 30) != fileTypeIndex)
                    continue;

                int pid = (int)Marshal.ReadInt64(entry, 8);

                if (pid == currentId || pid == ignoreId || pid <= 4)
                    continue;

                // The same file object can be reached through several handles.
                if (!seenObjects.Add(Marshal.ReadInt64(entry, 0)))
                    continue;

                IntPtr process;

                if (!processHandles.TryGetValue(pid, out process))
                {
                    process = OpenProcess(PROCESS_DUP_HANDLE, false, pid);
                    processHandles[pid] = process;
                }

                if (process == IntPtr.Zero)
                    continue;

                Candidate candidate;
                candidate.Pid = pid;
                candidate.Process = process;
                candidate.Handle = (IntPtr)Marshal.ReadInt64(entry, 16);
                candidates.Add(candidate);
            }

            return candidates;
        }

        // ---------------------------------------------------------------- scan

        // Returns the id of a process that has the device open, or 0.
        private static int RunScan(ScanState state)
        {
            if (state.Candidates.Count == 0)
                return 0;

            int threads = Math.Min(Math.Max(Environment.ProcessorCount, 2), MaxScanThreads);

            lock (state.Sync)
            {
                state.Active = threads;

                for (int i = 0; i < threads; i++)
                    StartScanThread(state);
            }

            // Watchdog: replaces threads stuck in a name query, until all work is done.
            while (true)
            {
                lock (state.Sync)
                {
                    if (state.Active <= 0)
                        break;
                }

                if (Volatile.Read(ref state.FoundPid) != 0 || DateTime.UtcNow > state.Deadline)
                    break;

                CheckForBlockedThreads(state);

                Thread.Sleep(10);
            }

            state.Stop = true;

            return Volatile.Read(ref state.FoundPid);
        }

        // Caller holds state.Sync or has not published the state to other threads yet.
        private static void StartScanThread(ScanState state)
        {
            ScanThread self = new ScanThread();
            state.Threads.Add(self);

            Thread thread = new Thread(() => ScanLoop(state, self));
            thread.IsBackground = true;
            thread.Start();
        }

        private static void CheckForBlockedThreads(ScanState state)
        {
            long now = Stopwatch.GetTimestamp();

            lock (state.Sync)
            {
                foreach (ScanThread thread in state.Threads.ToArray())
                {
                    long started = Volatile.Read(ref thread.QueryStart);

                    if (thread.Retired || started == 0 || (now - started) * 1000 / Stopwatch.Frequency <= NameQueryTimeoutMs)
                        continue;

                    thread.Retired = true;
                    state.Threads.Remove(thread);
                    state.TimedOut++;

                    // The replacement takes over the slot, or the slot closes if nothing is left.
                    if (Volatile.Read(ref state.Next) < state.Candidates.Count - 1 && DateTime.UtcNow < state.Deadline)
                        StartScanThread(state);
                    else
                        state.Active--;
                }
            }
        }

        private static void ScanLoop(ScanState state, ScanThread self)
        {
            IntPtr buffer = Marshal.AllocHGlobal(NameBufferSize);

            try
            {
                while (!self.Retired && !state.Stop && Volatile.Read(ref state.FoundPid) == 0)
                {
                    int index = Interlocked.Increment(ref state.Next);

                    if (index >= state.Candidates.Count)
                        break;

                    Candidate candidate = state.Candidates[index];
                    IntPtr duplicate;

                    if (!DuplicateHandle(candidate.Process, candidate.Handle, GetCurrentProcess(), out duplicate, 0, false, DUPLICATE_SAME_ACCESS))
                        continue;

                    Volatile.Write(ref self.QueryStart, Stopwatch.GetTimestamp());
                    string name = QueryName(duplicate, buffer);
                    Volatile.Write(ref self.QueryStart, 0);

                    CloseHandle(duplicate);
                    Interlocked.Increment(ref state.Checked);

                    if (self.Retired)
                        return;

                    if (name != null && string.Equals(name, DeviceName, StringComparison.OrdinalIgnoreCase))
                    {
                        Interlocked.CompareExchange(ref state.FoundPid, candidate.Pid, 0);
                        break;
                    }
                }
            }
            finally
            {
                // The buffer is only freed once the query is over, even for an abandoned thread.
                Marshal.FreeHGlobal(buffer);

                lock (state.Sync)
                {
                    if (!self.Retired)
                        state.Active--;
                }
            }
        }

        private static string QueryName(IntPtr handle, IntPtr buffer)
        {
            int returned;

            if (NtQueryObject(handle, ObjectNameInformation, buffer, NameBufferSize, out returned) < 0)
                return null;

            // UNICODE_STRING (64-bit): Length at 0, Buffer pointer at 8.
            int length = (ushort)Marshal.ReadInt16(buffer, 0);
            IntPtr text = Marshal.ReadIntPtr(buffer, 8);

            if (length == 0 || text == IntPtr.Zero)
                return null;

            return Marshal.PtrToStringUni(text, length / 2);
        }

        private static string ProcessName(int pid)
        {
            try
            {
                using (Process process = Process.GetProcessById(pid))
                    return process.ProcessName;
            }
            catch
            {
                return "another program";
            }
        }
    }
}
