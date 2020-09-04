using AdonisUI;
using AdonisUI.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using ZenTimings;

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
                status.Content = "Advanced Mode will be applied on next launch.";
                statusStrip1.Visibility = Visibility.Visible;
            }

            //statusStrip1.Visibility = Visibility.Visible;
        }

        private void CheckBoxAdvancedMode_CheckedChanged(object sender, RoutedEventArgs e)
        {
            var main = Owner as MainWindow;
            checkBoxAutoRefresh.IsEnabled = (bool)checkBoxAdvancedMode.IsChecked;
            numericUpDownRefreshInterval.IsEnabled = (bool)checkBoxAdvancedMode.IsChecked && (bool)checkBoxAutoRefresh.IsChecked;
            main.SetWindowTitle();
        }

        private void ComboBoxTheme_Checked(object sender, RoutedEventArgs e)
        {
            settingsInstance.DarkMode = true;
            settingsInstance.ChangeTheme();
        }

        private void ComboBoxTheme_Unchecked(object sender, RoutedEventArgs e)
        {
            settingsInstance.DarkMode = false;
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
    }
}
