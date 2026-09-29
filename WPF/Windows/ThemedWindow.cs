using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ZenStates.Core.OHWM;
using ZenTimings.Settings;
using ZenTimings.Theming;
using ZenTimings.Utils;

namespace ZenTimings
{
    /// <summary>
    /// Base of the application's windows: the themed chrome of <see cref="ChromeWindow"/>, the native border
    /// color of the theme, and snapping to (and moving with) the other application windows.
    /// </summary>
    public class ThemedWindow : ChromeWindow
    {
        private const double WindowSnapThreshold = 16;
        private const int WM_ENTERSIZEMOVE = 0x0231;
        private const int WM_EXITSIZEMOVE = 0x0232;
        private static readonly Dictionary<ThemedWindow, SnapAttachment> SnapAttachments = new Dictionary<ThemedWindow, SnapAttachment>();

        private bool _isApplyingWindowSnap;
        private bool _isMovingWindow;
        private double _lastMoveLeft;
        private double _lastMoveTop;

        private sealed class SnapAttachment
        {
            public ThemedWindow TargetWindow { get; set; }
        }

        public static readonly DependencyProperty NativeBorderBrushProperty =
            DependencyProperty.Register(
                "NativeBorderBrush",
                typeof(Brush),
                typeof(ThemedWindow),
                new PropertyMetadata(null, OnNativeBorderBrushChanged));

        public static readonly DependencyProperty CanSnapToOtherWindowsProperty =
            DependencyProperty.Register(
                "CanSnapToOtherWindows",
                typeof(bool),
                typeof(ThemedWindow),
                new PropertyMetadata(true));

        public static readonly DependencyProperty SnapIdentifierProperty =
            DependencyProperty.Register(
                "SnapIdentifier",
                typeof(string),
                typeof(ThemedWindow),
                new PropertyMetadata(null));

        public Brush NativeBorderBrush
        {
            get { return (Brush)GetValue(NativeBorderBrushProperty); }
            set { SetValue(NativeBorderBrushProperty, value); }
        }

        public bool CanSnapToOtherWindows
        {
            get { return (bool)GetValue(CanSnapToOtherWindowsProperty); }
            set { SetValue(CanSnapToOtherWindowsProperty, value); }
        }

        public string SnapIdentifier
        {
            get { return (string)GetValue(SnapIdentifierProperty); }
            set { SetValue(SnapIdentifierProperty, value); }
        }

        public ThemedWindow()
        {
            SetResourceReference(NativeBorderBrushProperty, "WindowBorderColor");
            Loaded += ThemedWindow_Loaded;
            Closing += ThemedWindow_Closing;
            LocationChanged += ThemedWindow_LocationChanged;
            Closed += ThemedWindow_Closed;
        }

        protected void MinimizeFootprint()
        {
            InteropMethods.EmptyWorkingSet(Process.GetCurrentProcess().Handle);
        }

        private void ThemedWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyNativeBorderFromTheme();
            MinimizeFootprint();

            if (AppSettings.Instance.EnableWindowSnapping && AppSettings.Instance.SaveWindowPosition)
            {
                Dispatcher.BeginInvoke(new Action(RestoreSavedSnapAttachments), DispatcherPriority.Loaded);
            }
        }

        private void ThemedWindow_LocationChanged(object sender, EventArgs e)
        {
            if (_isApplyingWindowSnap ||
                !AppSettings.Instance.EnableWindowSnapping ||
                !IsLoaded ||
                WindowState != WindowState.Normal)
            {
                return;
            }

            if (_isMovingWindow)
            {
                MoveAttachedWindows();

                if (CanSnapToOtherWindows)
                {
                    SnapToOtherWindows(false);
                }
            }
        }

        private void ThemedWindow_Closing(object sender, CancelEventArgs e)
        {
            PersistSnapAttachment();
        }

        private void ThemedWindow_Closed(object sender, EventArgs e)
        {
            ClearSnapAttachment(this);
            ClearFollowerAttachments(this);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            ApplyNativeBorderFromTheme();

            var source = PresentationSource.FromVisual(this) as HwndSource;
            if (source != null)
            {
                source.AddHook(WndProc);
            }

            if (AppSettings.Instance.CornerRadius != 0 && AppSettings.Instance.CornerRadius != 2)
            {
                WindowUtils.SetCornerPreference(this, AppSettings.Instance.CornerRadius);
            }
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            ApplyNativeBorderFromTheme();
        }

        protected override void OnDeactivated(EventArgs e)
        {
            base.OnDeactivated(e);
            ApplyNativeBorderFromTheme();
        }

        private static void OnNativeBorderBrushChanged(
            DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            var window = d as ThemedWindow;
            if (window != null)
            {
                window.ApplyNativeBorderFromTheme();
            }
        }

        public void ApplyNativeBorderFromTheme()
        {
            var brush = NativeBorderBrush as SolidColorBrush;

            if (brush == null)
                brush = BorderBrush as SolidColorBrush;

            if (brush == null)
                return;

            WindowUtils.TrySetBorderColor(this, brush);
        }

        public static void RefreshAllOpenWindows()
        {
            if (Application.Current == null)
                return;

            foreach (Window window in Application.Current.Windows)
            {
                var themedWindow = window as ThemedWindow;
                if (themedWindow != null)
                {
                    themedWindow.ApplyNativeBorderFromTheme();
                }
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case WM_ENTERSIZEMOVE:
                    if (AppSettings.Instance.EnableWindowSnapping && WindowState == WindowState.Normal)
                    {
                        _isMovingWindow = true;
                        _lastMoveLeft = Left;
                        _lastMoveTop = Top;
                        ClearSnapAttachment(this);
                    }
                    break;
                case WM_EXITSIZEMOVE:
                    if (_isMovingWindow)
                    {
                        _isMovingWindow = false;
                        if (CanSnapToOtherWindows)
                        {
                            if (!SnapToOtherWindows(true))
                            {
                                ClearSnapAttachment(this);
                            }
                        }
                    }
                    break;
            }

            return IntPtr.Zero;
        }

        private void MoveAttachedWindows()
        {
            var deltaLeft = Left - _lastMoveLeft;
            var deltaTop = Top - _lastMoveTop;

            _lastMoveLeft = Left;
            _lastMoveTop = Top;

            if (Math.Abs(deltaLeft) < double.Epsilon && Math.Abs(deltaTop) < double.Epsilon)
                return;

            MoveAttachedWindows(this, deltaLeft, deltaTop, new HashSet<ThemedWindow>());
        }

        private static void MoveAttachedWindows(ThemedWindow targetWindow, double deltaLeft, double deltaTop, HashSet<ThemedWindow> visited)
        {
            if (!visited.Add(targetWindow))
                return;

            var followers = SnapAttachments
                .Where(x => x.Value.TargetWindow == targetWindow)
                .Select(x => x.Key)
                .ToList();

            foreach (var follower in followers)
            {
                if (!follower.IsVisible || follower.WindowState != WindowState.Normal)
                    continue;

                follower._isApplyingWindowSnap = true;
                try
                {
                    follower.Left += deltaLeft;
                    follower.Top += deltaTop;
                }
                finally
                {
                    follower._isApplyingWindowSnap = false;
                }

                MoveAttachedWindows(follower, deltaLeft, deltaTop, visited);
            }
        }

        private bool SnapToOtherWindows(bool updateAttachment)
        {
            ThemedWindow targetWindow;
            double snappedLeft;
            double snappedTop;
            bool hasSnappedLeft;
            bool hasSnappedTop;

            if (!TryGetSnapCandidate(out targetWindow, out snappedLeft, out snappedTop, out hasSnappedLeft, out hasSnappedTop))
                return false;

            var originalLeft = Left;
            var originalTop = Top;

            _isApplyingWindowSnap = true;
            try
            {
                if (hasSnappedLeft)
                    Left = snappedLeft;

                if (hasSnappedTop)
                    Top = snappedTop;
            }
            finally
            {
                _isApplyingWindowSnap = false;
            }

            if (updateAttachment)
            {
                var deltaLeft = Left - originalLeft;
                var deltaTop = Top - originalTop;

                if (Math.Abs(deltaLeft) > double.Epsilon || Math.Abs(deltaTop) > double.Epsilon)
                {
                    MoveAttachedWindows(this, deltaLeft, deltaTop, new HashSet<ThemedWindow>());
                }

                SetSnapAttachment(this, targetWindow);
            }

            return true;
        }

        private bool TryGetSnapCandidate(out ThemedWindow targetWindow, out double snappedLeft, out double snappedTop, out bool hasSnappedLeft, out bool hasSnappedTop)
        {
            targetWindow = null;
            snappedLeft = Left;
            snappedTop = Top;
            hasSnappedLeft = false;
            hasSnappedTop = false;

            if (Application.Current == null || ActualWidth <= 0 || ActualHeight <= 0)
                return false;

            var currentBounds = new Rect(Left, Top, ActualWidth, ActualHeight);
            var bestScore = WindowSnapThreshold + 1;

            foreach (Window window in Application.Current.Windows)
            {
                var themedWindow = window as ThemedWindow;
                if (themedWindow == null ||
                    ReferenceEquals(themedWindow, this) ||
                    !themedWindow.IsVisible ||
                    themedWindow.WindowState != WindowState.Normal ||
                    themedWindow.ActualWidth <= 0 ||
                    themedWindow.ActualHeight <= 0)
                {
                    continue;
                }

                var otherBounds = new Rect(themedWindow.Left, themedWindow.Top, themedWindow.ActualWidth, themedWindow.ActualHeight);
                var candidateLeft = Left;
                var candidateTop = Top;
                var horizontalDistance = WindowSnapThreshold + 1;
                var verticalDistance = WindowSnapThreshold + 1;
                var candidateHasLeft = false;
                var candidateHasTop = false;

                TrySnapHorizontal(currentBounds, otherBounds, ref candidateLeft, ref horizontalDistance, ref candidateHasLeft);
                TrySnapVertical(currentBounds, otherBounds, ref candidateTop, ref verticalDistance, ref candidateHasTop);

                if (!candidateHasLeft && !candidateHasTop)
                    continue;

                var score = Math.Min(
                    candidateHasLeft ? horizontalDistance : WindowSnapThreshold + 1,
                    candidateHasTop ? verticalDistance : WindowSnapThreshold + 1);

                if (score >= bestScore)
                    continue;

                bestScore = score;
                targetWindow = themedWindow;
                snappedLeft = candidateLeft;
                snappedTop = candidateTop;
                hasSnappedLeft = candidateHasLeft;
                hasSnappedTop = candidateHasTop;
            }

            return targetWindow != null;
        }

        private static void SetSnapAttachment(ThemedWindow follower, ThemedWindow targetWindow)
        {
            if (follower == null ||
                targetWindow == null ||
                ReferenceEquals(follower, targetWindow) ||
                ReferenceEquals(follower, Application.Current?.MainWindow))
            {
                return;
            }

            SnapAttachments[follower] = new SnapAttachment
            {
                TargetWindow = targetWindow
            };
        }

        private static void ClearSnapAttachment(ThemedWindow window)
        {
            if (window == null)
                return;

            SnapAttachments.Remove(window);
        }

        private static void ClearFollowerAttachments(ThemedWindow targetWindow)
        {
            var followers = SnapAttachments
                .Where(x => x.Value.TargetWindow == targetWindow)
                .Select(x => x.Key)
                .ToList();

            foreach (var follower in followers)
            {
                SnapAttachments.Remove(follower);
            }
        }

        private void PersistSnapAttachment()
        {
            if (!AppSettings.Instance.SaveWindowPosition || string.IsNullOrWhiteSpace(SnapIdentifier))
                return;

            var targetWindow = GetSnapTargetWindow(this);
            AppSettings.Instance.SetWindowSnapTarget(SnapIdentifier, targetWindow?.SnapIdentifier);
        }

        private static ThemedWindow GetSnapTargetWindow(ThemedWindow window)
        {
            SnapAttachment snapAttachment;
            return window != null && SnapAttachments.TryGetValue(window, out snapAttachment)
                ? snapAttachment.TargetWindow
                : null;
        }

        private static void RestoreSavedSnapAttachments()
        {
            if (Application.Current == null)
                return;

            var openWindows = Application.Current.Windows
                .OfType<Window>()
                .OfType<ThemedWindow>()
                .Where(x => x.IsLoaded && x.IsVisible && x.WindowState == WindowState.Normal)
                .ToList();

            foreach (var follower in openWindows)
            {
                if (!follower.CanSnapToOtherWindows || string.IsNullOrWhiteSpace(follower.SnapIdentifier))
                {
                    ClearSnapAttachment(follower);
                    continue;
                }

                var targetIdentifier = AppSettings.Instance.GetWindowSnapTarget(follower.SnapIdentifier);
                if (string.IsNullOrWhiteSpace(targetIdentifier))
                {
                    ClearSnapAttachment(follower);
                    continue;
                }

                var targetWindow = openWindows.FirstOrDefault(x =>
                    !ReferenceEquals(x, follower) &&
                    string.Equals(x.SnapIdentifier, targetIdentifier, StringComparison.OrdinalIgnoreCase));

                if (targetWindow != null && AreWindowsSnapped(follower, targetWindow))
                    SetSnapAttachment(follower, targetWindow);
            }
        }

        private static bool AreWindowsSnapped(ThemedWindow currentWindow, ThemedWindow targetWindow)
        {
            if (currentWindow == null || targetWindow == null)
                return false;

            var currentBounds = new Rect(currentWindow.Left, currentWindow.Top, currentWindow.ActualWidth, currentWindow.ActualHeight);
            var targetBounds = new Rect(targetWindow.Left, targetWindow.Top, targetWindow.ActualWidth, targetWindow.ActualHeight);

            return IsHorizontalSnap(currentBounds, targetBounds) || IsVerticalSnap(currentBounds, targetBounds);
        }

        private static bool IsHorizontalSnap(Rect currentBounds, Rect targetBounds)
        {
            return RangesOverlap(currentBounds.Top, currentBounds.Bottom, targetBounds.Top, targetBounds.Bottom) &&
                   (IsWithinSnapThreshold(currentBounds.Left, targetBounds.Left) ||
                    IsWithinSnapThreshold(currentBounds.Left, targetBounds.Right) ||
                    IsWithinSnapThreshold(currentBounds.Right, targetBounds.Left) ||
                    IsWithinSnapThreshold(currentBounds.Right, targetBounds.Right));
        }

        private static bool IsVerticalSnap(Rect currentBounds, Rect targetBounds)
        {
            return RangesOverlap(currentBounds.Left, currentBounds.Right, targetBounds.Left, targetBounds.Right) &&
                   (IsWithinSnapThreshold(currentBounds.Top, targetBounds.Top) ||
                    IsWithinSnapThreshold(currentBounds.Top, targetBounds.Bottom) ||
                    IsWithinSnapThreshold(currentBounds.Bottom, targetBounds.Top) ||
                    IsWithinSnapThreshold(currentBounds.Bottom, targetBounds.Bottom));
        }

        private static bool IsWithinSnapThreshold(double value1, double value2)
        {
            return Math.Abs(value1 - value2) <= WindowSnapThreshold;
        }

        private static void TrySnapHorizontal(Rect currentBounds, Rect otherBounds, ref double snappedLeft, ref double bestDistance, ref bool hasSnap)
        {
            if (!RangesOverlap(currentBounds.Top, currentBounds.Bottom, otherBounds.Top, otherBounds.Bottom))
                return;

            TryUpdateSnap(Math.Abs(currentBounds.Left - otherBounds.Left), otherBounds.Left, ref snappedLeft, ref bestDistance, ref hasSnap);
            TryUpdateSnap(Math.Abs(currentBounds.Left - otherBounds.Right), otherBounds.Right, ref snappedLeft, ref bestDistance, ref hasSnap);
            TryUpdateSnap(Math.Abs(currentBounds.Right - otherBounds.Left), otherBounds.Left - currentBounds.Width, ref snappedLeft, ref bestDistance, ref hasSnap);
            TryUpdateSnap(Math.Abs(currentBounds.Right - otherBounds.Right), otherBounds.Right - currentBounds.Width, ref snappedLeft, ref bestDistance, ref hasSnap);
        }

        private static void TrySnapVertical(Rect currentBounds, Rect otherBounds, ref double snappedTop, ref double bestDistance, ref bool hasSnap)
        {
            if (!RangesOverlap(currentBounds.Left, currentBounds.Right, otherBounds.Left, otherBounds.Right))
                return;

            TryUpdateSnap(Math.Abs(currentBounds.Top - otherBounds.Top), otherBounds.Top, ref snappedTop, ref bestDistance, ref hasSnap);
            TryUpdateSnap(Math.Abs(currentBounds.Top - otherBounds.Bottom), otherBounds.Bottom, ref snappedTop, ref bestDistance, ref hasSnap);
            TryUpdateSnap(Math.Abs(currentBounds.Bottom - otherBounds.Top), otherBounds.Top - currentBounds.Height, ref snappedTop, ref bestDistance, ref hasSnap);
            TryUpdateSnap(Math.Abs(currentBounds.Bottom - otherBounds.Bottom), otherBounds.Bottom - currentBounds.Height, ref snappedTop, ref bestDistance, ref hasSnap);
        }

        private static bool RangesOverlap(double start1, double end1, double start2, double end2)
        {
            return end1 >= start2 - WindowSnapThreshold && end2 >= start1 - WindowSnapThreshold;
        }

        private static void TryUpdateSnap(double distance, double candidate, ref double snappedValue, ref double bestDistance, ref bool hasSnap)
        {
            if (distance > WindowSnapThreshold || distance >= bestDistance)
                return;

            snappedValue = candidate;
            bestDistance = distance;
            hasSnap = true;
        }
    }
}
