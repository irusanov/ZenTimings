using AdonisUI.Controls;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace ZenTimings.Windows
{
    /// <summary>
    /// Interaction logic for OptionsDialog.xaml
    /// </summary>
    public partial class OptionsDialog : AdonisWindow
    {
        private const string Caption = "Disabling auto-refresh might lead to inaccurate voltages and frequencies on first launch";
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

            checkBoxAutoRefresh.IsEnabled = settings.AdvancedMode;
            checkBoxAdvancedMode.IsChecked = settings.AdvancedMode;
            numericUpDownRefreshInterval.IsEnabled = settings.AutoRefresh && settings.AdvancedMode;
        }

        private void CheckBoxAutoRefresh_Click(object sender, RoutedEventArgs e)
        {
            numericUpDownRefreshInterval.IsEnabled = (bool)checkBoxAutoRefresh.IsChecked;
        }

        private void ButtonSettingsApply_Click(object sender, RoutedEventArgs e)
        {
            settingsInstance.AutoRefresh = (bool)checkBoxAutoRefresh.IsChecked;
            settingsInstance.AutoRefreshInterval = Convert.ToInt32(numericUpDownRefreshInterval.Text);
            settingsInstance.AdvancedMode = (bool)checkBoxAdvancedMode.IsChecked;
            timerInstance.Interval = TimeSpan.FromMilliseconds(settingsInstance.AutoRefreshInterval);
            settingsInstance.Save();

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
                myPopup.IsOpen = false;
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
                settingsInstance.IsRestarting = true;
                settingsInstance.Save();
                popupText.Text = "Advanced Mode will be applied on next launch.";
            }

            myPopup.Width = OptionWindowContent.ActualWidth;
            myPopup.IsOpen = true;
        }

        private void CheckBoxAdvancedMode_CheckedChanged(object sender, RoutedEventArgs e)
        {
            checkBoxAutoRefresh.IsEnabled = (bool)checkBoxAdvancedMode.IsChecked;
            numericUpDownRefreshInterval.IsEnabled = (bool)checkBoxAdvancedMode.IsChecked && (bool)checkBoxAutoRefresh.IsChecked;
        }

        private void ComboBoxTheme_CheckedChanged(object sender, RoutedEventArgs e)
        {
            settingsInstance.DarkMode = (bool)comboBoxTheme.IsChecked;
            settingsInstance.ChangeTheme();
        }

        private void ButtonSettingsCancel_Click(object sender, RoutedEventArgs e)
        {
            // Restore theme on close if not saved
            if (settingsInstance.DarkMode != _DarkMode)
            {
                settingsInstance.DarkMode = _DarkMode;
                settingsInstance.ChangeTheme();
            }
        }

        private void ButtonSettingsRestart_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(Application.ResourceAssembly.Location);
            Application.Current.Shutdown();
        }

        private void MyPopup_MouseDown(object sender, MouseButtonEventArgs e)
        {
            myPopup.IsOpen = false;
        }
    }
}
