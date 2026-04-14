using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows;
using System.Windows.Media;

namespace ZenTimings
{
    internal static class WindowUtils
    {
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
            DwmSetWindowAttribute(
                hwnd,
                DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE,
                ref preference,
                Marshal.SizeOf(typeof(int)));
        }

        public static void SetCornerPreference(System.Windows.Window window, int preference = 0)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
                return;

            if (preference < 0 && preference > 3)
            {
                return;
            }

            DWM_WINDOW_CORNER_PREFERENCE cornerPreference = (DWM_WINDOW_CORNER_PREFERENCE)preference;

            DwmSetWindowAttribute(
                hwnd,
                DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE,
                ref cornerPreference,
                Marshal.SizeOf(typeof(int)));
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
