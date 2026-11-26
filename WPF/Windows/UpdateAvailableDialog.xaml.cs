using System;
using System.Collections.Generic;
using System.Windows;
using ZenTimings.Localization;

namespace ZenTimings.Windows
{
    public partial class UpdateAvailableDialog : ThemedWindow
    {
        public bool DontAskAgain => DontAskCheckBox.IsChecked == true;

        public UpdateAvailableDialog(string newVersion, Version installedVersion, IEnumerable<string> changes, bool showDontAskAgain, bool isBeta = false)
        {
            InitializeComponent();

            Title = isBeta ? UiText.Get("BetaUpdateAvailable") : UiText.Get("StableUpdateAvailable");

            HeaderText.Text = UiText.Format("NewVersionAvailable", newVersion, Environment.NewLine) +
                              UiText.Format("InstalledVersion", installedVersion);
            ChangesList.ItemsSource = changes ?? new string[0];
            DontAskCheckBox.Visibility = showDontAskAgain ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
