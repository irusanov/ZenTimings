using AdonisUI;
using System;
using System.Windows;
using ZenTimings.Properties;

namespace ZenTimings
{
    public sealed class AppSettings
    {
        private Settings settings;

        private void Load()
        {
            try
            {
                settings = Settings.Default;
                AutoRefresh = settings.AutoRefresh;
                AutoRefreshInterval = settings.AutoRefreshInterval;
                AdvancedMode = settings.AdvancedMode;
                DarkMode = settings.DarkMode;
            }
            catch
            {
                settings = new Settings();
                AutoRefresh = true;
                AutoRefreshInterval = 2000;
                AdvancedMode = false;
                settings.AutoRefresh = AutoRefresh;
                settings.AutoRefreshInterval = AutoRefreshInterval;
                settings.AdvancedMode = AdvancedMode;
                settings.DarkMode = DarkMode;
            }
        }

        public void Save() => settings.Save();
        public void Reload() => settings.Reload();

        public AppSettings() => Load();

        public void ChangeTheme()
        {
            Uri DarkColorScheme = new Uri("pack://application:,,,/ZenTimings;component/Themes/Dark.xaml", UriKind.Absolute);
            Uri LightColorScheme = new Uri("pack://application:,,,/ZenTimings;component/Themes/Light.xaml", UriKind.Absolute);

            if (DarkMode)
                ResourceLocator.SetColorScheme(Application.Current.Resources, DarkColorScheme);
            else
                ResourceLocator.SetColorScheme(Application.Current.Resources, LightColorScheme);

            //DarkMode = !DarkMode;
        }

        public bool AutoRefresh
        {
            get => settings.AutoRefresh;
            set => settings.AutoRefresh = value;
        }
        public int AutoRefreshInterval
        {
            get => settings.AutoRefreshInterval;
            set => settings.AutoRefreshInterval = value;
        }

        public bool AdvancedMode
        {
            get => settings.AdvancedMode;
            set => settings.AdvancedMode = value;
        }

        public bool DarkMode
        {
            get => settings.DarkMode;
            set => settings.DarkMode = value;
        }

        public bool IsRestarting
        {
            get => settings.IsRestarting;
            set => settings.IsRestarting = value;
        }
    }
}
