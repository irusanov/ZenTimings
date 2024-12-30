using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using static ZenTimings.AppSettings;

namespace ZenTimings.Windows
{
    /// <summary>
    /// Interaction logic for OptionsDialog.xaml
    /// </summary>
    public partial class OptionsDialog
    {
        //private const string Caption = "Disabling auto-refresh might lead to inaccurate voltages and frequencies on first launch";
        internal readonly AppSettings appSettings = AppSettings.Instance;
        private readonly DispatcherTimer timerInstance;
        private DispatcherTimer notificationTimer;
        private Theme _Theme;
        private readonly bool _AdvancedMode;

        public OptionsDialog(DispatcherTimer timer)
        {
            timerInstance = timer;
            _Theme = appSettings.AppTheme;
            _AdvancedMode = appSettings.AdvancedMode;

            InitializeComponent();

            checkBoxAutoRefresh.IsChecked = appSettings.AutoRefresh;
            checkBoxAutoRefresh.IsEnabled = appSettings.AdvancedMode;
            checkBoxAdvancedMode.IsChecked = appSettings.AdvancedMode;
            checkBoxCheckUpdate.IsChecked = appSettings.CheckForUpdates;
            checkBoxSavePosition.IsChecked = appSettings.SaveWindowPosition;
            checkBoxMinimizeToTray.IsChecked = appSettings.MinimizeToTray;
            //checkBoxAutoUninstallDriver.IsChecked = appSettings.AutoUninstallDriver;
            numericUpDownRefreshInterval.IsEnabled = appSettings.AutoRefresh && appSettings.AdvancedMode;
            numericUpDownRefreshInterval.Text = appSettings.AutoRefreshInterval.ToString();
            msText.IsEnabled = numericUpDownRefreshInterval.IsEnabled;
            comboBoxTheme.SelectedIndex = (int)_Theme;
            comboBoxScreenshot.SelectedIndex = (int)appSettings.ScreenshotMode;
        }

        private void CheckBoxAutoRefresh_Click(object sender, RoutedEventArgs e)
        {
            numericUpDownRefreshInterval.IsEnabled = (bool)checkBoxAutoRefresh.IsChecked;
            msText.IsEnabled = numericUpDownRefreshInterval.IsEnabled;
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
            appSettings.AutoRefresh = (bool)checkBoxAutoRefresh.IsChecked;
            appSettings.AutoRefreshInterval = Convert.ToInt32(numericUpDownRefreshInterval.Text);
            appSettings.AdvancedMode = (bool)checkBoxAdvancedMode.IsChecked;
            appSettings.CheckForUpdates = (bool)checkBoxCheckUpdate.IsChecked;
            appSettings.SaveWindowPosition = (bool)checkBoxSavePosition.IsChecked;
            appSettings.MinimizeToTray = (bool)checkBoxMinimizeToTray.IsChecked;
            //appSettings.AutoUninstallDriver = (bool)checkBoxAutoUninstallDriver.IsChecked;
            appSettings.ScreenshotMode = (ScreenshotType)comboBoxScreenshot.SelectedIndex;

            appSettings.Save();

            timerInstance.Interval = TimeSpan.FromMilliseconds(appSettings.AutoRefreshInterval);
            _Theme = appSettings.AppTheme;

            if (notificationTimer != null)
                if (notificationTimer.IsEnabled)
                    notificationTimer.Stop();

            notificationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(6000)
            };

            notificationTimer.Tick += new EventHandler((s, x) =>
            {
                notificationTimer.Stop();
                OptionsPopup.IsOpen = false;
            });

            notificationTimer.Start();

            if (checkBoxAutoRefresh.IsEnabled)
            {
                if (appSettings.AutoRefresh && !timerInstance.IsEnabled)
                    timerInstance.Start();
                else if (!appSettings.AutoRefresh && timerInstance.IsEnabled)
                    timerInstance.Stop();
            }

            if (_AdvancedMode != appSettings.AdvancedMode)
            {
                buttonSettingsRestart.Visibility = Visibility.Visible;
                appSettings.Save();
                OptionsPopupText.Text = "Advanced Mode will be applied on next launch.";
            }

            OptionsPopup.Width = OptionWindowContent.ActualWidth;
            OptionsPopup.IsOpen = true;
        }

        private void ComboBoxTheme_CheckedChanged(object sender, RoutedEventArgs e)
        {
            //appSettings.DarkMode = (bool)comboBoxTheme.IsChecked;
            //appSettings.ChangeTheme();
        }

        private void ButtonSettingsCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ButtonSettingsRestart_Click(object sender, RoutedEventArgs e)
        {
            ProcessStartInfo Info = new ProcessStartInfo
            {
                Arguments = "/C choice /C Y /N /D Y /T 1 & START \"\" \"" + Assembly.GetEntryAssembly().Location + "\"",
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                FileName = "cmd.exe"
            };
            Process.Start(Info);
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
                appSettings.ChangeTheme();
            }
        }

        private void ComboBoxTheme_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            appSettings.AppTheme = (Theme)comboBoxTheme.SelectedIndex;
            appSettings.ChangeTheme();
        }
    }
}