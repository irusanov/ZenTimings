using System;
using System.Runtime.InteropServices;

namespace ZenTimings
{
    // this class just wraps some Win32 stuff that we're going to use
    public static class InteropMethods
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

        [DllImport("inpout32.dll", EntryPoint = "GetPhysLong", CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetPhysLong32(UIntPtr memAddress, out uint data);

        [DllImport("inpoutx64.dll", EntryPoint = "GetPhysLong", CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetPhysLong64(UIntPtr memAddress, out uint data);

        public static bool GetPhysLong(UIntPtr memAddress, out uint data)
        {
            if (Environment.Is64BitProcess)
                return GetPhysLong64(memAddress, out data);

            return GetPhysLong32(memAddress, out data);
        }

        [DllImport("inpout32.dll", EntryPoint = "IsInpOutDriverOpen", CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.U4)]
        public static extern uint IsInpOutDriverOpen32();

        [DllImport("inpoutx64.dll", EntryPoint = "IsInpOutDriverOpen", CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.U4)]
        public static extern uint IsInpOutDriverOpen64();

        public static bool IsInpOutDriverOpen()
        {
            if (Environment.Is64BitProcess)
                return IsInpOutDriverOpen64() != 0;

            return IsInpOutDriverOpen32() != 0;
        }

    }
}
