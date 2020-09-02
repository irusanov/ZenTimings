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

        public OptionsDialog(AppSettings settings, DispatcherTimer timer)
        {
            settingsInstance = settings;
            timerInstance = timer;

            InitializeComponent();
            InitSettings();
        }

        private void InitSettings()
        {
            /*ToolTip toolTip = new ToolTip();
            toolTip.SetToolTip(checkBoxAutoRefresh, Caption);
            toolTip.SetToolTip(numericUpDownRefreshInterval, Caption);*/

            /*comboBoxTheme.Items.Add("Light");
            comboBoxTheme.Items.Add("Dark");
            comboBoxTheme.SelectedIndex = 0;*/

            checkBoxCompactMode.IsChecked = settingsInstance.CompactMode;
            checkBoxAutoRefresh.IsChecked = settingsInstance.AutoRefresh;
            numericUpDownRefreshInterval.Text = Convert.ToString(settingsInstance.AutoRefreshInterval);
            numericUpDownRefreshInterval.IsEnabled = (bool)checkBoxAutoRefresh.IsChecked;
        }

        private void CheckBoxAutoRefresh_Click(object sender, RoutedEventArgs e)
        {
            numericUpDownRefreshInterval.IsEnabled = (bool)checkBoxAutoRefresh.IsChecked;
        }

        private void ButtonSettingsApply_Click(object sender, RoutedEventArgs e)
        {
            var currentCompactMode = settingsInstance.CompactMode;
            settingsInstance.AutoRefresh = (bool)checkBoxAutoRefresh.IsChecked;
            settingsInstance.AutoRefreshInterval = Convert.ToInt32(numericUpDownRefreshInterval.Text);
            settingsInstance.CompactMode = (bool)checkBoxCompactMode.IsChecked;
            timerInstance.Interval = TimeSpan.FromMilliseconds(settingsInstance.AutoRefreshInterval);
            settingsInstance.Save();

            if (settingsInstance.AutoRefresh && !timerInstance.IsEnabled)
                timerInstance.Start();
            else if (!settingsInstance.AutoRefresh && timerInstance.IsEnabled)
                timerInstance.Stop();

            if (currentCompactMode != settingsInstance.CompactMode)
            {
                AdonisUI.Controls.MessageBoxResult result = AdonisUI.Controls.MessageBox.Show("Settings will take effect on next app launch.\nDo you want to restart it now?", "Restart", AdonisUI.Controls.MessageBoxButton.YesNo);

                if (result == AdonisUI.Controls.MessageBoxResult.Yes)
                {
                    Properties.Settings.Default.IsRestarting = true;
                    Properties.Settings.Default.Save();
                    System.Diagnostics.Process.Start(Application.ResourceAssembly.Location);
                    Application.Current.Shutdown();
                }
            }
        }

        private void CheckBoxCompactMode_CheckedChanged(object sender, RoutedEventArgs e)
        {
            checkBoxAutoRefresh.IsEnabled = !(bool)checkBoxCompactMode.IsChecked;
            numericUpDownRefreshInterval.IsEnabled = !(bool)checkBoxCompactMode.IsChecked && (bool)checkBoxAutoRefresh.IsChecked;
        }

        private bool _isDark;
        private void ComboBoxTheme_SelectionChanged(object sender, RoutedEventArgs e)
        {
            ResourceLocator.SetColorScheme(Application.Current.Resources, _isDark ? ResourceLocator.LightColorScheme : ResourceLocator.DarkColorScheme);

            _isDark = !_isDark;
        }
    }
}
