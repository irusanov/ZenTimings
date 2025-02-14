using AdonisUI;
using System;
using System.IO;
using System.Management.Instrumentation;
using System.Windows;
using System.Xml.Serialization;
using ZenStates.Core;

namespace ZenTimings
{
    [Serializable]
    public sealed class AppSettings
    {
        public const int VersionMajor = 1;
        public const int VersionMinor = 5;

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
            Version = $"{VersionMajor}.{VersionMinor}";
            AppTheme = Theme.DarkMintGradient;
            ScreenshotMode = ScreenshotType.Window;
            AutoRefresh = true;
            AutoRefreshInterval = 2000;
            AdvancedMode = true;
            CheckForUpdates = true;
            SaveWindowPosition = false;
            AutoUninstallDriver = true;
            WindowLeft = 0;
            WindowTop = 0;
            SysInfoWindowLeft = 0;
            SysInfoWindowHeight = 0;
            SysInfoWindowWidth = 0;
            NotifiedChangelog = "";
            NotifiedRembrandt = "";
            MbName = "";
            BiosVersion = "";
            SmuVersion = "";
            AgesaVersion = "";

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

                if (CpuSingleton.Instance?.systemInfo != null)
                {
                    MbName = CpuSingleton.Instance.systemInfo.MbName;
                    BiosVersion = CpuSingleton.Instance.systemInfo.BiosVersion;
                    SmuVersion = CpuSingleton.Instance.systemInfo.GetSmuVersionString();
                    AgesaVersion = CpuSingleton.Instance.systemInfo.AgesaVersion;
                }

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
                new Uri("pack://application:,,,/ZenTimings;component/Themes/DarkMintGradient.xaml", UriKind.Absolute),
                new Uri("pack://application:,,,/ZenTimings;component/Themes/DarkRed.xaml", UriKind.Absolute),
                new Uri("pack://application:,,,/ZenTimings;component/Themes/Dracula.xaml", UriKind.Absolute),
                new Uri("pack://application:,,,/ZenTimings;component/Themes/RetroWave.xaml", UriKind.Absolute),
                new Uri("pack://application:,,,/ZenTimings;component/Themes/BurntOrange.xaml", UriKind.Absolute),
            };

            ResourceLocator.SetColorScheme(Application.Current.Resources, themeUri[(int)AppTheme]);
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
        public string MbName { get; set; } = "";
        public string BiosVersion { get; set; } = "";
        public string SmuVersion { get; set; } = "";
        public string AgesaVersion { get; set; } = "";
    }
}
