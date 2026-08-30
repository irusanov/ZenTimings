using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace ZenTimings.DriverCleaner
{
    internal static class Notification
    {
        private const uint NIM_ADD = 0x00000000;
        private const uint NIM_DELETE = 0x00000002;
        private const uint NIM_SETVERSION = 0x00000004;

        private const uint NIF_ICON = 0x00000002;
        private const uint NIF_TIP = 0x00000004;
        private const uint NIF_INFO = 0x00000010;

        private const uint NIIF_INFO = 0x00000001;
        private const uint NIIF_WARNING = 0x00000002;

        private const uint NOTIFYICON_VERSION_4 = 4;

        // Stock Windows icons.
        private const int IDI_INFORMATION = 32516;
        private const int IDI_WARNING = 32515;

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

        [DllImport(
            "shell32.dll",
            CharSet = CharSet.Unicode,
            SetLastError = true)]
        private static extern bool Shell_NotifyIcon(
            uint dwMessage,
            ref NOTIFYICONDATA lpData);

        [DllImport(
            "user32.dll",
            CharSet = CharSet.Unicode,
            SetLastError = true)]
        private static extern IntPtr CreateWindowEx(
            uint dwExStyle,
            string lpClassName,
            string lpWindowName,
            uint dwStyle,
            int X,
            int Y,
            int nWidth,
            int nHeight,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam);

        [DllImport("user32.dll")]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr LoadIcon(
            IntPtr hInstance,
            IntPtr lpIconName);

        public static void Show(
            string title,
            string message,
            bool warning = false)
        {
            IntPtr hwnd = IntPtr.Zero;
            bool iconAdded = false;

            try
            {
                hwnd = CreateWindowEx(
                    0,
                    "STATIC",
                    "ZenTimings Driver Cleanup",
                    0,
                    0,
                    0,
                    0,
                    0,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero);

                if (hwnd == IntPtr.Zero)
                    return;

                NOTIFYICONDATA data = new NOTIFYICONDATA
                {
                    cbSize = (uint)Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                    hWnd = hwnd,
                    uID = 1,

                    uFlags = NIF_ICON | NIF_TIP | NIF_INFO,

                    hIcon = LoadIcon(
                        IntPtr.Zero,
                        new IntPtr(
                            warning
                                ? IDI_WARNING
                                : IDI_INFORMATION)),

                    szTip = "ZenTimings Driver Cleanup",

                    szInfoTitle = title,
                    szInfo = message,

                    dwInfoFlags = warning
                        ? NIIF_WARNING
                        : NIIF_INFO,

                    uVersion = NOTIFYICON_VERSION_4
                };

                // Add the notification-area icon and display
                // the balloon notification.
                if (!Shell_NotifyIcon(NIM_ADD, ref data))
                    return;

                iconAdded = true;

                // Tell Shell that we want the Windows Vista+
                // notification icon interface.
                Shell_NotifyIcon(
                    NIM_SETVERSION,
                    ref data);

                /*
                 * Windows Vista and later ignore uTimeout.
                 *
                 * The notification lifetime is controlled by
                 * Windows and the user's notification/accessibility
                 * settings.
                 *
                 * Keep the notification icon alive long enough for
                 * the notification to naturally disappear.
                 */
                Thread.Sleep(10000);
            }
            catch
            {
                // Notification failure must never affect
                // driver cleanup.
            }
            finally
            {
                if (iconAdded)
                {
                    NOTIFYICONDATA data = new NOTIFYICONDATA
                    {
                        cbSize = (uint)Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                        hWnd = hwnd,
                        uID = 1
                    };

                    Shell_NotifyIcon(
                        NIM_DELETE,
                        ref data);
                }

                if (hwnd != IntPtr.Zero)
                    DestroyWindow(hwnd);
            }
        }
    }
}