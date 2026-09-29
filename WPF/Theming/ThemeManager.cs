using System;
using System.Linq;
using System.Windows;

namespace ZenTimings.Theming
{
    /// <summary>
    /// Switches the active theme: a resource dictionary with the colors and theme specific styles, which is
    /// merged into the application resources on top of the control styles.
    /// </summary>
    public static class ThemeManager
    {
        private static ResourceDictionary currentTheme;

        public static Uri CurrentThemeUri => currentTheme?.Source;

        public static void SetTheme(Uri themeUri)
        {
            if (themeUri == null)
                throw new ArgumentNullException(nameof(themeUri));

            Application app = Application.Current;
            if (app == null)
                return;

            var dictionaries = app.Resources.MergedDictionaries;
            ResourceDictionary previous = currentTheme ?? dictionaries.LastOrDefault(IsThemeDictionary);

            // Add the new theme before removing the old one, so no resource is missing in between
            // (every missing resource is looked up again and traced, which is slow).
            var theme = new ResourceDictionary { Source = themeUri };
            dictionaries.Add(theme);

            if (previous != null)
                dictionaries.Remove(previous);

            currentTheme = theme;
        }

        // A theme merged by XAML (not through SetTheme) is recognized by its location.
        private static bool IsThemeDictionary(ResourceDictionary dictionary)
        {
            string path = dictionary.Source?.OriginalString;
            if (string.IsNullOrEmpty(path) || path.IndexOf("/Themes/", StringComparison.OrdinalIgnoreCase) < 0)
                return false;

            return path.IndexOf("/Themes/Base.xaml", StringComparison.OrdinalIgnoreCase) < 0 &&
                   path.IndexOf("/Themes/AppIcons.xaml", StringComparison.OrdinalIgnoreCase) < 0;
        }
    }
}
