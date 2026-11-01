using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ZenStates.Core.Common;
using ZenTimings.Helpers;
using ZenTimings.Settings;

namespace ZenTimings.Windows
{
    /// <summary>
    /// Lets the user add a description, an optional email and a screenshot to a debug report and sends it to the
    /// developer through <see cref="DebugReportSender"/>. The report is either given, or built when the dialog
    /// opens (<see cref="DebugReportGenerator"/>), so it can be opened from anywhere, after an error too.
    /// </summary>
    public partial class SendReportDialog : ThemedWindow
    {
        private static readonly Regex EmailPattern = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

        private readonly BitmapSource screenshot;
        private readonly Func<Task<string>> reportFactory;
        private readonly string errorDetails;
        private string report;
        private string savedFolder;
        private CancellationTokenSource sendCancellation;

        /// <param name="report">The debug report text, sent as is.</param>
        /// <param name="screenshot">Picture of the main window, or null when none could be taken.</param>
        public SendReportDialog(string report, BitmapSource screenshot)
            : this(screenshot, null, null, null)
        {
            SetReport(report);
        }

        /// <param name="reportFactory">Builds the report when the dialog opens, e.g. <see cref="DebugReportGenerator.GenerateAsync"/>.</param>
        /// <param name="screenshot">Picture of the main window, or null when none could be taken.</param>
        /// <param name="errorDetails">The error that led here (message, crash log lines), offered for the end of the report; null for none.</param>
        /// <param name="description">Text to start the description with, null for none.</param>
        public SendReportDialog(Func<Task<string>> reportFactory, BitmapSource screenshot, string errorDetails = null, string description = null)
            : this(screenshot, reportFactory ?? throw new ArgumentNullException(nameof(reportFactory)), errorDetails, description)
        {
        }

        private SendReportDialog(BitmapSource screenshot, Func<Task<string>> reportFactory, string errorDetails, string description)
        {
            this.screenshot = screenshot;
            this.reportFactory = reportFactory;
            this.errorDetails = string.IsNullOrWhiteSpace(errorDetails) ? null : errorDetails.TrimEnd();

            InitializeComponent();

            DescriptionBox.MaxLength = DebugReportSender.MaxDescriptionLength;
            EmailBox.MaxLength = DebugReportSender.MaxEmailLength;
            if (!string.IsNullOrEmpty(description))
                DescriptionBox.Text = description.Length > DescriptionBox.MaxLength ? description.Substring(0, DescriptionBox.MaxLength) : description;

            if (this.errorDetails != null)
            {
                ErrorDetailsText.Text = this.errorDetails;
                ErrorDetailsCard.Visibility = Visibility.Visible;
            }

            if (!AppSettings.Instance.AdvancedMode)
                AdvancedModeHint.Visibility = Visibility.Visible;

            if (screenshot != null)
            {
                ScreenshotImage.Source = screenshot;
                ScreenshotToggle.IsChecked = true;
            }
            else
            {
                ScreenshotToggle.IsChecked = false;
                ScreenshotToggle.IsEnabled = false;
                ScreenshotFrame.Visibility = Visibility.Collapsed;
                ScreenshotHint.Text = "The main window could not be captured.";
            }

            Loaded += async (s, e) =>
            {
                // Without an owner (opened after an error, before the main window is up) CenterOwner places it nowhere useful
                if (Owner == null)
                    CenterOnScreen();

                DescriptionBox.Focus();
                if (this.reportFactory != null)
                    await CollectReportAsync();
                else
                    SendButton.IsEnabled = true;
            };
        }

        private bool IsSending => sendCancellation != null;

        private bool IsCollecting { get; set; }

        /// <summary>What is sent: the report, plus the error details when they are included.</summary>
        private string ComposedReport
        {
            get
            {
                if (errorDetails == null || ErrorDetailsToggle.IsChecked != true)
                    return report ?? "";

                return (report ?? "").TrimEnd() + Environment.NewLine + Environment.NewLine +
                    ReportBuilder.Heading("Error Details") + errorDetails + Environment.NewLine;
            }
        }

        private async Task CollectReportAsync()
        {
            IsCollecting = true;
            BusyText.Text = "Collecting system information...";
            BusyPanel.Visibility = Visibility.Visible;
            ReportSizeText.Text = "Collecting...";
            TogglePreviewButton.IsEnabled = false;

            string collected;
            try
            {
                collected = await reportFactory();
            }
            catch (Exception ex)
            {
                collected = DebugReportGenerator.BuildFallbackReport("The report could not be built: " + ex.Message, null, ex.ToString());
            }

            IsCollecting = false;
            BusyPanel.Visibility = Visibility.Collapsed;
            BusyText.Text = "Sending...";
            TogglePreviewButton.IsEnabled = true;
            SetReport(collected);
            SendButton.IsEnabled = !IsSending;
        }

        private void SetReport(string text)
        {
            report = text ?? "";
            UpdateReportView();
        }

        private void UpdateReportView()
        {
            if (IsCollecting)
                return;

            string composed = ComposedReport;
            ReportPreview.Text = composed;
            ReportSizeText.Text = FormatSize(Encoding.UTF8.GetByteCount(composed)) + " of text";
        }

        private void CenterOnScreen()
        {
            Rect area = SystemParameters.WorkArea;
            Left = area.Left + Math.Max(0, (area.Width - ActualWidth) / 2);
            Top = area.Top + Math.Max(0, (area.Height - ActualHeight) / 2);
        }

        private void ErrorDetailsToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (ErrorDetailsText != null)
            {
                ErrorDetailsText.Opacity = ErrorDetailsToggle.IsChecked == true ? 1.0 : 0.35;
                UpdateReportView();
            }
        }

        private void DescriptionBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            DescriptionPlaceholder.Visibility = DescriptionBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            //SendButton.IsEnabled = !IsSending && DescriptionBox.Text.Trim().Length > 0;
            HideError();
        }

        private void EmailBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            EmailPlaceholder.Visibility = EmailBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            HideError();
        }

        private void ScreenshotToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (ScreenshotImage != null)
                ScreenshotImage.Opacity = ScreenshotToggle.IsChecked == true ? 1.0 : 0.35;
        }

        private void TogglePreviewButton_Click(object sender, RoutedEventArgs e)
        {
            bool show = ReportPreview.Visibility != Visibility.Visible;
            ReportPreview.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            TogglePreviewButton.Content = show ? "Hide" : "Show";
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            string email = EmailBox.Text.Trim();
            if (email.Length > 0 && !EmailPattern.IsMatch(email))
            {
                ShowError("That email address doesn't look right. Correct it or leave the field empty.");
                EmailBox.Focus();
                return;
            }

            var submission = new DebugReportSubmission
            {
                Report = ComposedReport,
                Description = DescriptionBox.Text.Trim(),
                Email = email,
                ScreenshotPng = ScreenshotToggle.IsChecked == true ? EncodePng(screenshot) : null,
            };

            SetBusy(true);
            try
            {
                string id = await DebugReportSender.SendAsync(submission, sendCancellation.Token);

                // A copy next to the app, so the reference ID and what was sent can be looked up later
                string saveError = null;
                try
                {
                    savedFolder = await Task.Run(() => SentReportArchive.Save(id, submission));
                }
                catch (Exception ex)
                {
                    savedFolder = null;
                    saveError = ex.Message;
                }

                ShowSuccess(id, email.Length > 0);
                ShowSaved(savedFolder, saveError);
            }
            catch (OperationCanceledException) when (sendCancellation.IsCancellationRequested)
            {
                // Cancelled by the user (Cancel button or closing the window)
            }
            catch (TaskCanceledException)
            {
                ShowError("The server did not respond in time. Please try again later.");
            }
            catch (HttpRequestException)
            {
                ShowError("Could not reach the server. Check your internet connection and try again.");
            }
            catch (DebugReportException ex)
            {
                ShowError(ex.Message);
            }
            catch (Exception ex)
            {
                ShowError("The report could not be sent: " + ex.Message);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsSending)
                sendCancellation.Cancel();
            else
                Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            sendCancellation?.Cancel();
        }

        private void CopyIdButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(ReferenceIdText.Text);
                CopyIdIcon.Kind = "IconCheck";
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
                timer.Tick += (s, args) =>
                {
                    timer.Stop();
                    CopyIdIcon.Kind = "IconCopy";
                };
                timer.Start();
            }
            catch
            {
                // The clipboard can be locked by another application; the ID is still selectable.
            }
        }

        private void SetBusy(bool busy)
        {
            if (busy)
            {
                sendCancellation = new CancellationTokenSource();
                HideError();
            }
            else
            {
                sendCancellation?.Dispose();
                sendCancellation = null;
            }

            BusyPanel.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
            //SendButton.IsEnabled = !busy && DescriptionBox.Text.Trim().Length > 0;
            SendButton.IsEnabled = !busy && !IsCollecting;
            DescriptionBox.IsReadOnly = busy;
            EmailBox.IsReadOnly = busy;
            ScreenshotToggle.IsEnabled = !busy && screenshot != null;
            ErrorDetailsToggle.IsEnabled = !busy;
        }

        private void ShowSuccess(string id, bool gaveEmail)
        {
            ReferenceIdText.Text = id;
            SuccessText.Text = gaveEmail
                ? "Thank you! The developer will look into it and may contact you by email."
                : "Thank you! The developer will look into it.";
            FormView.Visibility = Visibility.Collapsed;
            SuccessView.Visibility = Visibility.Visible;
        }

        private void ShowSaved(string folder, string error)
        {
            SavedCard.Visibility = Visibility.Visible;
            if (folder != null)
            {
                SavedTitle.Text = "A copy of the report was saved";
                SavedPathText.Text = folder;
                OpenSavedFolderButton.Visibility = Visibility.Visible;
            }
            else
            {
                SavedTitle.Text = "The report was sent, but no copy could be saved";
                SavedPathText.Text = error ?? "";
                OpenSavedFolderButton.Visibility = Visibility.Collapsed;
            }
        }

        private void OpenSavedFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (savedFolder == null)
                return;

            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", "\"" + savedFolder + "\"") { UseShellExecute = true });
            }
            catch
            {
                // The path stays shown and selectable as text
            }
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorPanel.Visibility = Visibility.Visible;
        }

        private void HideError()
        {
            if (ErrorPanel != null)
                ErrorPanel.Visibility = Visibility.Collapsed;
        }

        private static byte[] EncodePng(BitmapSource bitmap)
        {
            if (bitmap == null)
                return null;

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = new MemoryStream())
            {
                encoder.Save(stream);
                return stream.ToArray();
            }
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";
            return bytes < 1024 * 1024 ? $"{bytes / 1024.0:0.#} KB" : $"{bytes / (1024.0 * 1024.0):0.#} MB";
        }
    }
}
