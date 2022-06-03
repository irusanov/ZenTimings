using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;

namespace ZenTimings.Windows
{
    /// <summary>
    /// Interaction logic for AboutDialog.xaml
    /// </summary>
    public partial class AboutDialog
    {
        private static readonly Updater updater = (Application.Current as App)?.updater;
        private DispatcherTimer notificationTimer;

        public AboutDialog()
        {
            var AssemblyTitle = ((AssemblyTitleAttribute)Attribute.GetCustomAttribute(
                Assembly.GetExecutingAssembly(),
                typeof(AssemblyTitleAttribute), false)).Title;

            var AssemblyProduct = ((AssemblyProductAttribute)Attribute.GetCustomAttribute(
                Assembly.GetExecutingAssembly(),
                typeof(AssemblyProductAttribute), false)).Product;

            var AssemblyVersion = ((AssemblyFileVersionAttribute)Attribute.GetCustomAttribute(
                Assembly.GetExecutingAssembly(),
                typeof(AssemblyFileVersionAttribute), false)).Version;

            var AssemblyDescription = ((AssemblyDescriptionAttribute)Attribute.GetCustomAttribute(
                Assembly.GetExecutingAssembly(),
                typeof(AssemblyDescriptionAttribute), false)).Description;

            var AssemblyCopyright = ((AssemblyCopyrightAttribute)Attribute.GetCustomAttribute(
                Assembly.GetExecutingAssembly(),
                typeof(AssemblyCopyrightAttribute), false)).Copyright;


            InitializeComponent();

            //this.Title = string.Format("About {0}", AssemblyTitle);
            this.labelProductName.Content = AssemblyProduct;
            this.labelVersion.Text = $"Version {AssemblyVersion}";
            this.labelCopyright.Text = AssemblyCopyright;
            this.labelCompanyName.Text = AssemblyDescription;

            // List of all modules, there might be more DLL files in the directory
            string[] files =
            {
                "AdonisUI.ClassicTheme.dll",
                "AdonisUI.dll",
                "AutoUpdater.NET.dll",
                "inpoutx64.dll",
                "WinIo32.dll",
                "ZenStates-Core.dll",
            };
            var appModules = new List<KeyValuePair<string, string>>();

            foreach (var file in files)
            {
                var version = "missing";

                try
                {
                    FileVersionInfo fileVersionInfo = FileVersionInfo.GetVersionInfo(file);
                    version = fileVersionInfo.FileVersion.Replace(", ", ".");
                }
                catch (Exception)
                {
                    // Do Nothing 
                }

                appModules.Add(new KeyValuePair<string, string>(file.Replace(".dll", ""), version));
            }

            Modules.ItemsSource = appModules;
            updater.UpdateCheckCompleteEvent += Updater_UpdateCheckCompleteEvent;
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        private void CheckUpdateBtn_Click(object sender, RoutedEventArgs e)
        {
            updater.CheckForUpdate(true);
        }

        private void Updater_UpdateCheckCompleteEvent(object sender, EventArgs e)
        {
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
                aboutWindowPopup.IsOpen = false;
            });

            notificationTimer.Start();

            aboutWindowPopup.Width = AboutWindowContent.ActualWidth;
            aboutWindowPopup.IsOpen = true;
        }

        private void AboutWindowPopup_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            aboutWindowPopup.IsOpen = false;
        }
    }
}
