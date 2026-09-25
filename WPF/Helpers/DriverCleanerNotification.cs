using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ZenTimings.Helpers
{
    internal static class DriverCleanerNotification
    {
        private const uint NIM_ADD = 0x00000000;
        private const uint NIM_DELETE = 0x00000002;
        private const uint NIM_SETVERSION = 0x00000004;

        private const uint NIF_MESSAGE = 0x00000001;
        private const uint NIF_ICON = 0x00000002;
        private const uint NIF_TIP = 0x00000004;
        private const uint NIF_INFO = 0x00000010;

        private const uint NIIF_INFO = 0x00000001;
        private const uint NIIF_WARNING = 0x00000002;
        private const uint NIIF_USER = 0x00000004;
        private const uint NIIF_LARGE_ICON = 0x00000020;

        private const uint NOTIFYICON_VERSION_4 = 4;

        private const uint WM_APP = 0x8000;
        private const uint CallbackMessage = WM_APP + 1;
        private const uint NIN_BALLOONSHOW = 0x0402;
        private const uint NIN_BALLOONHIDE = 0x0403;
        private const uint NIN_BALLOONTIMEOUT = 0x0404;
        private const uint NIN_BALLOONUSERCLICK = 0x0405;

        private const uint PM_REMOVE = 0x0001;
        private const uint QS_ALLINPUT = 0x04FF;
        private const int BalloonTimeoutMs = 7000;
        private const int BalloonVisibleMs = 6000;

        [StructLayout(LayoutKind.Sequential)]
        private struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public int ptX;
            public int ptY;
        }

        [DllImport("user32.dll")]
        private static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

        [DllImport("user32.dll")]
        private static extern bool TranslateMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        private static extern IntPtr DispatchMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        private static extern uint MsgWaitForMultipleObjects(uint nCount, IntPtr[] pHandles, bool bWaitAll, uint dwMilliseconds, uint dwWakeMask);

        private const uint MSGFLT_ALLOW = 1;
        private const string WindowClassName = "ZenTimingsDriverCleanupNotification";

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WNDCLASSEX
        {
            public uint cbSize;
            public uint style;
            public IntPtr lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            public string lpszMenuName;
            public string lpszClassName;
            public IntPtr hIconSm;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

        [DllImport("user32.dll")]
        private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool ChangeWindowMessageFilterEx(IntPtr hWnd, uint message, uint action, IntPtr pChangeFilterStruct);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private static readonly WndProcDelegate wndProc = WindowProc;
        private static bool classRegistered;
        private static bool balloonClosed;
        private static int balloonDeadline;

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

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
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
        private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern uint PrivateExtractIcons(string szFileName, int nIconIndex, int cxIcon, int cyIcon,
            [Out] IntPtr[] phicon, IntPtr piconid, uint nIcons, uint flags);

        public static void Show(string title, string message, bool warning = false)
        {
            IntPtr hwnd = IntPtr.Zero;
            IntPtr smallIcon = IntPtr.Zero;
            IntPtr largeIcon = IntPtr.Zero;
            bool iconAdded = false;

            try
            {
                bool hasIcons = TryLoadIcons(out smallIcon, out largeIcon);
                bool useAppIcon = hasIcons && !warning;

                hwnd = CreateWindowEx(
                    0,
                    EnsureWindowClass() ? WindowClassName : "STATIC",
                    "ZenTimings Driver Cleanup",
                    0,
                    0,
                    0,
                    0,
                    0,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    GetModuleHandle(null),
                    IntPtr.Zero);

                if (hwnd == IntPtr.Zero)
                    return;

                // Explorer runs non-elevated
                ChangeWindowMessageFilterEx(hwnd, CallbackMessage, MSGFLT_ALLOW, IntPtr.Zero);

                NOTIFYICONDATA data = new NOTIFYICONDATA
                {
                    cbSize = (uint)Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                    hWnd = hwnd,
                    uID = 1,
                    uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_INFO,
                    uCallbackMessage = CallbackMessage,
                    hIcon = hasIcons
                        ? smallIcon
                        : LoadIcon(IntPtr.Zero, new IntPtr(warning ? IDI_WARNING : IDI_INFORMATION)),
                    szTip = "ZenTimings Driver Cleanup",
                    szInfoTitle = title,
                    szInfo = message,
                    dwInfoFlags = warning
                        ? NIIF_WARNING
                        : useAppIcon ? NIIF_USER | NIIF_LARGE_ICON : NIIF_INFO,
                    uVersion = NOTIFYICON_VERSION_4,
                    hBalloonIcon = useAppIcon ? largeIcon : IntPtr.Zero
                };

                // Add the notification-area icon and display the balloon notification.
                if (!Shell_NotifyIcon(NIM_ADD, ref data))
                {
                    if (!hasIcons)
                        return;

                    data.hIcon = LoadIcon(IntPtr.Zero, new IntPtr(warning ? IDI_WARNING : IDI_INFORMATION));
                    data.hBalloonIcon = IntPtr.Zero;
                    data.dwInfoFlags = warning ? NIIF_WARNING : NIIF_INFO;

                    if (!Shell_NotifyIcon(NIM_ADD, ref data))
                        return;
                }

                iconAdded = true;

                // Tell Shell that we want the Windows Vista+ notification icon interface.
                Shell_NotifyIcon(NIM_SETVERSION, ref data);

                // Keep the icon (and process) alive until the balloon closes so Shell can attribute it to this app.
                WaitForBalloon(hwnd);
            }
            catch
            {
                // Notification failure must never affect driver cleanup.
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

                    Shell_NotifyIcon(NIM_DELETE, ref data);
                }

                if (hwnd != IntPtr.Zero)
                    DestroyWindow(hwnd);

                if (smallIcon != IntPtr.Zero)
                    DestroyIcon(smallIcon);

                if (largeIcon != IntPtr.Zero)
                    DestroyIcon(largeIcon);
            }
        }

        private static void WaitForBalloon(IntPtr hwnd)
        {
            balloonClosed = false;
            balloonDeadline = Environment.TickCount + BalloonTimeoutMs;

            while (!balloonClosed)
            {
                int remaining = balloonDeadline - Environment.TickCount;
                if (remaining <= 0)
                    return;

                MsgWaitForMultipleObjects(0, null, false, (uint)remaining, QS_ALLINPUT);

                while (PeekMessage(out MSG msg, IntPtr.Zero, 0, 0, PM_REMOVE))
                {
                    TranslateMessage(ref msg);
                    DispatchMessage(ref msg);
                }
            }
        }

        private static IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == CallbackMessage)
            {
                uint evt = (uint)(lParam.ToInt64() & 0xFFFF);

                if (evt == NIN_BALLOONHIDE || evt == NIN_BALLOONTIMEOUT || evt == NIN_BALLOONUSERCLICK)
                    balloonClosed = true;
                else if (evt == NIN_BALLOONSHOW)
                    balloonDeadline = Environment.TickCount + BalloonVisibleMs;

                return IntPtr.Zero;
            }

            return DefWindowProc(hWnd, msg, wParam, lParam);
        }

        private static bool EnsureWindowClass()
        {
            if (classRegistered)
                return true;

            WNDCLASSEX wc = new WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf(typeof(WNDCLASSEX)),
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(wndProc),
                hInstance = GetModuleHandle(null),
                lpszClassName = WindowClassName
            };

            classRegistered = RegisterClassEx(ref wc) != 0;
            return classRegistered;
        }

        private static bool TryLoadIcons(out IntPtr smallIcon, out IntPtr largeIcon)
        {
            string exePath = ExecutablePath();
            smallIcon = LoadIconFromFile(exePath, 16);
            largeIcon = LoadIconFromFile(exePath, 256);

            return smallIcon != IntPtr.Zero && largeIcon != IntPtr.Zero;
        }

        private static IntPtr LoadIconFromFile(string path, int size)
        {
            if (string.IsNullOrEmpty(path))
                return IntPtr.Zero;

            IntPtr[] icons = new IntPtr[1];
            PrivateExtractIcons(path, 0, size, size, icons, IntPtr.Zero, 1, 0);
            return icons[0];
        }

        private static string ExecutablePath()
        {
            return Assembly.GetEntryAssembly()?.Location;
        }
    }
}
