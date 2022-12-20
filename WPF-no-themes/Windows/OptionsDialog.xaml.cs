using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace ZenTimings.Windows
{
    /// <summary>
    /// Interaction logic for OptionsDialog.xaml
    /// </summary>
    public partial class OptionsDialog
    {
        //private const string Caption = "Disabling auto-refresh might lead to inaccurate voltages and frequencies on first launch";
        internal readonly AppSettings appSettings = (Application.Current as App)?.settings;
        private readonly DispatcherTimer timerInstance;
        private DispatcherTimer notificationTimer;
        private bool _DarkMode;
        private readonly bool _AdvancedMode;

        public OptionsDialog(DispatcherTimer timer)
        {
            timerInstance = timer;
            _DarkMode = appSettings.DarkMode;
            _AdvancedMode = appSettings.AdvancedMode;

            InitializeComponent();

            checkBoxAutoRefresh.IsChecked = appSettings.AutoRefresh;
            checkBoxAutoRefresh.IsEnabled = appSettings.AdvancedMode;
            checkBoxAdvancedMode.IsChecked = appSettings.AdvancedMode;
            checkBoxCheckUpdate.IsChecked = appSettings.CheckForUpdates;
            checkBoxSavePosition.IsChecked = appSettings.SaveWindowPosition;
            checkBoxMinimizeToTray.IsChecked = appSettings.MinimizeToTray;
            numericUpDownRefreshInterval.IsEnabled = appSettings.AutoRefresh && appSettings.AdvancedMode;
            numericUpDownRefreshInterval.Text = appSettings.AutoRefreshInterval.ToString();
            msText.IsEnabled = numericUpDownRefreshInterval.IsEnabled;
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

            appSettings.Save();

            timerInstance.Interval = TimeSpan.FromMilliseconds(appSettings.AutoRefreshInterval);
            _DarkMode = appSettings.DarkMode;


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

        private void ButtonSettingsCancel_Click(object sender, RoutedEventArgs e)
        {
            // Restore theme on close if not saved
            if (appSettings.DarkMode != _DarkMode)
            {
                appSettings.DarkMode = _DarkMode;

            }
        }

        private void ButtonSettingsRestart_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(Application.ResourceAssembly.Location);
            Application.Current.Shutdown();
        }

        private void OptionsPopup_MouseDown(object sender, MouseButtonEventArgs e)
        {
            OptionsPopup.IsOpen = false;
        }
    }
}