using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ZenTimings.Theming
{
    /// <summary>
    /// A window that draws its own title bar and border (see Themes/Generic.xaml), so they follow the theme.
    /// The native frame is replaced through <see cref="System.Windows.Shell.WindowChrome"/>, which keeps the
    /// native resizing, snapping and shadow.
    /// </summary>
    [TemplatePart(Name = PartTitleBar, Type = typeof(FrameworkElement))]
    [TemplatePart(Name = PartIcon, Type = typeof(FrameworkElement))]
    [TemplatePart(Name = PartMinimizeButton, Type = typeof(Button))]
    [TemplatePart(Name = PartMaximizeRestoreButton, Type = typeof(Button))]
    [TemplatePart(Name = PartCloseButton, Type = typeof(Button))]
    public class ChromeWindow : Window
    {
        private const string PartTitleBar = "PART_TitleBar";
        private const string PartIcon = "PART_Icon";
        private const string PartMinimizeButton = "PART_MinimizeButton";
        private const string PartMaximizeRestoreButton = "PART_MaximizeRestoreButton";
        private const string PartCloseButton = "PART_CloseButton";

        private const int WM_SYSCOMMAND = 0x0112;
        private const int SC_MAXIMIZE = 0xF030;

        private FrameworkElement titleBar;
        private FrameworkElement iconPresenter;
        private Button minimizeButton;
        private Button maximizeRestoreButton;
        private Button closeButton;
        private bool restoreOnDrag;

        public static readonly DependencyProperty TitleBarBackgroundProperty = DependencyProperty.Register(
            nameof(TitleBarBackground), typeof(Brush), typeof(ChromeWindow), new PropertyMetadata(null));

        public static readonly DependencyProperty TitleBarForegroundProperty = DependencyProperty.Register(
            nameof(TitleBarForeground), typeof(Brush), typeof(ChromeWindow), new PropertyMetadata(null));

        public static readonly DependencyProperty WindowButtonHighlightBrushProperty = DependencyProperty.Register(
            nameof(WindowButtonHighlightBrush), typeof(Brush), typeof(ChromeWindow), new PropertyMetadata(null));

        public static readonly DependencyProperty IconVisibilityProperty = DependencyProperty.Register(
            nameof(IconVisibility), typeof(Visibility), typeof(ChromeWindow), new PropertyMetadata(Visibility.Visible));

        public static readonly DependencyProperty TitleVisibilityProperty = DependencyProperty.Register(
            nameof(TitleVisibility), typeof(Visibility), typeof(ChromeWindow), new PropertyMetadata(Visibility.Visible));

        public static readonly DependencyProperty IconSourceProperty = DependencyProperty.Register(
            nameof(IconSource), typeof(ImageSource), typeof(ChromeWindow), new PropertyMetadata(null));

        public static readonly DependencyProperty TitleBarContentProperty = DependencyProperty.Register(
            nameof(TitleBarContent), typeof(object), typeof(ChromeWindow), new PropertyMetadata(null));

        private static readonly DependencyPropertyKey MaximizeBorderThicknessPropertyKey = DependencyProperty.RegisterReadOnly(
            nameof(MaximizeBorderThickness), typeof(Thickness), typeof(ChromeWindow), new PropertyMetadata(new Thickness()));

        public static readonly DependencyProperty MaximizeBorderThicknessProperty = MaximizeBorderThicknessPropertyKey.DependencyProperty;

        static ChromeWindow()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ChromeWindow), new FrameworkPropertyMetadata(typeof(ChromeWindow)));
            IconProperty.OverrideMetadata(typeof(ChromeWindow), new FrameworkPropertyMetadata(OnIconChanged));
        }

        public ChromeWindow()
        {
            IconSource = ApplicationIcon;
            SetValue(MaximizeBorderThicknessPropertyKey, GetMaximizeBorderThickness());
        }

        public Brush TitleBarBackground
        {
            get => (Brush)GetValue(TitleBarBackgroundProperty);
            set => SetValue(TitleBarBackgroundProperty, value);
        }

        public Brush TitleBarForeground
        {
            get => (Brush)GetValue(TitleBarForegroundProperty);
            set => SetValue(TitleBarForegroundProperty, value);
        }

        /// <summary>Background of the minimize and maximize buttons while hovered.</summary>
        public Brush WindowButtonHighlightBrush
        {
            get => (Brush)GetValue(WindowButtonHighlightBrushProperty);
            set => SetValue(WindowButtonHighlightBrushProperty, value);
        }

        public Visibility IconVisibility
        {
            get => (Visibility)GetValue(IconVisibilityProperty);
            set => SetValue(IconVisibilityProperty, value);
        }

        public Visibility TitleVisibility
        {
            get => (Visibility)GetValue(TitleVisibilityProperty);
            set => SetValue(TitleVisibilityProperty, value);
        }

        /// <summary>The icon shown in the title bar. Follows <see cref="Window.Icon"/>, and defaults to the application icon.</summary>
        public ImageSource IconSource
        {
            get => (ImageSource)GetValue(IconSourceProperty);
            set => SetValue(IconSourceProperty, value);
        }

        /// <summary>Optional content shown in the title bar, between the title and the window buttons.</summary>
        public object TitleBarContent
        {
            get => GetValue(TitleBarContentProperty);
            set => SetValue(TitleBarContentProperty, value);
        }

        /// <summary>
        /// A maximized window extends past the screen by its (invisible) resize border; the template pads the
        /// content by this much so nothing is cut off.
        /// </summary>
        public Thickness MaximizeBorderThickness => (Thickness)GetValue(MaximizeBorderThicknessProperty);

        private bool CanResize => ResizeMode == ResizeMode.CanResize || ResizeMode == ResizeMode.CanResizeWithGrip;

        private static void OnIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is ChromeWindow window))
                return;

            if (e.NewValue is ImageSource image)
            {
                window.IconSource = image;
                return;
            }

            string icon = e.NewValue?.ToString();
            window.IconSource = string.IsNullOrEmpty(icon) ? null : new BitmapImage(new Uri(icon));
        }

        private static ImageSource applicationIcon;

        private static ImageSource ApplicationIcon
        {
            get
            {
                if (applicationIcon != null)
                    return applicationIcon;

                try
                {
                    string path = Process.GetCurrentProcess().MainModule?.FileName;
                    if (path == null || !File.Exists(path))
                        return null;

                    using (System.Drawing.Icon icon = System.Drawing.Icon.ExtractAssociatedIcon(path))
                    {
                        if (icon == null)
                            return null;

                        BitmapSource source = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                        source.Freeze();
                        applicationIcon = source;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"ChromeWindow: could not load the application icon: {ex.Message}");
                }

                return applicationIcon;
            }
        }

        private static Thickness GetMaximizeBorderThickness()
        {
            Thickness frame = SystemParameters.WindowNonClientFrameThickness;
            Thickness resize = SystemParameters.WindowResizeBorderThickness;

            return new Thickness(
                frame.Left + resize.Left - 1,
                frame.Top + resize.Top - SystemParameters.CaptionHeight - 1,
                frame.Right + resize.Right - 1,
                frame.Bottom + resize.Bottom - 1);
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (titleBar != null)
            {
                titleBar.MouseLeftButtonDown -= TitleBar_MouseLeftButtonDown;
                titleBar.MouseLeftButtonUp -= TitleBar_MouseLeftButtonUp;
                titleBar.MouseMove -= TitleBar_MouseMove;
                titleBar.MouseRightButtonUp -= TitleBar_MouseRightButtonUp;
            }
            if (iconPresenter != null)
                iconPresenter.MouseLeftButtonDown -= Icon_MouseLeftButtonDown;
            if (minimizeButton != null)
                minimizeButton.Click -= MinimizeButton_Click;
            if (maximizeRestoreButton != null)
                maximizeRestoreButton.Click -= MaximizeRestoreButton_Click;
            if (closeButton != null)
                closeButton.Click -= CloseButton_Click;

            titleBar = GetTemplateChild(PartTitleBar) as FrameworkElement;
            iconPresenter = GetTemplateChild(PartIcon) as FrameworkElement;
            minimizeButton = GetTemplateChild(PartMinimizeButton) as Button;
            maximizeRestoreButton = GetTemplateChild(PartMaximizeRestoreButton) as Button;
            closeButton = GetTemplateChild(PartCloseButton) as Button;

            if (titleBar != null)
            {
                titleBar.MouseLeftButtonDown += TitleBar_MouseLeftButtonDown;
                titleBar.MouseLeftButtonUp += TitleBar_MouseLeftButtonUp;
                titleBar.MouseMove += TitleBar_MouseMove;
                titleBar.MouseRightButtonUp += TitleBar_MouseRightButtonUp;
            }
            if (iconPresenter != null)
                iconPresenter.MouseLeftButtonDown += Icon_MouseLeftButtonDown;
            if (minimizeButton != null)
                minimizeButton.Click += MinimizeButton_Click;
            if (maximizeRestoreButton != null)
                maximizeRestoreButton.Click += MaximizeRestoreButton_Click;
            if (closeButton != null)
                closeButton.Click += CloseButton_Click;
        }

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);

            // WPF draws the window from now on.
            firstFrameRendered = true;

            if (sizeRefreshed)
                return;

            sizeRefreshed = true;
            RefreshSizeToContent();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            if (PresentationSource.FromVisual(this) is HwndSource source)
            {
                source.AddHook(ChromeWndProc);
                PrepareFirstFrame(source);
            }
        }

        /// <summary>
        /// Windows shows (and animates) a new window before WPF has drawn anything into it, so for a moment it
        /// would appear as a white window with a light border. Give the native window the theme colors instead:
        /// its background is painted in the theme background until WPF's first frame (see WM_ERASEBKGND in
        /// <see cref="ChromeWndProc"/>), and the DWM border and dark mode follow the theme.
        /// </summary>
        private void PrepareFirstFrame(HwndSource source)
        {
            if (AllowsTransparency)
                return;

            Color? background = GetThemeBackgroundColor();
            if (background == null)
                return;

            Color color = background.Value;
            eraseColor = ToColorRef(color);

            if (source.CompositionTarget != null)
                source.CompositionTarget.BackgroundColor = color;

            IntPtr hwnd = source.Handle;
            bool dark = 0.299 * color.R + 0.587 * color.G + 0.114 * color.B < 128;
            int darkValue = dark ? 1 : 0;
            // Windows 10 20H1 and newer use 20, older Windows 10 builds 19; both fail harmlessly elsewhere.
            if (SetDwmAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, darkValue) != 0)
                SetDwmAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, darkValue);

            // Windows 11 draws a border around the window; ThemedWindow keeps it in sync with the theme later on.
            if (BorderBrush is SolidColorBrush border && border.Color.A > 0)
                SetDwmAttribute(hwnd, DWMWA_BORDER_COLOR, ToColorRef(border.Color));
        }

        private Color? GetThemeBackgroundColor()
        {
            Color? color = ToOpaqueColor(Background);
            return color ?? ToOpaqueColor(TryFindResource(ThemeBrushes.BackgroundBrush) as Brush);
        }

        private static Color? ToOpaqueColor(Brush brush)
        {
            if (brush is SolidColorBrush solid)
                return solid.Color.A > 0 ? Color.FromRgb(solid.Color.R, solid.Color.G, solid.Color.B) : (Color?)null;

            if (brush is GradientBrush gradient && gradient.GradientStops.Count > 0)
            {
                // The average of the stops is close enough for the moment before the first frame.
                int r = 0, g = 0, b = 0;
                foreach (GradientStop stop in gradient.GradientStops)
                {
                    r += stop.Color.R;
                    g += stop.Color.G;
                    b += stop.Color.B;
                }
                int count = gradient.GradientStops.Count;
                return Color.FromRgb((byte)(r / count), (byte)(g / count), (byte)(b / count));
            }

            return null;
        }

        private static int ToColorRef(Color color) => color.R | (color.G << 8) | (color.B << 16);

        private bool firstFrameRendered;
        private int? eraseColor;

        private bool EraseBackground(IntPtr hwnd, IntPtr hdc)
        {
            if (firstFrameRendered || eraseColor == null || hdc == IntPtr.Zero)
                return false;

            try
            {
                if (!GetClientRect(hwnd, out RECT rect))
                    return false;

                IntPtr brush = CreateSolidBrush(eraseColor.Value);
                if (brush == IntPtr.Zero)
                    return false;

                FillRect(hdc, ref rect, brush);
                DeleteObject(brush);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ChromeWindow: could not paint the background: {ex.Message}");
                return false;
            }
        }

        private static int SetDwmAttribute(IntPtr hwnd, int attribute, int value)
        {
            try
            {
                return DwmSetWindowAttribute(hwnd, attribute, ref value, sizeof(int));
            }
            catch (DllNotFoundException)
            {
            }
            catch (EntryPointNotFoundException)
            {
            }

            return -1;
        }

        private const int WM_ERASEBKGND = 0x0014;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_BORDER_COLOR = 34;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetClientRect(IntPtr hwnd, out RECT rect);

        [DllImport("user32.dll")]
        private static extern int FillRect(IntPtr hdc, ref RECT rect, IntPtr brush);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateSolidBrush(int color);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteObject(IntPtr handle);

        private IntPtr ChromeWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // A window sized to its content can't be maximized; drop the sizing before Windows maximizes it
            // (also when maximized by the keyboard, the system menu or Aero Snap).
            if (msg == WM_SYSCOMMAND && (wParam.ToInt64() & 0xFFF0) == SC_MAXIMIZE)
                SizeToContent = SizeToContent.Manual;

            if (msg == WM_ERASEBKGND && EraseBackground(hwnd, wParam))
            {
                handled = true;
                return new IntPtr(1);
            }

            return IntPtr.Zero;
        }

        private bool sizeRefreshed;

        /// <summary>
        /// With custom chrome, a window sized to its content can get the size of the native frame wrong on the first
        /// layout and show a black band. Measuring again once the window is shown fixes it. Switching to Manual keeps
        /// the current size, so the window doesn't jump or flash, and switching back measures the content again.
        /// </summary>
        private void RefreshSizeToContent()
        {
            if (SizeToContent != SizeToContent.WidthAndHeight)
                return;

            SizeToContent = SizeToContent.Manual;
            SizeToContent = SizeToContent.WidthAndHeight;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                if (CanResize)
                    ToggleMaximized();
                return;
            }

            if (WindowState == WindowState.Maximized)
            {
                // Restored only once the mouse actually moves, so a click alone leaves it maximized.
                restoreOnDrag = true;
                return;
            }

            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void TitleBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => restoreOnDrag = false;

        private void TitleBar_MouseMove(object sender, MouseEventArgs e)
        {
            if (!restoreOnDrag || e.LeftButton != MouseButtonState.Pressed)
                return;

            restoreOnDrag = false;

            // Restore under the mouse, keeping the grab point at the same relative spot of the title bar.
            Point positionInWindow = e.GetPosition(this);
            double relativeX = ActualWidth > 0 ? positionInWindow.X / ActualWidth : 0.5;
            Point mouseOnScreen = ToLogical(PointToScreen(positionInWindow));
            double restoreWidth = RestoreBounds.Width;

            WindowState = WindowState.Normal;
            Left = mouseOnScreen.X - restoreWidth * relativeX;
            Top = mouseOnScreen.Y - Math.Max(0, positionInWindow.Y - MaximizeBorderThickness.Top);

            if (Mouse.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void TitleBar_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            ShowSystemMenu(e.GetPosition(this));
            e.Handled = true;
        }

        private void Icon_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                Close();
                return;
            }

            // Below the icon, like the native system menu.
            FrameworkElement anchor = titleBar ?? iconPresenter;
            ShowSystemMenu(anchor.TranslatePoint(new Point(0, anchor.ActualHeight), this));
            e.Handled = true;
        }

        private void ShowSystemMenu(Point positionInWindow)
        {
            try
            {
                SystemCommands.ShowSystemMenu(this, ToLogical(PointToScreen(positionInWindow)));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ChromeWindow: could not show the system menu: {ex.Message}");
            }
        }

        // PointToScreen returns device pixels; window positions and SystemCommands use device independent units.
        private Point ToLogical(Point devicePoint)
        {
            var source = PresentationSource.FromVisual(this);
            return source?.CompositionTarget != null
                ? source.CompositionTarget.TransformFromDevice.Transform(devicePoint)
                : devicePoint;
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void MaximizeRestoreButton_Click(object sender, RoutedEventArgs e) => ToggleMaximized();

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        protected virtual void ToggleMaximized()
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
                return;
            }

            SizeToContent = SizeToContent.Manual;
            WindowState = WindowState.Maximized;
        }
    }
}
