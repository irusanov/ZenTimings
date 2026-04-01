using System;
using System.Windows;
using System.Windows.Media;
using AdonisUI.Controls;

namespace ZenTimings
{
    public class ThemedAdonisWindow : AdonisWindow
    {
        public static readonly DependencyProperty NativeBorderBrushProperty =
            DependencyProperty.Register(
                "NativeBorderBrush",
                typeof(Brush),
                typeof(ThemedAdonisWindow),
                new PropertyMetadata(null, OnNativeBorderBrushChanged));

        public Brush NativeBorderBrush
        {
            get { return (Brush)GetValue(NativeBorderBrushProperty); }
            set { SetValue(NativeBorderBrushProperty, value); }
        }

        public ThemedAdonisWindow()
        {
            SetResourceReference(NativeBorderBrushProperty, "WindowBorderColor");
            Loaded += ThemedAdonisWindow_Loaded;
        }

        private void ThemedAdonisWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyNativeBorderFromTheme();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            ApplyNativeBorderFromTheme();
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
            var window = d as ThemedAdonisWindow;
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
                var themedWindow = window as ThemedAdonisWindow;
                if (themedWindow != null)
                {
                    themedWindow.ApplyNativeBorderFromTheme();
                }
            }
        }
    }
}