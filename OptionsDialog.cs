using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ZenTimings.Utils;

namespace ZenTimings
{
    public partial class OptionsDialog : Form
    {
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
            checkBoxAutoRefresh.Checked = settingsInstance.AutoRefresh;
            numericUpDownRefreshInterval.Value = settingsInstance.AutoRefreshInterval;
            numericUpDownRefreshInterval.Enabled = checkBoxAutoRefresh.Checked;
            labelAutoRefreshWarning.Visible = !checkBoxAutoRefresh.Checked;
        }

        private void ButtonSettingsCancel_Click(object sender, EventArgs e) => Close();

        private void CheckBoxAutoRefresh_CheckedChanged(object sender, EventArgs e)
        {
            numericUpDownRefreshInterval.Enabled = checkBoxAutoRefresh.Checked;
            labelAutoRefreshWarning.Visible = !checkBoxAutoRefresh.Checked;
        }

        private void ButtonSettingsApply_Click(object sender, EventArgs e)
        {
            settingsInstance.AutoRefresh = checkBoxAutoRefresh.Checked;
            settingsInstance.AutoRefreshInterval = (int)numericUpDownRefreshInterval.Value;
            timerInstance.Interval = settingsInstance.AutoRefreshInterval;
            settingsInstance.Save();

            if (settingsInstance.AutoRefresh && !timerInstance.Enabled)
                timerInstance.Start();
            else if (!settingsInstance.AutoRefresh && timerInstance.Enabled)
                timerInstance.Stop();
        }
    }
}
