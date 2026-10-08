using System;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ZenTimings.Helpers;

namespace ZenTimings.Windows
{
    /// <summary>
    /// Lets the user add a description, an optional email and a screenshot to a debug report and sends it to the
    /// developer through <see cref="DebugReportSender"/>.
    /// </summary>
    public partial class SendReportDialog : ThemedWindow
    {
        private static readonly Regex EmailPattern = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

        private readonly string report;
        private readonly BitmapSource screenshot;
        private CancellationTokenSource sendCancellation;

        /// <param name="report">The debug report text, sent as is.</param>
        /// <param name="screenshot">Picture of the main window, or null when none could be taken.</param>
        public SendReportDialog(string report, BitmapSource screenshot)
        {
            this.report = report ?? "";
            this.screenshot = screenshot;

            InitializeComponent();

            DescriptionBox.MaxLength = DebugReportSender.MaxDescriptionLength;
            EmailBox.MaxLength = DebugReportSender.MaxEmailLength;
            ReportPreview.Text = this.report;
            ReportSizeText.Text = FormatSize(Encoding.UTF8.GetByteCount(this.report)) + " of text";

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

            Loaded += (s, e) =>
            {
                SendButton.IsEnabled = true;
                DescriptionBox.Focus();
            };
        }

        private bool IsSending => sendCancellation != null;

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
                Report = report,
                Description = DescriptionBox.Text.Trim(),
                Email = email,
                ScreenshotPng = ScreenshotToggle.IsChecked == true ? EncodePng(screenshot) : null,
            };

            SetBusy(true);
            try
            {
                string id = await DebugReportSender.SendAsync(submission, sendCancellation.Token);
                ShowSuccess(id, email.Length > 0);
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
            SendButton.IsEnabled = !busy;
            DescriptionBox.IsReadOnly = busy;
            EmailBox.IsReadOnly = busy;
            ScreenshotToggle.IsEnabled = !busy && screenshot != null;
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
