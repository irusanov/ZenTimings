using System.Windows.Forms;
using ZenTimings.Properties;

namespace ZenTimings.Utils
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
            }
            catch
            {
                settings = new Settings();
                AutoRefresh = true;
                AutoRefreshInterval = 2000;
                settings.AutoRefresh = AutoRefresh;
                settings.AutoRefreshInterval = AutoRefreshInterval;
            }
        }

        public void Save() => settings.Save();
        public void Reload() => settings.Reload();

        public AppSettings() => Load();

        public bool AutoRefresh { 
            get => settings.AutoRefresh;
            set => settings.AutoRefresh = value;
        }
        public int AutoRefreshInterval {
            get => settings.AutoRefreshInterval; 
            set => settings.AutoRefreshInterval = value; 
        }
    }
}
