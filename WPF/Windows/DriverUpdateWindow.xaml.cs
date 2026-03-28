using System;

namespace ZenTimings.Windows
{
    /// <summary>
    /// Interaction logic for DriverUpdateWindow.xaml
    /// </summary>
    public partial class DriverUpdateWindow
    {
        public bool IsSkipChecked => SkipDriverUpdateCheckBox.IsChecked == true;

        public DriverUpdateWindow(Version currentVersion, Version newVersion)
        {
            InitializeComponent();

            this.DataContext = new
            {
                currentVersion,
                newVersion
            };
        }

        private void DriverUpdate_Install_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void AdonisWindow_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            WindowUtils.RemoveSystemBorderAndRadius(this);
        }
    }
}
