using System.Diagnostics;
using System.Reflection;

namespace ZenTimings.Windows
{
    /// <summary>
    /// Interaction logic for Changelog.xaml
    /// </summary>
    public partial class Changelog : ThemedAdonisWindow
    {
        public Changelog()
        {
            InitializeComponent();
            /*
            var exePath = AppDomain.CurrentDomain.BaseDirectory;
            var pagesFolder = Directory.GetParent(exePath);
            string changeLogPath = pagesFolder.FullName + "\\whatsnew.html";
            Browser1.Source = new Uri(changeLogPath);
            */

            this.DataContext = new
            {
#if BETA
                Version = $"Version {Version} - beta",
#else
                Version = $"Version {Version}",
#endif
            };
        }

        private string Version => Assembly.GetExecutingAssembly().GetName().Version?.ToString().Replace(".0.0", "") ?? string.Empty;

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }
    }
}
