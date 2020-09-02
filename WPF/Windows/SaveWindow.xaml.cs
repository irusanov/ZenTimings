using AdonisUI.Controls;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Drawing;
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
    /// Interaction logic for SaveWindow.xaml
    /// </summary>
    public partial class SaveWindow : AdonisWindow, IDisposable
    {
        private static Bitmap screenshot;
        public SaveWindow(Bitmap bitmap)
        {
            screenshot = bitmap;
            InitializeComponent();
        }

        private void SaveToFile(string filename = "ZenTimingsScreenshot.png")
        {
            screenshot.Save(filename);
            screenshot.Dispose();
            Close();
        }

        private void ButtonSave_Click(object sender, RoutedEventArgs e)
        {
            string unixTimestamp = Convert.ToString((DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalMinutes);
            string filename = $@"{string.Join("_", this.Title.Split())}_{unixTimestamp}.png";
            SaveToFile(filename);
        }

        private void ButtonSaveAs_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "png files (*.png)|*.png|All files (*.*)|*.*",
                FilterIndex = 1,
                DefaultExt = "png",
                FileName = "ZenTimings_Screenshot.png",
                RestoreDirectory = true
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                SaveToFile(saveFileDialog.FileName);
            }
        }

        private void ButtonCopyToClipboard_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetDataObject(screenshot);
            statusStrip1.Visibility = Visibility.Visible;
        }

        public void Dispose()
        {
            ((IDisposable)screenshot).Dispose();
        }
    }
}
