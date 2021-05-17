using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace ZenTimings.Windows
{
    /// <summary>
    /// Interaction logic for OptionsDialog.xaml
    /// </summary>
    public partial class OptionsDialog : Window
    {
        //private const string Caption = "Disabling auto-refresh might lead to inaccurate voltages and frequencies on first launch";
        private readonly AppSettings settingsInstance;
        private readonly DispatcherTimer timerInstance;
        private DispatcherTimer notificationTimer;
        private bool _DarkMode;
        private bool _AdvancedMode;

        public OptionsDialog(AppSettings settings, DispatcherTimer timer)
        {
            settingsInstance = settings;
            timerInstance = timer;
            DataContext = settingsInstance;
            _DarkMode = settingsInstance.DarkMode;
            _AdvancedMode = settingsInstance.AdvancedMode;

            InitializeComponent();

            checkBoxAutoRefresh.IsChecked = settings.AutoRefresh;
            checkBoxAutoRefresh.IsEnabled = settings.AdvancedMode;
            checkBoxAdvancedMode.IsChecked = settings.AdvancedMode;
            checkBoxCheckUpdate.IsChecked = settings.CheckForUpdates;
            numericUpDownRefreshInterval.IsEnabled = settings.AutoRefresh && settings.AdvancedMode;
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
            numericUpDownRefreshInterval.IsEnabled = (bool)checkBoxAutoRefresh.IsChecked && checkBoxAutoRefresh.IsEnabled;
            msText.IsEnabled = numericUpDownRefreshInterval.IsEnabled;
        }

        private void ButtonSettingsApply_Click(object sender, RoutedEventArgs e)
        {
            settingsInstance.AutoRefresh = (bool)checkBoxAutoRefresh.IsChecked;
            settingsInstance.AutoRefreshInterval = Convert.ToInt32(numericUpDownRefreshInterval.Text);
            settingsInstance.AdvancedMode = (bool)checkBoxAdvancedMode.IsChecked;
            settingsInstance.CheckForUpdates = (bool)checkBoxCheckUpdate.IsChecked;

            settingsInstance.Save();

            timerInstance.Interval = TimeSpan.FromMilliseconds(settingsInstance.AutoRefreshInterval);
            _DarkMode = settingsInstance.DarkMode;


            if (notificationTimer != null)
            {
                if (notificationTimer.IsEnabled) notificationTimer.Stop();
            }

            notificationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(6000)
            };

            notificationTimer.Tick += new EventHandler((s, x) =>
            {
                notificationTimer.Stop();
                optionsPopup.IsOpen = false;
            });

            notificationTimer.Start();

            if (checkBoxAutoRefresh.IsEnabled)
            {
                if (settingsInstance.AutoRefresh && !timerInstance.IsEnabled)
                    timerInstance.Start();
                else if (!settingsInstance.AutoRefresh && timerInstance.IsEnabled)
                    timerInstance.Stop();
            }

            if (_AdvancedMode != settingsInstance.AdvancedMode)
            {
                buttonSettingsRestart.Visibility = Visibility.Visible;
                settingsInstance.Save();
                optionsPopupText.Text = "Advanced Mode will be applied on next launch.";
            }

            optionsPopup.Width = OptionWindowContent.ActualWidth;
            optionsPopup.IsOpen = true;
        }

        private void ButtonSettingsCancel_Click(object sender, RoutedEventArgs e)
        {
            // Restore theme on close if not saved
            if (settingsInstance.DarkMode != _DarkMode)
            {
                settingsInstance.DarkMode = _DarkMode;
            }
        }

        private void ButtonSettingsRestart_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(Application.ResourceAssembly.Location);
            Application.Current.Shutdown();
        }

        private void OptionsPopup_MouseDown(object sender, MouseButtonEventArgs e)
        {
            optionsPopup.IsOpen = false;
        }
    }
}
