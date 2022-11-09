using Microsoft.Win32;
using System;
using System.Drawing;
using System.Globalization;
using System.Windows;

namespace ZenTimings.Windows
{
    /// <summary>
    /// Interaction logic for SaveWindow.xaml
    /// </summary>
    public partial class SaveWindow : IDisposable
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
            string unixTimestamp = Convert.ToString(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMinutes,
                CultureInfo.InvariantCulture);
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

            if (saveFileDialog.ShowDialog() == true) SaveToFile(saveFileDialog.FileName);
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