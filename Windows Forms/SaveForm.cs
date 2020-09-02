using System;
using System.Drawing;
using System.Windows.Forms;

namespace ZenTimings
{
    public partial class SaveForm : Form
    {
        private static Bitmap screenshot;
        public SaveForm(Bitmap bitmap)
        {
            screenshot = bitmap;
            InitializeComponent();
        }

        private void SaveToFile(string filename = "ZenTimingsScreenshot.png")
        {
            // MessageBox.Show($"File saved as {filename}");
            screenshot.Save(filename);
            screenshot.Dispose();
            Close();
            //Dispose();
        }

        private void ButtonSave_Click(object sender, EventArgs e)
        {
            string unixTimestamp = Convert.ToString((DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalMinutes);
            string filename = $@"{string.Join("_", this.Text.Split())}_{unixTimestamp}.png";
            SaveToFile(filename);
        }

        private void ButtonSaveAs_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "png files (*.png)|*.png|All files (*.*)|*.*",
                FilterIndex = 1,
                DefaultExt = "png",
                FileName = "ZenTimings_Screenshot.png",
                RestoreDirectory = true
            };

            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                SaveToFile(saveFileDialog.FileName);
            }
        }

        private void ButtonCopyToClipboard_Click(object sender, EventArgs e)
        {
            Clipboard.SetImage(screenshot);
            statusStrip1.Visible = true;
            toolStripStatusLabel1.Text = "Screenshot copied to clipboard.";
        }
    }
}
