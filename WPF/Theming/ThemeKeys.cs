using System.Windows;

namespace ZenTimings.Theming
{
    // Resource keys shared by the color schemes (Theming/ColorSchemes), the app themes (Themes/*.xaml) and the
    // control styles (Theming/Controls.xaml).
    //
    // A theme only sets the handful of colors in ThemeColors. Every brush in ThemeBrushes is built from those
    // colors (Theming/ColorSchemes/Shared.xaml); the interaction states (hover, pressed) are translucent layers set
    // by the dark and light color schemes or the accent at a lower opacity, so they fit any background.

    /// <summary>The colors a theme defines.</summary>
    public static class ThemeColors
    {
        /// <summary>Window background.</summary>
        public static ComponentResourceKey BackgroundColor => Key("BackgroundColor");
        /// <summary>Cards and group boxes. Usually a translucent white (dark themes) so it works on gradients.</summary>
        public static ComponentResourceKey SurfaceColor => Key("SurfaceColor");
        /// <summary>Opaque background of menus, drop-downs and tool tips.</summary>
        public static ComponentResourceKey PopupColor => Key("PopupColor");
        /// <summary>Borders of controls, cards and popups.</summary>
        public static ComponentResourceKey BorderColor => Key("BorderColor");
        public static ComponentResourceKey ForegroundColor => Key("ForegroundColor");
        public static ComponentResourceKey SecondaryForegroundColor => Key("SecondaryForegroundColor");
        public static ComponentResourceKey DisabledForegroundColor => Key("DisabledForegroundColor");
        public static ComponentResourceKey AccentColor => Key("AccentColor");
        /// <summary>Text and glyphs drawn on the accent color.</summary>
        public static ComponentResourceKey AccentForegroundColor => Key("AccentForegroundColor");
        public static ComponentResourceKey SuccessColor => Key("SuccessColor");
        public static ComponentResourceKey WarningColor => Key("WarningColor");
        public static ComponentResourceKey ErrorColor => Key("ErrorColor");

        private static ComponentResourceKey Key(string id) => new ComponentResourceKey(typeof(ThemeColors), id);
    }

    /// <summary>Brushes built from <see cref="ThemeColors"/>.</summary>
    public static class ThemeBrushes
    {
        public static ComponentResourceKey BackgroundBrush => Key("BackgroundBrush");
        public static ComponentResourceKey SurfaceBrush => Key("SurfaceBrush");
        public static ComponentResourceKey PopupBrush => Key("PopupBrush");
        public static ComponentResourceKey BorderBrush => Key("BorderBrush");
        public static ComponentResourceKey ForegroundBrush => Key("ForegroundBrush");
        public static ComponentResourceKey SecondaryForegroundBrush => Key("SecondaryForegroundBrush");
        public static ComponentResourceKey DisabledForegroundBrush => Key("DisabledForegroundBrush");
        public static ComponentResourceKey AccentBrush => Key("AccentBrush");
        public static ComponentResourceKey AccentForegroundBrush => Key("AccentForegroundBrush");
        public static ComponentResourceKey SuccessBrush => Key("SuccessBrush");
        public static ComponentResourceKey WarningBrush => Key("WarningBrush");
        public static ComponentResourceKey ErrorBrush => Key("ErrorBrush");

        /// <summary>Resting fill of buttons, text boxes and other controls.</summary>
        public static ComponentResourceKey ControlFillBrush => Key("ControlFillBrush");
        public static ComponentResourceKey ControlFillHoverBrush => Key("ControlFillHoverBrush");
        public static ComponentResourceKey ControlFillPressedBrush => Key("ControlFillPressedBrush");
        /// <summary>Hover fill of transparent elements: menu items, list rows, window buttons.</summary>
        public static ComponentResourceKey SubtleHoverBrush => Key("SubtleHoverBrush");
        public static ComponentResourceKey SubtlePressedBrush => Key("SubtlePressedBrush");
        public static ComponentResourceKey AccentHoverBrush => Key("AccentHoverBrush");
        public static ComponentResourceKey AccentPressedBrush => Key("AccentPressedBrush");

        private static ComponentResourceKey Key(string id) => new ComponentResourceKey(typeof(ThemeBrushes), id);
    }

    /// <summary>Sizes a theme may override.</summary>
    public static class ThemeDimensions
    {
        /// <summary>Corner radius of controls (buttons, text boxes, combo boxes).</summary>
        public static ComponentResourceKey CornerRadius => new ComponentResourceKey(typeof(ThemeDimensions), "CornerRadius");
    }

    /// <summary>Named control styles.</summary>
    public static class ThemeStyles
    {
        /// <summary>Button filled with the accent color, for the main action of a window.</summary>
        public static ComponentResourceKey AccentButton => Key("AccentButton");
        /// <summary>On/off switch for a ToggleButton or CheckBox.</summary>
        public static ComponentResourceKey ToggleSwitch => Key("ToggleSwitch");
        public static ComponentResourceKey WindowButton => Key("WindowButton");
        public static ComponentResourceKey WindowCloseButton => Key("WindowCloseButton");

        private static ComponentResourceKey Key(string id) => new ComponentResourceKey(typeof(ThemeStyles), id);
    }

    /// <summary>Glyphs used by the control templates.</summary>
    public static class ThemeTemplates
    {
        /// <summary>Drop-down arrow (combo boxes, menus).</summary>
        public static ComponentResourceKey Expander => Key("Expander");
        public static ComponentResourceKey CheckMark => Key("CheckMark");
        public static ComponentResourceKey WindowMinimize => Key("WindowMinimize");
        public static ComponentResourceKey WindowMaximize => Key("WindowMaximize");
        public static ComponentResourceKey WindowRestore => Key("WindowRestore");
        public static ComponentResourceKey WindowClose => Key("WindowClose");

        private static ComponentResourceKey Key(string id) => new ComponentResourceKey(typeof(ThemeTemplates), id);
    }
}
