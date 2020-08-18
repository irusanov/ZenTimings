using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ZenTimings
{
    public partial class OptionsDialog : Form
    {
        public OptionsDialog()
        {
            InitializeComponent();
        }

        private void ButtonSettingsCancel_Click(object sender, EventArgs e)
        {
            Close();
        }
        private void CheckBoxAutoRefresh_CheckedChanged(object sender, EventArgs e)
        {
            numericUpDownRefreshInterval.Enabled = checkBoxAutoRefresh.Checked;
        }
    }
}
