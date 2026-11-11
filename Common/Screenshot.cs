using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ZenTimings.Common
{
    public class Screenshot : IDisposable
    {
        // GDI stuff for window screenshot without shadows
        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hdcDest, int nxDest, int nyDest, int nWidth, int nHeight,
            IntPtr hdcSrc, int nXSrc, int nYSrc, int dwRop);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int width, int nHeight);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr DeleteObject(IntPtr hObject);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ReleaseDC(IntPtr hWnd, IntPtr hDc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hObject);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute,
            int cbAttribute);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [StructLayout(LayoutKind.Sequential)]
        private readonly struct RECT
        {
            public readonly int left;
            public readonly int top;
            public readonly int right;
            public readonly int bottom;
        }

        private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
        private const int SRCCOPY = 0x00CC0020;
        private const int CAPTUREBLT = 0x40000000;
        private bool disposedValue;

        private Bitmap CaptureRegion(Rectangle region)
        {
            Bitmap result;

            IntPtr desktophWnd = GetDesktopWindow();
            IntPtr desktopDc = GetWindowDC(desktophWnd);
            IntPtr memoryDc = CreateCompatibleDC(desktopDc);
            IntPtr bitmap = CreateCompatibleBitmap(desktopDc, region.Width, region.Height);
            IntPtr oldBitmap = SelectObject(memoryDc, bitmap);

            var success = BitBlt(memoryDc, 0, 0, region.Width, region.Height, desktopDc, region.Left, region.Top,
                SRCCOPY | CAPTUREBLT);

            try
            {
                if (!success) throw new Win32Exception();

                result = Image.FromHbitmap(bitmap);
            }
            finally
            {
                SelectObject(memoryDc, oldBitmap);
                DeleteObject(bitmap);
                DeleteDC(memoryDc);
                ReleaseDC(desktophWnd, desktopDc);
            }

            return result;
        }

        private Bitmap CaptureWindow(IntPtr hWnd)
        {
            return CaptureWindow(hWnd, 0);
        }

        /// <summary>
        /// Captures a window from the screen. With a corner radius (in physical pixels) the pixels outside the
        /// window's rounded corners are made transparent, so the desktop or windows behind those corners don't
        /// show in the image. The color of those pixels is kept; only their alpha changes.
        /// </summary>
        public Bitmap CaptureWindow(IntPtr hWnd, double cornerRadius)
        {
            RECT region;

            if (Environment.OSVersion.Version.Major < 6)
            {
                GetWindowRect(hWnd, out region);
            }
            else
            {
                if (DwmGetWindowAttribute(hWnd, DWMWA_EXTENDED_FRAME_BOUNDS, out region,
                    Marshal.SizeOf(typeof(RECT))) != 0) GetWindowRect(hWnd, out region);
            }

            Bitmap capture = CaptureRegion(Rectangle.FromLTRB(region.left, region.top, region.right, region.bottom));
            if (cornerRadius < 0.5)
                return capture;

            try
            {
                return ClipCorners(capture, cornerRadius);
            }
            finally
            {
                capture.Dispose();
            }
        }

        // Supersampling per axis for the corner coverage, so the clipped edge is antialiased like DWM's.
        private const int CornerSamples = 4;

        private static Bitmap ClipCorners(Bitmap source, double radius)
        {
            int width = source.Width;
            int height = source.Height;
            radius = Math.Min(radius, Math.Min(width, height) / 2.0);

            // The capture has no alpha channel (32bppRgb); drawing it into an ARGB bitmap makes it opaque.
            var result = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(result))
            {
                g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                g.DrawImage(source, new Rectangle(0, 0, width, height));
            }

            int size = (int)Math.Ceiling(radius);
            if (size <= 0)
                return result;

            // Coverage of the top-left corner; the other three corners are its mirror images.
            var coverage = new byte[size, size];
            const int samples = CornerSamples * CornerSamples;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int inside = 0;
                    for (int sy = 0; sy < CornerSamples; sy++)
                    {
                        double py = y + (sy + 0.5) / CornerSamples;
                        for (int sx = 0; sx < CornerSamples; sx++)
                        {
                            double px = x + (sx + 0.5) / CornerSamples;
                            double dx = radius - px;
                            double dy = radius - py;
                            if (px >= radius || py >= radius || dx * dx + dy * dy <= radius * radius)
                                inside++;
                        }
                    }

                    coverage[x, y] = (byte)(inside * 255 / samples);
                }
            }

            BitmapData data = result.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadWrite,
                PixelFormat.Format32bppArgb);
            try
            {
                int stride = data.Stride;
                var pixels = new byte[stride * height];
                Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        byte alpha = coverage[x, y];
                        if (alpha == 255)
                            continue;

                        // Byte order in memory is B, G, R, A.
                        pixels[y * stride + x * 4 + 3] = alpha;
                        pixels[y * stride + (width - 1 - x) * 4 + 3] = alpha;
                        pixels[(height - 1 - y) * stride + x * 4 + 3] = alpha;
                        pixels[(height - 1 - y) * stride + (width - 1 - x) * 4 + 3] = alpha;
                    }
                }

                Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
            }
            finally
            {
                result.UnlockBits(data);
            }

            return result;
        }

        public Bitmap CaptureActiveWindow()
        {
            return CaptureWindow(GetForegroundWindow());
        }

        public Bitmap CaptureDekstop()
        {
            return CaptureWindow(GetDesktopWindow());
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~Screenshot()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}