using System.Windows;

namespace ZenTimings.Theming
{
    // Resource keys shared by the color schemes (Theming/ColorSchemes), the app themes (Themes/*.xaml) and the
    // control styles (Theming/Controls.xaml). Colors are defined by a color scheme and may be overridden by a
    // theme; every brush is built from the color of the same name, so overriding a color recolors its brush.
    //
    // The layer keys follow a simple model: content inside a window is on layer 1, and every GroupBox (or any
    // element with LayerExtension.IncreaseLayer) puts its content one layer higher, up to layer 4. Controls pick
    // their background, border and highlight colors from the layer they are on (see LayerExtension).

    /// <summary>Color resource keys.</summary>
    public static class ThemeColors
    {
        public static ComponentResourceKey ForegroundColor => new ComponentResourceKey(typeof(ThemeColors), "ForegroundColor");
        public static ComponentResourceKey AccentColor => new ComponentResourceKey(typeof(ThemeColors), "AccentColor");
        public static ComponentResourceKey AccentHighlightColor => new ComponentResourceKey(typeof(ThemeColors), "AccentHighlightColor");
        public static ComponentResourceKey AccentForegroundColor => new ComponentResourceKey(typeof(ThemeColors), "AccentForegroundColor");
        public static ComponentResourceKey AccentIntenseHighlightColor => new ComponentResourceKey(typeof(ThemeColors), "AccentIntenseHighlightColor");
        public static ComponentResourceKey AccentIntenseHighlightBorderColor => new ComponentResourceKey(typeof(ThemeColors), "AccentIntenseHighlightBorderColor");
        public static ComponentResourceKey AccentInteractionColor => new ComponentResourceKey(typeof(ThemeColors), "AccentInteractionColor");
        public static ComponentResourceKey AccentInteractionBorderColor => new ComponentResourceKey(typeof(ThemeColors), "AccentInteractionBorderColor");
        public static ComponentResourceKey AccentInteractionForegroundColor => new ComponentResourceKey(typeof(ThemeColors), "AccentInteractionForegroundColor");
        public static ComponentResourceKey Layer0BackgroundColor => new ComponentResourceKey(typeof(ThemeColors), "Layer0BackgroundColor");
        public static ComponentResourceKey Layer0BorderColor => new ComponentResourceKey(typeof(ThemeColors), "Layer0BorderColor");
        public static ComponentResourceKey Layer1BackgroundColor => new ComponentResourceKey(typeof(ThemeColors), "Layer1BackgroundColor");
        public static ComponentResourceKey Layer1BorderColor => new ComponentResourceKey(typeof(ThemeColors), "Layer1BorderColor");
        public static ComponentResourceKey Layer1HighlightColor => new ComponentResourceKey(typeof(ThemeColors), "Layer1HighlightColor");
        public static ComponentResourceKey Layer1HighlightBorderColor => new ComponentResourceKey(typeof(ThemeColors), "Layer1HighlightBorderColor");
        public static ComponentResourceKey Layer1IntenseHighlightColor => new ComponentResourceKey(typeof(ThemeColors), "Layer1IntenseHighlightColor");
        public static ComponentResourceKey Layer1IntenseHighlightBorderColor => new ComponentResourceKey(typeof(ThemeColors), "Layer1IntenseHighlightBorderColor");
        public static ComponentResourceKey Layer1InteractionColor => new ComponentResourceKey(typeof(ThemeColors), "Layer1InteractionColor");
        public static ComponentResourceKey Layer1InteractionBorderColor => new ComponentResourceKey(typeof(ThemeColors), "Layer1InteractionBorderColor");
        public static ComponentResourceKey Layer1InteractionForegroundColor => new ComponentResourceKey(typeof(ThemeColors), "Layer1InteractionForegroundColor");
        public static ComponentResourceKey Layer2BackgroundColor => new ComponentResourceKey(typeof(ThemeColors), "Layer2BackgroundColor");
        public static ComponentResourceKey Layer2BorderColor => new ComponentResourceKey(typeof(ThemeColors), "Layer2BorderColor");
        public static ComponentResourceKey Layer2HighlightColor => new ComponentResourceKey(typeof(ThemeColors), "Layer2HighlightColor");
        public static ComponentResourceKey Layer2HighlightBorderColor => new ComponentResourceKey(typeof(ThemeColors), "Layer2HighlightBorderColor");
        public static ComponentResourceKey Layer2IntenseHighlightColor => new ComponentResourceKey(typeof(ThemeColors), "Layer2IntenseHighlightColor");
        public static ComponentResourceKey Layer2IntenseHighlightBorderColor => new ComponentResourceKey(typeof(ThemeColors), "Layer2IntenseHighlightBorderColor");
        public static ComponentResourceKey Layer2InteractionColor => new ComponentResourceKey(typeof(ThemeColors), "Layer2InteractionColor");
        public static ComponentResourceKey Layer2InteractionBorderColor => new ComponentResourceKey(typeof(ThemeColors), "Layer2InteractionBorderColor");
        public static ComponentResourceKey Layer2InteractionForegroundColor => new ComponentResourceKey(typeof(ThemeColors), "Layer2InteractionForegroundColor");
        public static ComponentResourceKey Layer3BackgroundColor => new ComponentResourceKey(typeof(ThemeColors), "Layer3BackgroundColor");
        public static ComponentResourceKey Layer3BorderColor => new ComponentResourceKey(typeof(ThemeColors), "Layer3BorderColor");
        public static ComponentResourceKey Layer3HighlightColor => new ComponentResourceKey(typeof(ThemeColors), "Layer3HighlightColor");
        public static ComponentResourceKey Layer3HighlightBorderColor => new ComponentResourceKey(typeof(ThemeColors), "Layer3HighlightBorderColor");
        public static ComponentResourceKey Layer3IntenseHighlightColor => new ComponentResourceKey(typeof(ThemeColors), "Layer3IntenseHighlightColor");
        public static ComponentResourceKey Layer3IntenseHighlightBorderColor => new ComponentResourceKey(typeof(ThemeColors), "Layer3IntenseHighlightBorderColor");
        public static ComponentResourceKey Layer3InteractionColor => new ComponentResourceKey(typeof(ThemeColors), "Layer3InteractionColor");
        public static ComponentResourceKey Layer3InteractionBorderColor => new ComponentResourceKey(typeof(ThemeColors), "Layer3InteractionBorderColor");
        public static ComponentResourceKey Layer3InteractionForegroundColor => new ComponentResourceKey(typeof(ThemeColors), "Layer3InteractionForegroundColor");
        public static ComponentResourceKey Layer4BackgroundColor => new ComponentResourceKey(typeof(ThemeColors), "Layer4BackgroundColor");
        public static ComponentResourceKey Layer4BorderColor => new ComponentResourceKey(typeof(ThemeColors), "Layer4BorderColor");
        public static ComponentResourceKey Layer4HighlightColor => new ComponentResourceKey(typeof(ThemeColors), "Layer4HighlightColor");
        public static ComponentResourceKey Layer4HighlightBorderColor => new ComponentResourceKey(typeof(ThemeColors), "Layer4HighlightBorderColor");
        public static ComponentResourceKey Layer4IntenseHighlightColor => new ComponentResourceKey(typeof(ThemeColors), "Layer4IntenseHighlightColor");
        public static ComponentResourceKey Layer4IntenseHighlightBorderColor => new ComponentResourceKey(typeof(ThemeColors), "Layer4IntenseHighlightBorderColor");
        public static ComponentResourceKey Layer4InteractionColor => new ComponentResourceKey(typeof(ThemeColors), "Layer4InteractionColor");
        public static ComponentResourceKey Layer4InteractionBorderColor => new ComponentResourceKey(typeof(ThemeColors), "Layer4InteractionBorderColor");
        public static ComponentResourceKey Layer4InteractionForegroundColor => new ComponentResourceKey(typeof(ThemeColors), "Layer4InteractionForegroundColor");
        public static ComponentResourceKey DisabledForegroundColor => new ComponentResourceKey(typeof(ThemeColors), "DisabledForegroundColor");
        public static ComponentResourceKey DisabledAccentForegroundColor => new ComponentResourceKey(typeof(ThemeColors), "DisabledAccentForegroundColor");
        public static ComponentResourceKey SuccessColor => new ComponentResourceKey(typeof(ThemeColors), "SuccessColor");
        public static ComponentResourceKey ErrorColor => new ComponentResourceKey(typeof(ThemeColors), "ErrorColor");
        public static ComponentResourceKey AlertColor => new ComponentResourceKey(typeof(ThemeColors), "AlertColor");
        public static ComponentResourceKey HyperlinkColor => new ComponentResourceKey(typeof(ThemeColors), "HyperlinkColor");
        public static ComponentResourceKey WindowButtonHighlightColor => new ComponentResourceKey(typeof(ThemeColors), "WindowButtonHighlightColor");
        public static ComponentResourceKey WindowButtonInteractionColor => new ComponentResourceKey(typeof(ThemeColors), "WindowButtonInteractionColor");
    }

    /// <summary>Brush resource keys, one per color.</summary>
    public static class ThemeBrushes
    {
        public static ComponentResourceKey ForegroundBrush => new ComponentResourceKey(typeof(ThemeBrushes), "ForegroundBrush");
        public static ComponentResourceKey AccentBrush => new ComponentResourceKey(typeof(ThemeBrushes), "AccentBrush");
        public static ComponentResourceKey AccentHighlightBrush => new ComponentResourceKey(typeof(ThemeBrushes), "AccentHighlightBrush");
        public static ComponentResourceKey AccentForegroundBrush => new ComponentResourceKey(typeof(ThemeBrushes), "AccentForegroundBrush");
        public static ComponentResourceKey AccentIntenseHighlightBrush => new ComponentResourceKey(typeof(ThemeBrushes), "AccentIntenseHighlightBrush");
        public static ComponentResourceKey AccentIntenseHighlightBorderBrush => new ComponentResourceKey(typeof(ThemeBrushes), "AccentIntenseHighlightBorderBrush");
        public static ComponentResourceKey AccentInteractionBrush => new ComponentResourceKey(typeof(ThemeBrushes), "AccentInteractionBrush");
        public static ComponentResourceKey AccentInteractionBorderBrush => new ComponentResourceKey(typeof(ThemeBrushes), "AccentInteractionBorderBrush");
        public static ComponentResourceKey AccentInteractionForegroundBrush => new ComponentResourceKey(typeof(ThemeBrushes), "AccentInteractionForegroundBrush");
        public static ComponentResourceKey Layer0BackgroundBrush => new ComponentResourceKey(typeof(ThemeBrushes), "Layer0BackgroundBrush");
        public static ComponentResourceKey Layer0BorderBrush => new ComponentResourceKey(typeof(ThemeBrushes), "Layer0BorderBrush");
        public static ComponentResourceKey Layer1BackgroundBrush => new ComponentResourceKey(typeof(ThemeBrushes), "Layer1BackgroundBrush");
        public static ComponentResourceKey Layer1BorderBrush => new ComponentResourceKey(typeof(ThemeBrushes), "Layer1BorderBrush");
        public static ComponentResourceKey Layer1HighlightBrush => new ComponentResourceKey(typeof(ThemeBrushes), "Layer1HighlightBrush");
        public static ComponentResourceKey Layer1HighlightBorderBrush => new ComponentResourceKey(typeof(ThemeBrushes), "Layer1HighlightBorderBrush");
        public static ComponentResourceKey Layer1IntenseHighlightBrush => new ComponentResourceKey(typeof(ThemeBrushes), "Layer1IntenseHighlightBrush");
        public static ComponentResourceKey Layer1IntenseHighlightBorderBrush => new ComponentResourceKey(typeof(ThemeBrushes), "Layer1IntenseHighlightBorderBrush");
        public static ComponentResourceKey Layer1InteractionBrush => new ComponentResourceKey(typeof(ThemeBrushes), "Layer1InteractionBrush");
        public static ComponentResourceKey Layer1InteractionBorderBrush => new ComponentResourceKey(typeof(ThemeBrushes), "Layer1InteractionBorderBrush");
        public static ComponentResourceKey Layer1InteractionForegroundBrush => new ComponentResourceKey(typeof(ThemeBrushes), "Layer1InteractionForegroundBrush");
        public static ComponentResourceKey Layer2BackgroundBrush => new ComponentResourceKey(typeof(ThemeBrushes), "Layer2BackgroundBrush");
        public static ComponentResourceKey Layer2BorderBrush => new ComponentResourceKey(typeof(ThemeBrushes), "Layer2BorderBrush");
        public static ComponentResourceKey Layer2HighlightBrush => new ComponentResourceKey(typeof(ThemeBrushes), "Layer2HighlightBrush");
        public static ComponentResourceKey Layer2HighlightBorderBrush => new ComponentResourceKey(typeof(ThemeBrushes), "Layer2HighlightBorderBrush");
        public static ComponentResourceKey Layer2IntenseHighlightBrush => new ComponentResourceKey(typeof(ThemeBrushes), "Layer2IntenseHighlightBrush");
        public static ComponentResourceKey Layer2IntenseHighlightBorderBrush => new ComponentResourceKey(typeof(ThemeBrushes), "Layer2IntenseHighlightBorderBrush");
        public static ComponentResourceKey Layer2InteractionBrush => new ComponentResourceKey(typeof(ThemeBrushes), "Layer2InteractionBrush");
        public static ComponentResourceKey Layer2InteractionBorderBrush => new ComponentResourceKey(typeof(ThemeBrushes), "Layer2InteractionBorderBrush");
        public static ComponentResourceKey Layer2InteractionForegroundBrush => new ComponentResourceKey(typeof(ThemeBrushes), "Layer2InteractionForegroundBrush");
        public static ComponentResourceKey Layer3BackgroundBrush => new ComponentResourceKey(typeof(ThemeBrushes), "Layer3BackgroundBrush");
        public static ComponentResourceKey Layer3BorderBrush => new ComponentResourceKey(typeof(ThemeBrushes), "Layer3BorderBrush");
        public static ComponentResourceKey Layer3HighlightBrush => new ComponentResourceKey(typeof(ThemeBrushes), "Layer3HighlightBrush");
        public static ComponentResourceKey Layer3HighlightBorderBrush => new ComponentResourceKey(typeof(ThemeBrushes), "Layer3HighlightBorderBrush");
        public static ComponentResourceKey Layer3IntenseHighlightBrush => new ComponentResourceKey(typeof(ThemeBrushes), "Layer3IntenseHighlightBrush");
        public static ComponentResourceKey Layer3IntenseHighlightBorderBrush => new ComponentResourceKey(typeof(ThemeBrushes), "Layer3IntenseHighlightBorderBrush");
        public static ComponentResourceKey Layer3InteractionBrush => new ComponentResourceKey(typeof(ThemeBrushes), "Layer3InteractionBrush");
        public static ComponentResourceKey Layer3InteractionBorderBrush => new ComponentResourceKey(typeof(ThemeBrushes), "Layer3InteractionBorderBrush");
        public static ComponentResourceKey Layer3InteractionForegroundBrush => new ComponentResourceKey(typeof(ThemeBrushes), "Layer3InteractionForegroundBrush");
        public static ComponentResourceKey Layer4BackgroundBrush => new ComponentResourceKey(typeof(ThemeBrushes), "Layer4BackgroundBrush");
        public static ComponentResourceKey Layer4BorderBrush => new ComponentResourceKey(typeof(ThemeBrushes), "Layer4BorderBrush");
        public static ComponentResourceKey Layer4HighlightBrush => new ComponentResourceKey(typeof(ThemeBrushes), "Layer4HighlightBrush");
        public static ComponentResourceKey Layer4HighlightBorderBrush => new ComponentResourceKey(typeof(ThemeBrushes), "Layer4HighlightBorderBrush");
        public static ComponentResourceKey Layer4IntenseHighlightBrush => new ComponentResourceKey(typeof(ThemeBrushes), "Layer4IntenseHighlightBrush");
        public static ComponentResourceKey Layer4IntenseHighlightBorderBrush => new ComponentResourceKey(typeof(ThemeBrushes), "Layer4IntenseHighlightBorderBrush");
        public static ComponentResourceKey Layer4InteractionBrush => new ComponentResourceKey(typeof(ThemeBrushes), "Layer4InteractionBrush");
        public static ComponentResourceKey Layer4InteractionBorderBrush => new ComponentResourceKey(typeof(ThemeBrushes), "Layer4InteractionBorderBrush");
        public static ComponentResourceKey Layer4InteractionForegroundBrush => new ComponentResourceKey(typeof(ThemeBrushes), "Layer4InteractionForegroundBrush");
        public static ComponentResourceKey DisabledForegroundBrush => new ComponentResourceKey(typeof(ThemeBrushes), "DisabledForegroundBrush");
        public static ComponentResourceKey DisabledAccentForegroundBrush => new ComponentResourceKey(typeof(ThemeBrushes), "DisabledAccentForegroundBrush");
        public static ComponentResourceKey SuccessBrush => new ComponentResourceKey(typeof(ThemeBrushes), "SuccessBrush");
        public static ComponentResourceKey ErrorBrush => new ComponentResourceKey(typeof(ThemeBrushes), "ErrorBrush");
        public static ComponentResourceKey AlertBrush => new ComponentResourceKey(typeof(ThemeBrushes), "AlertBrush");
        public static ComponentResourceKey HyperlinkBrush => new ComponentResourceKey(typeof(ThemeBrushes), "HyperlinkBrush");
        public static ComponentResourceKey WindowButtonHighlightBrush => new ComponentResourceKey(typeof(ThemeBrushes), "WindowButtonHighlightBrush");
        public static ComponentResourceKey WindowButtonInteractionBrush => new ComponentResourceKey(typeof(ThemeBrushes), "WindowButtonInteractionBrush");
    }

    /// <summary>Size resource keys.</summary>
    public static class ThemeDimensions
    {
        public static ComponentResourceKey CornerRadius => new ComponentResourceKey(typeof(ThemeDimensions), "CornerRadius");
        public static ComponentResourceKey BorderThickness => new ComponentResourceKey(typeof(ThemeDimensions), "BorderThickness");
        public static ComponentResourceKey HorizontalSpace => new ComponentResourceKey(typeof(ThemeDimensions), "HorizontalSpace");
        public static ComponentResourceKey VerticalSpace => new ComponentResourceKey(typeof(ThemeDimensions), "VerticalSpace");
    }

    /// <summary>Named control style keys.</summary>
    public static class ThemeStyles
    {
        public static ComponentResourceKey AccentButton => new ComponentResourceKey(typeof(ThemeStyles), "AccentButton");
        public static ComponentResourceKey ToggleSwitch => new ComponentResourceKey(typeof(ThemeStyles), "ToggleSwitch");
        public static ComponentResourceKey WindowButton => new ComponentResourceKey(typeof(ThemeStyles), "WindowButton");
        public static ComponentResourceKey WindowCloseButton => new ComponentResourceKey(typeof(ThemeStyles), "WindowCloseButton");
    }

    /// <summary>Data template keys (icons).</summary>
    public static class ThemeTemplates
    {
        public static ComponentResourceKey Expander => new ComponentResourceKey(typeof(ThemeTemplates), "Expander");
        public static ComponentResourceKey WindowMinimize => new ComponentResourceKey(typeof(ThemeTemplates), "WindowMinimize");
        public static ComponentResourceKey WindowMaximize => new ComponentResourceKey(typeof(ThemeTemplates), "WindowMaximize");
        public static ComponentResourceKey WindowRestore => new ComponentResourceKey(typeof(ThemeTemplates), "WindowRestore");
        public static ComponentResourceKey WindowClose => new ComponentResourceKey(typeof(ThemeTemplates), "WindowClose");
        public static ComponentResourceKey CheckMark => new ComponentResourceKey(typeof(ThemeTemplates), "CheckMark");
    }
}
