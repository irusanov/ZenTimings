using System;
using System.Runtime.InteropServices;

namespace ZenTimings
{
    // this class just wraps some Win32 stuff that we're going to use
    internal static class NativeMethods
    {
        public const int HWND_BROADCAST = 0xffff;
        public static readonly int WM_SHOWME = RegisterWindowMessage("WM_SHOWME");

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern int RegisterWindowMessage(string lpString);

        [DllImport("psapi.dll")]
        public static extern int EmptyWorkingSet(IntPtr hwProc);

        [DllImport("inpoutx64.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetPhysLong(UIntPtr memAddress, out uint data);

        [DllImport("inpoutx64.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsInpOutDriverOpen();
    }
}
