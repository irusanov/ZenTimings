using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ZenTimings.Utils
{
    internal static class VisualCapture
    {
        /// <summary>Renders a laid-out element to a bitmap at the screen's DPI.</summary>
        public static BitmapSource Render(FrameworkElement element)
        {
            var dpi = VisualTreeHelper.GetDpi(element);
            var bitmap = new RenderTargetBitmap(
                (int)Math.Ceiling(element.ActualWidth * dpi.DpiScaleX),
                (int)Math.Ceiling(element.ActualHeight * dpi.DpiScaleY),
                dpi.PixelsPerInchX, dpi.PixelsPerInchY,
                PixelFormats.Pbgra32);

            bitmap.Render(element);
            bitmap.Freeze();
            return bitmap;
        }
    }
}
