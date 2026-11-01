using System;
using System.Globalization;
using System.IO;
using System.Windows;
using ZenStates.Core;
using ZenTimings.Common;
using ZenTimings.Helpers;
using ZenTimings.Utils;
using MessageBox = ZenTimings.Theming.MessageBox;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace ZenTimings.Windows
{
    /// <summary>
    ///     Interaction logic for DebugDialog.xaml: runs <see cref="DebugReportGenerator"/>, shows the report and
    ///     saves or sends it.
    /// </summary>
    public partial class DebugDialog : ThemedWindow
    {
        private readonly AsusWMI AWMI;
        private readonly BiosMemController BMC;
        private readonly Cpu cpu;

        public DebugDialog(BiosMemController biosMemCtrl, AsusWMI asusWmi)
        {
            InitializeComponent();
            cpu = CpuSingleton.Instance;
            BMC = biosMemCtrl;
            AWMI = asusWmi;
        }

        private void SetControlsState(bool enabled = true)
        {
            buttonDebugCancel.IsEnabled = enabled;
            buttonDebugSave.IsEnabled = enabled;
            buttonDebugSaveAs.IsEnabled = enabled;
            buttonDebugSend.IsEnabled = enabled;
            buttonDebug.IsEnabled = enabled;
            textBoxDebugOutput.IsEnabled = enabled;
            CheckPrintSerials.IsEnabled = enabled;
        }

        private void SaveToFile(bool saveAs = false)
        {
            var unixTimestamp = Convert.ToString(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMinutes,
                CultureInfo.InvariantCulture);
            var filename = $@"{string.Join("_", Title.Split())}_{unixTimestamp}.txt";

            // Never rely on the current directory (it is System32 when started by the scheduled task)
            var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filename);

            if (saveAs)
            {
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "text files (*.txt)|*.txt|All files (*.*)|*.*",
                    FilterIndex = 1,
                    DefaultExt = "txt",
                    FileName = filename,
                    InitialDirectory = AppDomain.CurrentDomain.BaseDirectory,
                    RestoreDirectory = true
                };

                if (saveFileDialog.ShowDialog() != true)
                    return;

                filePath = saveFileDialog.FileName;
            }

            try
            {
                File.WriteAllText(filePath, textBoxDebugOutput.Text);
                MessageBox.Show($"Debug report saved as {filePath}", saveAs ? "Save As" : "Save");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Could not save the debug report to {filePath}:\n{ex.Message}",
                    "Error",
                    ZenTimings.Theming.MessageBoxButton.OK,
                    ZenTimings.Theming.MessageBoxImage.Error);
            }
        }

        private async void ButtonDebug_Click(object sender, RoutedEventArgs e)
        {
            bool printSerialNumbers = CheckPrintSerials.IsChecked == true;
            var generator = new DebugReportGenerator(cpu, BMC, AWMI);

            SetControlsState(false);
            try
            {
                textBoxDebugOutput.Text = await generator.GenerateAsync(printSerialNumbers);
                MinimizeFootprint();

                if (generator.Error != null || generator.TimedOut)
                {
                    string reason = generator.TimedOut
                        ? "The debug report did not finish in time, it shows what was collected."
                        : $"An error occurred while generating the debug report:\n{generator.Error.Message}";
                    MessageBox.Show(
                        reason,
                        "Error",
                        ZenTimings.Theming.MessageBoxButton.OK,
                        ZenTimings.Theming.MessageBoxImage.Error);
                }
            }
            finally
            {
                SetControlsState();
            }
        }

        private void ButtonDebugSend_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Media.Imaging.BitmapSource screenshot = null;
            try
            {
                screenshot = WindowUtils.CaptureMainWindow();
            }
            catch
            {
                // send without a screenshot
            }

            new SendReportDialog(textBoxDebugOutput.Text, screenshot) { Owner = this }.ShowDialog();
        }

        private void ButtonDebugCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ButtonDebugSave_Click(object sender, RoutedEventArgs e)
        {
            SaveToFile();
        }

        private void ButtonDebugSaveAs_Click(object sender, RoutedEventArgs e)
        {
            SaveToFile(true);
        }
    }
}