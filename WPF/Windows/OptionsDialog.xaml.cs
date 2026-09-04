using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ZenStates.Core.Hardware;
using ZenTimings.Helpers;
using static ZenTimings.AppSettings;

namespace ZenTimings.Windows
{
    /// <summary>
    /// Interaction logic for OptionsDialog.xaml
    /// </summary>
    public partial class OptionsDialog : ThemedAdonisWindow
    {
        internal readonly AppSettings appSettings = AppSettings.Instance;
        internal readonly SystemInfo _systemInfo = CpuSingleton.Instance.systemInfo;
        private readonly DispatcherTimer timerInstance;
        private DispatcherTimer notificationTimer;
        private Theme _Theme;
        private readonly bool _AdvancedMode;
        private readonly ImpedanceTableSource _ImpedanceTableSource;
        private readonly int _CornerRadius;

        public OptionsDialog(DispatcherTimer timer)
        {
            timerInstance = timer;
            _Theme = appSettings.AppTheme;
            _AdvancedMode = appSettings.AdvancedMode;
            _ImpedanceTableSource = appSettings.ImpedanceTableSrc;
            _CornerRadius = appSettings.CornerRadius;

            InitializeComponent();

            LoadSettingsToUi();
        }

        private void LoadSettingsToUi()
        {
            checkBoxAutoRefresh.IsChecked = appSettings.AutoRefresh;
            checkBoxAutoRefresh.IsEnabled = appSettings.AdvancedMode;
            checkBoxAdvancedMode.IsChecked = appSettings.AdvancedMode;
            checkBoxCheckUpdate.IsChecked = appSettings.CheckForUpdates;
            checkBoxBetaUpdates.IsChecked = appSettings.ParticipateInBetaUpdates;
            checkBoxSavePosition.IsChecked = appSettings.SaveWindowPosition;
            checkBoxMinimizeToTray.IsChecked = appSettings.MinimizeToTray;
            checkBoxAutostart.IsChecked = appSettings.AutostartWithWindows;
            numericUpDownAutostartDelay.IsEnabled = appSettings.AutostartWithWindows;
            numericUpDownAutostartDelay.Text = appSettings.AutostartDelaySeconds.ToString();
            checkBoxStartMinimized.IsChecked = appSettings.StartMinimized;
            checkBoxSingleInstance.IsChecked = appSettings.SingleInstance;
            comboBoxCornerRadius.SelectedIndex = appSettings?.CornerRadius ?? 0;
            numericUpDownRefreshInterval.IsEnabled = appSettings.AutoRefresh && appSettings.AdvancedMode;
            numericUpDownRefreshInterval.Text = appSettings.AutoRefreshInterval.ToString();
            msText.IsEnabled = numericUpDownRefreshInterval.IsEnabled;
            comboBoxTheme.SelectedIndex = (int)_Theme;
            comboBoxScreenshot.SelectedIndex = (int)appSettings.ScreenshotMode;
            comboBoxImpedanceSource.SelectedIndex = (int)appSettings.ImpedanceTableSrc;
            textBoxScreenshotPath.Text = appSettings.ScreenshotSaveLocation;
            checkBoxAutoUninstallDriver.IsChecked = appSettings.AutoUninstallDriver;
            var notificationLevelIndex = appSettings.AutoUninstallDriverNotificationLevel + 1;
            if (notificationLevelIndex > comboBoxDriverNotification.Items.Count - 1)
                notificationLevelIndex = comboBoxDriverNotification.Items.Count - 1;
            comboBoxDriverNotification.SelectedIndex = notificationLevelIndex;
        }

        private void SaveSettingsFromUi()
        {
            appSettings.AutoRefresh = (bool)checkBoxAutoRefresh.IsChecked;
            appSettings.AutoRefreshInterval = Convert.ToInt32(numericUpDownRefreshInterval.Text);
            appSettings.AdvancedMode = (bool)checkBoxAdvancedMode.IsChecked;
            appSettings.CheckForUpdates = (bool)checkBoxCheckUpdate.IsChecked;
            appSettings.ParticipateInBetaUpdates = (bool)checkBoxBetaUpdates.IsChecked;
            appSettings.SaveWindowPosition = (bool)checkBoxSavePosition.IsChecked;
            appSettings.MinimizeToTray = (bool)checkBoxMinimizeToTray.IsChecked;
            appSettings.AutostartWithWindows = (bool)checkBoxAutostart.IsChecked;
            appSettings.AutostartDelaySeconds = Convert.ToInt32(numericUpDownAutostartDelay.Text);
            appSettings.StartMinimized = (bool)checkBoxStartMinimized.IsChecked;
            StartupHelper.SetAutostart(appSettings.AutostartWithWindows, appSettings.AutostartDelaySeconds);
            appSettings.SingleInstance = (bool)checkBoxSingleInstance.IsChecked;
            appSettings.CornerRadius = comboBoxCornerRadius.SelectedIndex;
            appSettings.ScreenshotMode = (ScreenshotType)comboBoxScreenshot.SelectedIndex;
            appSettings.ScreenshotSaveLocation = textBoxScreenshotPath.Text.Trim();
            appSettings.ImpedanceTableSrc = (ImpedanceTableSource)comboBoxImpedanceSource.SelectedIndex;
            appSettings.AutoUninstallDriver = (bool)checkBoxAutoUninstallDriver.IsChecked;
            appSettings.AutoUninstallDriverNotificationLevel = comboBoxDriverNotification.SelectedIndex - 1;
        }

        private void CheckBoxAutoRefresh_Click(object sender, RoutedEventArgs e)
        {
            numericUpDownRefreshInterval.IsEnabled = (bool)checkBoxAutoRefresh.IsChecked;
            msText.IsEnabled = numericUpDownRefreshInterval.IsEnabled;
        }

        private void CheckBoxAutostart_Click(object sender, RoutedEventArgs e)
        {
            numericUpDownAutostartDelay.IsEnabled = (bool)checkBoxAutostart.IsChecked;
        }

        private void CheckBoxAdvancedMode_Click(object sender, RoutedEventArgs e)
        {
            checkBoxAutoRefresh.IsEnabled = (bool)checkBoxAdvancedMode.IsChecked;
            numericUpDownRefreshInterval.IsEnabled =
                (bool)checkBoxAutoRefresh.IsChecked && checkBoxAutoRefresh.IsEnabled;
            msText.IsEnabled = numericUpDownRefreshInterval.IsEnabled;
        }

        private void ButtonSettingsApply_Click(object sender, RoutedEventArgs e)
        {
            SaveSettingsFromUi();
            appSettings.Save();

            timerInstance.Interval = TimeSpan.FromMilliseconds(appSettings.AutoRefreshInterval);
            _Theme = appSettings.AppTheme;

            if (checkBoxAutoRefresh.IsEnabled)
            {
                if (appSettings.AutoRefresh && !timerInstance.IsEnabled)
                    timerInstance.Start();
                else if (!appSettings.AutoRefresh && timerInstance.IsEnabled)
                    timerInstance.Stop();
            }

            var restartRequired = _AdvancedMode != appSettings.AdvancedMode ||
                                   _ImpedanceTableSource != appSettings.ImpedanceTableSrc ||
                                   _CornerRadius != appSettings.CornerRadius;

            if (restartRequired)
            {
                buttonSettingsRestart.Visibility = Visibility.Visible;
                appSettings.Save();
                OptionsPopupText.Text = "Some settings will be applied on next launch.";
            }

            ShowSavedNotification();
        }

        private void ShowSavedNotification()
        {
            if (notificationTimer != null && notificationTimer.IsEnabled)
                notificationTimer.Stop();

            notificationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(6000)
            };

            notificationTimer.Tick += (s, x) =>
            {
                notificationTimer.Stop();
                OptionsPopup.IsOpen = false;
            };

            notificationTimer.Start();

            OptionsPopup.Width = OptionWindowContent.ActualWidth;
            OptionsPopup.IsOpen = true;
        }

        private void ButtonSettingsCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ButtonSettingsRestart_Click(object sender, RoutedEventArgs e)
        {
            var info = new ProcessStartInfo
            {
                Arguments = "/C choice /C Y /N /D Y /T 1 & START \"\" \"" + Assembly.GetEntryAssembly().Location + "\"",
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                FileName = "cmd.exe"
            };
            Process.Start(info);
            Application.Current.Shutdown();
        }

        private void OptionsPopup_MouseDown(object sender, MouseButtonEventArgs e)
        {
            OptionsPopup.IsOpen = false;
        }

        private void OptionsWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Restore theme on close if not saved
            if (appSettings.AppTheme != _Theme)
            {
                appSettings.AppTheme = _Theme;
                appSettings.ApplyTheme();
            }
        }

        private void ComboBoxTheme_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            appSettings.AppTheme = (Theme)comboBoxTheme.SelectedIndex;
            appSettings.ApplyTheme();
        }

        private void ButtonBrowseScreenshotPath_Click(object sender, RoutedEventArgs e)
        {
            var folderDialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select folder to save screenshots"
            };

            // Set initial path to current screenshot path if valid
            string currentPath = textBoxScreenshotPath.Text.Trim();
            if (!string.IsNullOrEmpty(currentPath) && System.IO.Directory.Exists(currentPath))
            {
                folderDialog.SelectedPath = currentPath;
            }

            if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                textBoxScreenshotPath.Text = folderDialog.SelectedPath;
            }
        }
    }
}
