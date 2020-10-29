using System;
using System.Windows.Forms;
using ZenTimings.Utils;

namespace ZenTimings
{
    public partial class OptionsDialog : Form
    {
        private const string Caption = "Disabling auto-refresh might lead to inaccurate voltages and frequencies on first launch";
        private readonly AppSettings settingsInstance;
        private readonly Timer timerInstance;

        public OptionsDialog(AppSettings settings, Timer timer)
        {
            settingsInstance = settings;
            timerInstance = timer;

            InitializeComponent();
            InitSettings();
        }

        private void InitSettings()
        {
            ToolTip toolTip = new ToolTip();
            toolTip.SetToolTip(checkBoxAutoRefresh, Caption);
            toolTip.SetToolTip(numericUpDownRefreshInterval, Caption);

            checkBoxAdvancedMode.Checked = settingsInstance.AdvancedMode;
            checkBoxAutoRefresh.Checked = settingsInstance.AutoRefresh;
            checkBoxAutoRefresh.Enabled = settingsInstance.AdvancedMode;
            numericUpDownRefreshInterval.Value = settingsInstance.AutoRefreshInterval;
            numericUpDownRefreshInterval.Enabled = checkBoxAutoRefresh.Checked;
        }

        private void ButtonSettingsCancel_Click(object sender, EventArgs e) => Close();

        private void CheckBoxAutoRefresh_CheckedChanged(object sender, EventArgs e)
        {
            numericUpDownRefreshInterval.Enabled = checkBoxAutoRefresh.Checked;
        }

        private void ButtonSettingsApply_Click(object sender, EventArgs e)
        {
            var currentAdvancedMode = settingsInstance.AdvancedMode;
            settingsInstance.AutoRefresh = checkBoxAutoRefresh.Checked;
            settingsInstance.AutoRefreshInterval = (int)numericUpDownRefreshInterval.Value;
            settingsInstance.AdvancedMode = checkBoxAdvancedMode.Checked;
            timerInstance.Interval = settingsInstance.AutoRefreshInterval;
            settingsInstance.Save();

            if (settingsInstance.AutoRefresh && !timerInstance.Enabled)
                timerInstance.Start();
            else if (!settingsInstance.AutoRefresh && timerInstance.Enabled)
                timerInstance.Stop();

            if (currentAdvancedMode != settingsInstance.AdvancedMode)
            {
                DialogResult result = MessageBox.Show("Settings will take effect on next app launch.\nDo you want to restart it now?", "Restart",  MessageBoxButtons.YesNo);

                if (result == DialogResult.Yes)
                {
                    Properties.Settings.Default.IsRestarting = true;
                    Properties.Settings.Default.Save();
                    Application.Restart();
                }
            }
        }

        private void CheckBoxAdvancedMode_CheckedChanged(object sender, EventArgs e)
        {
            checkBoxAutoRefresh.Enabled = checkBoxAdvancedMode.Checked;
            numericUpDownRefreshInterval.Enabled = checkBoxAdvancedMode.Checked && checkBoxAutoRefresh.Checked;
        }
    }
}
