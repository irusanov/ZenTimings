using System;
using System.IO;

namespace ZenTimings.Windows
{
    /// <summary>
    /// Interaction logic for Changelog.xaml
    /// </summary>
    public partial class Changelog : AdonisUI.Controls.AdonisWindow
    {
        public Changelog()
        {
            InitializeComponent();

            var exePath = AppDomain.CurrentDomain.BaseDirectory;
            var pagesFolder = Directory.GetParent(exePath);
            string changeLogPath = pagesFolder.FullName + "\\whatsnew.html";
            Browser1.Source = new Uri(changeLogPath);
        }
    }
}
