using AdonisUI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using ZenTimings.Helpers;
using ZenTimings.Utils;
using static ZenTimings.Helpers.DriverCleaner;

namespace ZenTimings.Settings
{
    [Serializable]
    public sealed class AppSettings : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public const int VersionMajor = 1;
        public const int VersionMinor = 16;

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
            AsusRog,
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

        public enum ImpedanceTableSource : int
        {
            AOD,
            APOB
        }

        public enum VoltageSensorSource : int
        {
            SuperIo,
            Smu,
            Aod
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
                    return XmlUtils.DeserializeFromXmlFile<AppSettings>(Filename);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                MessageBox.Show(
                    "Invalid or outdated settings file!\nSettings will be reset to defaults and any custom settings will be lost.",
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
                Debug.WriteLine(ex.Message);
                AdonisUI.Controls.MessageBox.Show(
                    "Could not save settings to file!",
                    "Error",
                    AdonisUI.Controls.MessageBoxButton.OK,
                    AdonisUI.Controls.MessageBoxImage.Error);
            }
        }

        private static Uri ThemeUri(string name) =>
            new Uri("pack://application:,,,/ZenTimings;component/Themes/" + name + ".xaml", UriKind.Absolute);

        // Maps each theme by enum value (not by index) to its resource dictionary.
        private static Uri GetThemeUri(Theme theme)
        {
            switch (theme)
            {
                case Theme.Light: return ThemeUri("Light");
                case Theme.Dark: return ThemeUri("Dark");
                case Theme.DarkMint: return ThemeUri("DarkMint");
                case Theme.DarkMintGradient: return ThemeUri("DarkMintGradient");
                case Theme.AsusRog: return ThemeUri("AsusRog");
                case Theme.Dracula: return ThemeUri("Dracula");
                case Theme.RetroWave: return ThemeUri("RetroWave");
                case Theme.BurntOrange: return ThemeUri("BurntOrange");
                // Charcoal.xaml is not part of the build. Previous builds mapped the
                // stored "Charcoal" value (selected via the "Black" combo item) to Black.xaml.
                case Theme.Charcoal: return ThemeUri("Black");
                case Theme.Black: return ThemeUri("Black");
                default: return ThemeUri("DarkMintGradient");
            }
        }

        public void ApplyTheme()
        {
            try
            {
                ResourceLocator.SetColorScheme(Application.Current.Resources, GetThemeUri(AppTheme));
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);

                try
                {
                    ResourceLocator.SetColorScheme(Application.Current.Resources, GetThemeUri(Theme.DarkMintGradient));
                }
                catch (Exception fallbackEx)
                {
                    Debug.WriteLine(fallbackEx.Message);
                }
            }

            try
            {
                ThemedAdonisWindow.RefreshAllOpenWindows();
            }
            catch { }
        }

        public string Version { get; set; } = new Version(VersionMajor, VersionMinor).ToString();

        private bool _autoRefresh = true;
        public bool AutoRefresh
        {
            get => _autoRefresh;
            set
            {
                if (_autoRefresh != value)
                {
                    _autoRefresh = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AutoRefresh)));
                }
            }
        }

        private int _autoRefreshInterval = 2000;
        public int AutoRefreshInterval
        {
            get => _autoRefreshInterval;
            set
            {
                if (_autoRefreshInterval != value)
                {
                    _autoRefreshInterval = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AutoRefreshInterval)));
                }
            }
        }
        public bool AdvancedMode { get; set; } = true;
        public Theme AppTheme { get; set; } = Theme.DarkMintGradient;
        public ScreenshotType ScreenshotMode { get; set; } = ScreenshotType.Window;
        public string ScreenshotSaveLocation { get; set; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Screenshots");
        public bool CheckForUpdates { get; set; } = true;
#if BETA
        public bool ParticipateInBetaUpdates { get; set; } = true;
#else
        public bool ParticipateInBetaUpdates { get; set; } = false;
#endif
        public string UpdaterSkippedVersion { get; set; } = "";
        public string DriverUpdateLastSkippedVersion { get; set; } = "";
        public string UpdaterRemindLaterAt { get; set; } = "";
        public bool MinimizeToTray { get; set; }
        public bool SaveWindowPosition { get; set; } = true;
        public bool EnableWindowSnapping { get; set; } = true;
        public string WindowSnapStates { get; set; } = "";
        public bool AutostartWithWindows { get; set; }
        public int AutostartDelaySeconds { get; set; } = 12;
        public bool StartMinimized { get; set; }
        public bool AutoUninstallDriver { get; set; } = true;
        public int AutoUninstallDriverNotificationLevel { get; set; } = (int)NotificationLevel.All;
        public double WindowLeft { get; set; } = -1;
        public double WindowTop { get; set; } = -1;
        public double SysInfoWindowLeft { get; set; } = -1;
        public double SysInfoWindowTop { get; set; } = -1;
        public double SysInfoWindowWidth { get; set; }
        public double SysInfoWindowHeight { get; set; }
        public double SensorsWindowLeft { get; set; } = -1;
        public double SensorsWindowTop { get; set; } = -1;
        public double SensorsWindowWidth { get; set; }
        public double SensorsWindowHeight { get; set; }
        public string NotifiedChangelog { get; set; } = "";
        public bool SingleInstance { get; set; } = true;
        public bool AutoOpenTelemetry { get; set; } = false;
        public bool FirstStart { get; set; } = true;
        public int CornerRadius { get; set; } = 0;
        public ImpedanceTableSource ImpedanceTableSrc { get; set; } = ImpedanceTableSource.APOB;
        public VoltageSensorSource VsocSensorSource { get; set; } = VoltageSensorSource.SuperIo;
        public VoltageSensorSource VddioSensorSource { get; set; } = VoltageSensorSource.SuperIo;
        public VoltageSensorSource VmiscSensorSource { get; set; } = VoltageSensorSource.SuperIo;

        public string GetWindowSnapTarget(string windowId)
        {
            if (string.IsNullOrWhiteSpace(windowId) || string.IsNullOrWhiteSpace(WindowSnapStates))
                return null;

            foreach (var entry in WindowSnapStates.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = entry.Split(new[] { '=' }, 2);
                if (parts.Length == 2 && string.Equals(parts[0], windowId, StringComparison.OrdinalIgnoreCase))
                    return string.IsNullOrWhiteSpace(parts[1]) ? null : parts[1];
            }

            return null;
        }

        public void SetWindowSnapTarget(string windowId, string targetId)
        {
            if (string.IsNullOrWhiteSpace(windowId))
                return;

            var entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(WindowSnapStates))
            {
                foreach (var entry in WindowSnapStates.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = entry.Split(new[] { '=' }, 2);
                    if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[0]))
                        entries[parts[0]] = parts[1];
                }
            }

            if (string.IsNullOrWhiteSpace(targetId))
                entries.Remove(windowId);
            else
                entries[windowId] = targetId;

            WindowSnapStates = string.Join(";", entries.Select(x => $"{x.Key}={x.Value}"));
        }
    }
}
