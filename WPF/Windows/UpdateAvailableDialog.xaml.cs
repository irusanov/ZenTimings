using System;
using System.Collections.Generic;
using System.Windows;

namespace ZenTimings.Windows
{
    public partial class UpdateAvailableDialog : ThemedAdonisWindow
    {
        public bool DontAskAgain => DontAskCheckBox.IsChecked == true;

        public UpdateAvailableDialog(string newVersion, Version installedVersion, IEnumerable<string> changes, bool showDontAskAgain, bool isBeta = false)
        {
            InitializeComponent();

            Title = isBeta ? "BETA Update Available" : "Stable Update Available";

            HeaderText.Text = $"There is new version {newVersion} available.{Environment.NewLine}" +
                              $"You are using version {installedVersion}.";
            ChangesList.ItemsSource = changes ?? new string[0];
            DontAskCheckBox.Visibility = showDontAskAgain ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
