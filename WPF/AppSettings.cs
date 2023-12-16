using AdonisUI;
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
        public const int VersionMinor = 2;

        private static readonly string Filename = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.xml");

        public enum Theme : int
        {
            Light,
            Dark,
            DarkMint,
        }

        public AppSettings Create()
        {
            Version = $"{VersionMajor}.{VersionMinor}";
            AppTheme = Theme.Light;
            AutoRefresh = true;
            AutoRefreshInterval = 2000;
            AdvancedMode = true;
            CheckForUpdates = true;
            SaveWindowPosition = false;
            WindowLeft = 0;
            WindowTop = 0;
            SysInfoWindowLeft = 0;
            SysInfoWindowHeight = 0;
            SysInfoWindowWidth = 0;
            NotifiedChangelog = "";
            NotifiedRembrandt = "";

            Save();

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
                using (StreamWriter sw = new StreamWriter(Filename))
                {
                    XmlSerializer xmls = new XmlSerializer(typeof(AppSettings));
                    xmls.Serialize(sw, this);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                AdonisUI.Controls.MessageBox.Show(
                    "Could not save settings to file!",
                    "Error",
                    AdonisUI.Controls.MessageBoxButton.OK,
                    AdonisUI.Controls.MessageBoxImage.Error);
            }
        }

        public void ChangeTheme()
        {
            Uri[] themeUri = new Uri[]
            {
                new Uri("pack://application:,,,/ZenTimings;component/Themes/Light.xaml", UriKind.Absolute),
                new Uri("pack://application:,,,/ZenTimings;component/Themes/Dark.xaml", UriKind.Absolute),
                new Uri("pack://application:,,,/ZenTimings;component/Themes/DarkMint.xaml", UriKind.Absolute),
            };

            ResourceLocator.SetColorScheme(Application.Current.Resources, themeUri[(int)AppTheme]);
        }

        public string Version { get; set; } = $"{VersionMajor}.{VersionMinor}";
        public bool AutoRefresh { get; set; } = true;
        public int AutoRefreshInterval { get; set; } = 2000;
        public bool AdvancedMode { get; set; } = true;
        public Theme AppTheme { get; set; } = Theme.Light;
        public bool CheckForUpdates { get; set; } = true;
        public string UpdaterSkippedVersion { get; set; } = "";
        public string UpdaterRemindLaterAt { get; set; } = "";
        public bool MinimizeToTray { get; set; }
        public bool SaveWindowPosition { get; set; }
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
