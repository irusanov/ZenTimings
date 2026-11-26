using System.Globalization;
using System.Resources;
using ZenTimings.Settings;

namespace ZenTimings.Localization
{
    internal static class UiText
    {
        private static readonly ResourceManager defaultStrings =
            new ResourceManager("ZenTimings.Localization.Strings", typeof(UiText).Assembly);

        // Both sets of strings ship in ZenTimings.exe, including in portable releases.
        private static readonly ResourceManager simplifiedChineseStrings =
            new ResourceManager("ZenTimings.Localization.Strings.zh-CN", typeof(UiText).Assembly);

        private static readonly CultureInfo systemLanguage = CultureInfo.CurrentUICulture;
        private static bool useSimplifiedChinese = IsSimplifiedChinese(systemLanguage);

        internal static AppSettings.LanguagePreference Language { get; private set; }

        internal static void SetLanguage(AppSettings.LanguagePreference language)
        {
            Language = language;
            useSimplifiedChinese = language == AppSettings.LanguagePreference.SimplifiedChinese ||
                (language == AppSettings.LanguagePreference.System && IsSimplifiedChinese(systemLanguage));
        }

        internal static string Get(string key)
        {
            CultureInfo culture = CultureInfo.InvariantCulture;
            string translation = useSimplifiedChinese ? simplifiedChineseStrings.GetString(key, culture) : null;
            return translation ?? defaultStrings.GetString(key, culture) ?? key;
        }

        internal static string Format(string key, params object[] args) =>
            string.Format(CultureInfo.CurrentCulture, Get(key), args);

        private static bool IsSimplifiedChinese(CultureInfo culture)
        {
            while (!culture.Equals(CultureInfo.InvariantCulture))
            {
                if (culture.Name == "zh-CN" || culture.Name == "zh-Hans")
                    return true;

                culture = culture.Parent;
            }

            return false;
        }
    }
}
