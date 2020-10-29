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

            }
        }

        public void Save() => settings.Save();
        public void Reload() => settings.Reload();

        public AppSettings() => Load();

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
    }
}
