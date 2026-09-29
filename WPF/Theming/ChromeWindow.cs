using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

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

            RefreshSizeToContent();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            if (PresentationSource.FromVisual(this) is HwndSource source)
                source.AddHook(ChromeWndProc);
        }

        private IntPtr ChromeWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // A window sized to its content can't be maximized; drop the sizing before Windows maximizes it
            // (also when maximized by the keyboard, the system menu or Aero Snap).
            if (msg == WM_SYSCOMMAND && (wParam.ToInt64() & 0xFFF0) == SC_MAXIMIZE)
                SizeToContent = SizeToContent.Manual;

            return IntPtr.Zero;
        }

        /// <summary>
        /// With custom chrome, a window sized to its content can get the size of the native frame wrong on the first
        /// layout and show a black band. Measuring again once loaded fixes it.
        /// </summary>
        private void RefreshSizeToContent()
        {
            if (SizeToContent != SizeToContent.WidthAndHeight)
                return;

            SizeToContent = SizeToContent.Manual;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => SizeToContent = SizeToContent.WidthAndHeight));
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
