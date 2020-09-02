
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
                CompactMode = settings.CompactMode;
            }
            catch
            {
                settings = new Settings();
                AutoRefresh = true;
                AutoRefreshInterval = 2000;
                CompactMode = false;
                settings.AutoRefresh = AutoRefresh;
                settings.AutoRefreshInterval = AutoRefreshInterval;
                settings.CompactMode = CompactMode;
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

        public bool CompactMode
        {
            get => settings.CompactMode;
            set => settings.CompactMode = value;
        }
    }
}
