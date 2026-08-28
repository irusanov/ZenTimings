using System;
using System.Runtime.InteropServices;

namespace ZenTimings.DriverCleaner
{

    internal static class Notification
    {
        private const uint NIM_ADD = 0x00000000;
        private const uint NIM_DELETE = 0x00000002;
        private const uint NIF_MESSAGE = 0x00000001;
        private const uint NIF_ICON = 0x00000002;
        private const uint NIF_INFO = 0x00000010;

        private const uint NIIF_INFO = 0x00000001;
        private const uint NIIF_WARNING = 0x00000002;
        private const uint NIIF_ERROR = 0x00000003;

        private const int WM_USER = 0x0400;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NOTIFYICONDATA
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;

            public uint dwState;
            public uint dwStateMask;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;

            public uint uVersion;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;

            public uint dwInfoFlags;

            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

        [DllImport("user32.dll")]
        private static extern IntPtr CreateWindowEx(uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle, int X, int Y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll")]
        private static extern bool DestroyWindow(IntPtr hWnd);

        public static void Show(string title, string message, bool warning = false)
        {
            IntPtr hwnd = IntPtr.Zero;

            try
            {
                hwnd = CreateWindowEx(0, "STATIC", "ZenTimings Driver Cleanup", 0, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

                if (hwnd == IntPtr.Zero)
                    return;

                NOTIFYICONDATA data = new NOTIFYICONDATA();

                data.cbSize = (uint)Marshal.SizeOf(typeof(NOTIFYICONDATA));

                data.hWnd = hwnd;
                data.uID = 1;
                data.uFlags = NIF_INFO;

                data.szInfoTitle = title;
                data.szInfo = message;

                data.dwInfoFlags = warning ? NIIF_WARNING : NIIF_INFO;

                if (!Shell_NotifyIcon(NIM_ADD, ref data))
                    return;

                /*
                 * Keep the tray icon alive long enough for Windows
                 * to display the notification.
                 */
                System.Threading.Thread.Sleep(5000);

                Shell_NotifyIcon(NIM_DELETE, ref data);
            }
            catch
            {
            }
            finally
            {
                if (hwnd != IntPtr.Zero)
                    DestroyWindow(hwnd);
            }
        }
    }
}