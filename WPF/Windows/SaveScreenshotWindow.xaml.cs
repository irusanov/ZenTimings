using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows;
using Clipboard = System.Windows.Clipboard;
using MessageBox = AdonisUI.Controls.MessageBox;
using MessageBoxButton = AdonisUI.Controls.MessageBoxButton;
using MessageBoxImage = AdonisUI.Controls.MessageBoxImage;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace ZenTimings.Windows
{
    /// <summary>
    /// Interaction logic for SaveWindow.xaml
    /// </summary>
    public partial class SaveWindow : ThemedAdonisWindow, IDisposable
    {
        private static Bitmap screenshot;

        public SaveWindow(Bitmap bitmap)
        {
            screenshot = bitmap;
            InitializeComponent();
        }

        private string GetFilenameWithTimestamp()
        {
            string unixTimestamp = Convert.ToString(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMinutes,
                CultureInfo.InvariantCulture);
            return $"{string.Join("_", this.Title.Split())}_{unixTimestamp}.png";
        }

        private void SaveToFile(string filename = "ZenTimingsScreenshot.png")
        {
            try
            {
                var directory = AppSettings.Instance.ScreenshotSaveLocation;
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                var path = Path.Combine(directory, filename);
                screenshot.Save(path);
                screenshot.Dispose();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save screenshot: {ex.Message}", "Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            Close();
        }

        private void ButtonSave_Click(object sender, RoutedEventArgs e)
        {
            SaveToFile(GetFilenameWithTimestamp());
        }

        private void ButtonSaveAs_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "png files (*.png)|*.png|All files (*.*)|*.*",
                FilterIndex = 1,
                DefaultExt = "png",
                FileName = GetFilenameWithTimestamp(),
                RestoreDirectory = true
            };

            if (saveFileDialog.ShowDialog() == true) SaveToFile(saveFileDialog.FileName);
        }

        private void ButtonCopyToClipboard_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetDataObject(screenshot, true);
            statusStrip1.Visibility = Visibility.Visible;
        }

        public void Dispose()
        {
            ((IDisposable)screenshot).Dispose();
        }
    }
}