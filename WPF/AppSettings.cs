using AdonisUI;
using System;
using System.IO;
using System.Windows;

namespace ZenTimings
{
    [Serializable]
    public sealed class AppSettings
    {
        public const int VersionMajor = 1;
        public const int VersionMinor = 11;

        private static readonly string Filename = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.xml");
        public const string AGESA_UNKNOWN = "Unknown";

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
            Charcoal,
            Black,
        }

        public enum ScreenshotType : int
        {
            Window,
            Desktop,
        }

        public enum ImpedanceTableSource: int
        {
            AOD,
            APOB
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
                    return XmlUtils.DeserializeFromXml<AppSettings>(Filename);
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
                if (!DriverHelper.IsPawnIoInstalled)
                    return;

                Version = new Version(VersionMajor, VersionMinor).ToString();

                string xmlContent = XmlUtils.SerializeToXml<AppSettings>(this);
                File.WriteAllText(Filename, xmlContent);
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
                new Uri("pack://application:,,,/ZenTimings;component/Themes/DarkMintGradient.xaml", UriKind.Absolute),
                new Uri("pack://application:,,,/ZenTimings;component/Themes/DarkRed.xaml", UriKind.Absolute),
                new Uri("pack://application:,,,/ZenTimings;component/Themes/Dracula.xaml", UriKind.Absolute),
                new Uri("pack://application:,,,/ZenTimings;component/Themes/RetroWave.xaml", UriKind.Absolute),
                new Uri("pack://application:,,,/ZenTimings;component/Themes/BurntOrange.xaml", UriKind.Absolute),
                //new Uri("pack://application:,,,/ZenTimings;component/Themes/Charcoal.xaml", UriKind.Absolute),
                new Uri("pack://application:,,,/ZenTimings;component/Themes/Black.xaml", UriKind.Absolute),
            };

            ResourceLocator.SetColorScheme(Application.Current.Resources, themeUri[(int)AppTheme]);
        }

        public string Version { get; set; } = new Version(VersionMajor, VersionMinor).ToString();
        public bool AutoRefresh { get; set; } = true;
        public int AutoRefreshInterval { get; set; } = 2000;
        public bool AdvancedMode { get; set; } = true;
        public Theme AppTheme { get; set; } = Theme.DarkMintGradient;
        public ScreenshotType ScreenshotMode { get; set; } = ScreenshotType.Window;
        public string ScreenshotSaveLocation { get; set; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Screenshots");
        public bool CheckForUpdates { get; set; } = true;
        public string UpdaterSkippedVersion { get; set; } = "";
        public string DriverUpdateLastSkippedVersion { get; set; } = "";
        public string UpdaterRemindLaterAt { get; set; } = "";
        public bool MinimizeToTray { get; set; }
        public bool SaveWindowPosition { get; set; } = true;
        public bool AutoUninstallDriver { get; set; } = true;
        public double WindowLeft { get; set; } = -1;
        public double WindowTop { get; set; } = -1;
        public double SysInfoWindowLeft { get; set; } = -1;
        public double SysInfoWindowTop { get; set; } = -1;
        public double SysInfoWindowWidth { get; set; }
        public double SysInfoWindowHeight { get; set; }
        public string NotifiedChangelog { get; set; } = "";
        public bool SingleInstance { get; set; } = true;
        public ImpedanceTableSource ImpedanceTableSrc { get; set; } = ImpedanceTableSource.APOB;
    }
}
