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
            Dispose();
        }

        private void buttonSave_Click(object sender, EventArgs e)
        {
            string unixTimestamp = Convert.ToString((DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalMinutes);
            string filename = $@"{string.Join("_", this.Text.Split())}_{unixTimestamp}.png";
            SaveToFile(filename);
        }

        private void buttonSaveAs_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveFileDialog1 = new SaveFileDialog
            {
                Filter = "png files (*.png)|*.png|All files (*.*)|*.*",
                FilterIndex = 1,
                DefaultExt = "png",
                FileName = "ZenTimings_Screenshot.png",
                RestoreDirectory = true
            };

            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                SaveToFile(saveFileDialog1.FileName);
            }
        }
    }
}
