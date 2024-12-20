using System;
using System.IO;
using System.Windows;
using System.Xml.Serialization;

namespace ZenTimings
{
    [Serializable]
    public sealed class AppSettings
    {
        public const int VersionMajor = 1;
        public const int VersionMinor = 4;

        private static readonly string Filename = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.xml");

        private static AppSettings _instance = null;

        private AppSettings() { }

        public static AppSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new AppSettings().Load();
                }

                return _instance;
            }
        }

        public enum Theme : int
        {
            Light,
            Dark,
            DarkMint,
            DarkMintGradient,
            DarkRed,
            Dracula,
            RetroWave,
            BurntOrange,
        }

        public enum ScreenshotType : int
        {
            Window,
            Desktop,
        }

        public AppSettings Create(bool save = true)
        {
            if (save) Save();

            return this;
        }

        public AppSettings Reset() => Create();

        public AppSettings Load()
        {
            try
            {
                if (File.Exists(Filename))
                {
                    using (StreamReader sr = new StreamReader(Filename))
                    {
                        XmlSerializer xmls = new XmlSerializer(typeof(AppSettings));
                        return xmls.Deserialize(sr) as AppSettings;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                MessageBox.Show(
                    "Invalid or outdated settings file!\nSettings will be reset to defaults.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }

            return Create();
        }

        public void Save()
        {
            try
            {
                Version = $"{VersionMajor}.{VersionMinor}";
                using (StreamWriter sw = new StreamWriter(Filename))
                {
                    XmlSerializer xmls = new XmlSerializer(typeof(AppSettings));
                    xmls.Serialize(sw, this);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                MessageBox.Show(
                    "Could not save settings to file!",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        public string Version { get; set; } = $"{VersionMajor}.{VersionMinor}";
        public bool AutoRefresh { get; set; } = true;
        public int AutoRefreshInterval { get; set; } = 2000;
        public bool AdvancedMode { get; set; } = true;
        public Theme AppTheme { get; set; } = Theme.DarkMintGradient;
        public ScreenshotType ScreenshotMode { get; set; } = ScreenshotType.Window;
        public bool CheckForUpdates { get; set; } = true;
        public string UpdaterSkippedVersion { get; set; } = "";
        public string UpdaterRemindLaterAt { get; set; } = "";
        public bool MinimizeToTray { get; set; }
        public bool SaveWindowPosition { get; set; }
        public bool AutoUninstallDriver { get; set; } = true;
        public double WindowLeft { get; set; }
        public double WindowTop { get; set; }
        public double SysInfoWindowLeft { get; set; }
        public double SysInfoWindowTop { get; set; }
        public double SysInfoWindowWidth { get; set; }
        public double SysInfoWindowHeight { get; set; }
        public string NotifiedChangelog { get; set; } = "";
        public string NotifiedRembrandt { get; set; } = "";
    }
}
