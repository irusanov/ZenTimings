using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ZenTimings.Utils
{
    internal static class WindowUtils
    {
        // DWMWA_WINDOW_CORNER_PREFERENCE (33) and DWMWA_BORDER_COLOR (34) exist from Windows 11 (build 22000).
        private const int Windows11Build = 22000;
        private static readonly Lazy<int> osBuild = new Lazy<int>(GetOsBuild);

        // DWM's corner radius on Windows 11, in pixels at 96 DPI: DWMWCP_ROUND and DWMWCP_ROUNDSMALL.
        private const double RoundRadius = 8;
        private const double RoundSmallRadius = 4;

        // The corner preference last set on each window. DWM has no documented getter for it, and a window
        // without an entry has the default (rounded).
        private static readonly ConditionalWeakTable<Window, StrongBox<int>> cornerPreferences =
            new ConditionalWeakTable<Window, StrongBox<int>>();

        private static void RememberCornerPreference(Window window, int preference)
        {
            cornerPreferences.GetValue(window, _ => new StrongBox<int>()).Value = preference;
        }

        /// <summary>
        /// The radius, in physical pixels, that DWM rounds the window's corners with; 0 when they are square
        /// (before Windows 11, maximized, layered, or rounding turned off).
        /// </summary>
        public static double GetCornerRadiusPixels(Window window)
        {
            if (window == null || !SupportsCornerPreference || window.WindowState == WindowState.Maximized ||
                window.AllowsTransparency)
                return 0;

            int preference = cornerPreferences.TryGetValue(window, out StrongBox<int> stored)
                ? stored.Value
                : (int)DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_DEFAULT;

            double radius;
            switch ((DWM_WINDOW_CORNER_PREFERENCE)preference)
            {
                case DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_DONOTROUND:
                    return 0;
                case DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUNDSMALL:
                    radius = RoundSmallRadius;
                    break;
                default:
                    radius = RoundRadius;
                    break;
            }

            return radius * VisualTreeHelper.GetDpi(window).DpiScaleX;
        }

        /// <summary>
        /// True when the OS supports DWM rounded corners and border colors (Windows 11 or newer).
        /// </summary>
        public static bool SupportsCornerPreference => osBuild.Value >= Windows11Build;

        // Environment.OSVersion is capped at 6.2.9200 unless the app manifest declares Windows 10 support,
        // so read the real build number from ntdll.
        private static int GetOsBuild()
        {
            try
            {
                var info = new OSVERSIONINFOEX { dwOSVersionInfoSize = Marshal.SizeOf(typeof(OSVERSIONINFOEX)) };
                if (RtlGetVersion(ref info) == 0)
                    return info.dwBuildNumber;
            }
            catch
            {
                // Fall through to the managed value.
            }

            return Environment.OSVersion.Version.Build;
        }

        // Workaround for Windows 11 system border overriding application window border
        public static void RemoveSystemBorderAndRadius(System.Windows.Window window)
        {
            var hwnd = new WindowInteropHelper(window).Handle;

            if (hwnd == IntPtr.Zero)
                return;

            uint none = DWMWA_COLOR_NONE; // DWMWA_COLOR_NONE
            DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref none, sizeof(int));
            RemoveBorderRadius(window);
        }

        public static void RemoveBorderRadius(System.Windows.Window window)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
                return;
            var preference = DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_DONOTROUND;
            if (DwmSetWindowAttribute(
                    hwnd,
                    DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE,
                    ref preference,
                    Marshal.SizeOf(typeof(int))) == 0)
                RememberCornerPreference(window, (int)preference);
        }

        public static void SetCornerPreference(System.Windows.Window window, int preference = 0)
        {
            if (!SupportsCornerPreference)
                return;

            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
                return;

            if (preference < 0 || preference > 3)
            {
                return;
            }

            DWM_WINDOW_CORNER_PREFERENCE cornerPreference = (DWM_WINDOW_CORNER_PREFERENCE)preference;

            if (DwmSetWindowAttribute(
                    hwnd,
                    DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE,
                    ref cornerPreference,
                    Marshal.SizeOf(typeof(int))) == 0)
                RememberCornerPreference(window, preference);
        }

        public static void SetBorderColor(Window window, uint borderColor)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
                return;

            DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref borderColor, sizeof(uint));
        }

        public static bool TrySetBorderColor(Window window, SolidColorBrush brush)
        {
            if (window == null)
                return false;

            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
                return false;

            uint colorRef = WindowUtils.ToColorRef(
                brush.Color.R,
                brush.Color.G,
                brush.Color.B);

            int hr = DwmSetWindowAttribute(
                hwnd,
                DWMWA_BORDER_COLOR,
                ref colorRef,
                sizeof(uint));

            return hr == 0;
        }

        public static uint ToColorRef(byte r, byte g, byte b) => (uint)(r | (g << 8) | (b << 16));

        // Renders the main window's visual tree (not the screen), so windows covering it do not matter.
        // Returns null when the window is hidden (e.g. minimized to the tray) or cannot be rendered.
        internal static BitmapSource CaptureMainWindow()
        {
            try
            {
                Window main = Application.Current?.MainWindow;
                if (main == null || !main.IsVisible || main.WindowState == WindowState.Minimized)
                    return null;

                var root = VisualTreeHelper.GetChildrenCount(main) > 0
                    ? VisualTreeHelper.GetChild(main, 0) as FrameworkElement
                    : null;
                FrameworkElement target = root ?? main;
                if (target.ActualWidth < 1 || target.ActualHeight < 1)
                    return null;

                return VisualCapture.Render(target);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Main window capture failed: {ex.Message}");
                return null;
            }
        }

        private const int DWMWA_BORDER_COLOR = 34;
        private const uint DWMWA_COLOR_DEFAULT = 0xFFFFFFFF;
        private const uint DWMWA_COLOR_NONE = 0xFFFFFFFE;

        private enum DWMWINDOWATTRIBUTE
        {
            DWMWA_WINDOW_CORNER_PREFERENCE = 33
        }
        private enum DWM_WINDOW_CORNER_PREFERENCE
        {
            DWMWCP_DEFAULT = 0,
            DWMWCP_DONOTROUND = 1,
            DWMWCP_ROUND = 2,
            DWMWCP_ROUNDSMALL = 3
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct OSVERSIONINFOEX
        {
            public int dwOSVersionInfoSize;
            public int dwMajorVersion;
            public int dwMinorVersion;
            public int dwBuildNumber;
            public int dwPlatformId;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szCSDVersion;
            public ushort wServicePackMajor;
            public ushort wServicePackMinor;
            public ushort wSuiteMask;
            public byte wProductType;
            public byte wReserved;
        }

        [DllImport("ntdll.dll")]
        private static extern int RtlGetVersion(ref OSVERSIONINFOEX versionInfo);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(
            IntPtr hwnd,
            int dwAttribute,
            ref uint pvAttribute,
            int cbAttribute);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(
            IntPtr hwnd,
            DWMWINDOWATTRIBUTE dwAttribute,
            ref DWM_WINDOW_CORNER_PREFERENCE pvAttribute,
            int cbAttribute);
    }
}
