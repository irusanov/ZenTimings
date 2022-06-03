using System;
using System.Collections.Generic;
using System.IO;
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
