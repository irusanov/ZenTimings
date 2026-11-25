using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Media.Imaging;
using ZenTimings.Common;
using ZenTimings.Settings;
using ZenTimings.Theming;
using ZenTimings.Utils;
using ZenTimings.Windows;
using MessageBox = ZenTimings.Theming.MessageBox;
using MessageBoxImage = ZenTimings.Theming.MessageBoxImage;

namespace ZenTimings.Helpers
{
    /// <summary>
    /// Opens the report sending from anywhere: the menus, error boxes and the check for a crash at the last run.
    /// The report is built in the send dialog (<see cref="DebugReportGenerator"/>), so nothing has to be run first.
    /// </summary>
    internal static class ProblemReport
    {
        private static readonly object SendButtonId = new object();
        private static readonly HashSet<string> offeredErrors = new HashSet<string>();
        private static bool isOpen;

        /// <summary>DDR4: the BIOS memory controller config of the main window, for the report; null when not loaded.</summary>
        public static BiosMemController BiosMemController { get; set; }

        /// <summary>
        /// Opens the send dialog, which builds the report. One at a time: a second request while it is open is ignored.
        /// </summary>
        /// <param name="owner">Window to center on; null (or not visible) centers on the screen.</param>
        /// <param name="errorDetails">The error that led here, offered for the end of the report.</param>
        /// <param name="description">Text to start the description with.</param>
        public static void ShowSendDialog(Window owner = null, string errorDetails = null, string description = null)
        {
            if (isOpen)
                return;

            isOpen = true;
            try
            {
                BitmapSource screenshot = null;
                try
                {
                    screenshot = WindowUtils.CaptureMainWindow();
                }
                catch
                {
                    // send without a screenshot
                }

                // Only a core that exists: after a failed start, creating one could fail again
                var generator = new DebugReportGenerator(CpuSingleton.Existing, BiosMemController);
                var dialog = new SendReportDialog(() => generator.GenerateAsync(), screenshot, errorDetails, description);
                if (owner != null && owner.IsVisible)
                    dialog.Owner = owner;

                dialog.ShowDialog();
            }
            finally
            {
                isOpen = false;
            }
        }

        /// <summary>
        /// An error box that offers to send a report; when chosen, the send dialog opens after it, with the error
        /// attached. The offer is made once per error in a session (an exception repeating from the same place, or
        /// the same message); a repeat only shows the message.
        /// </summary>
        /// <returns>True when the user chose to send a report.</returns>
        public static bool ShowError(string message, string caption = "Error", Exception exception = null, Window owner = null,
            MessageBoxImage icon = MessageBoxImage.Error, string closeLabel = "Close")
        {
            MessageBoxButtonModel pressed = ShowErrorWithButtons(message, caption, exception, owner, null, icon, closeLabel);
            return pressed != null && ReferenceEquals(pressed.Id, SendButtonId);
        }

        /// <summary>
        /// <see cref="ShowError(string, string, Exception, Window, MessageBoxImage, string)"/> with buttons of the
        /// caller's own between the send button and Close, e.g. "Don't show again". They are shown whether or not
        /// the report is offered.
        /// </summary>
        /// <param name="extraButtons">Buttons to add, null for none. The caller compares the result with them.</param>
        /// <returns>
        /// The button that closed the box, null when it was closed otherwise. When it is the send button, the send
        /// dialog has already been shown.
        /// </returns>
        public static MessageBoxButtonModel ShowErrorWithButtons(string message, string caption, Exception exception, Window owner,
            IEnumerable<MessageBoxButtonModel> extraButtons, MessageBoxImage icon = MessageBoxImage.Error, string closeLabel = "Close")
        {
            bool offerReport = offeredErrors.Add(GetSignature(message, caption, exception));

            var buttons = new List<MessageBoxButtonModel>();
            if (offerReport)
                buttons.Add(MessageBoxButtons.Custom("Send report...", SendButtonId));
            if (extraButtons != null)
                buttons.AddRange(extraButtons);
            buttons.Add(MessageBoxButtons.Ok(closeLabel));

            var model = new MessageBoxModel
            {
                Text = offerReport ? message + "\n\nYou can send a debug report to the developer to help fix this." : message,
                Caption = caption,
                Icon = icon,
                Buttons = buttons,
            };

            MessageBox.Show(owner, model);

            MessageBoxButtonModel pressed = model.ButtonPressed;
            if (pressed != null && ReferenceEquals(pressed.Id, SendButtonId))
                ShowSendDialog(owner, BuildDetails(message, exception), $"{caption}: {FirstLine(message)}");

            return pressed;
        }

        /// <summary>
        /// Offers once to send a report when the last run ended with a crash that closed the app (a terminating entry
        /// in crash.log newer than the last one offered). The first run only records where crash.log stands.
        /// </summary>
        public static void OfferAfterCrash(Window owner)
        {
            var settings = AppSettings.Instance;
            CrashLog.Entry entry = CrashLog.FindLastTerminating();

            if (string.IsNullOrEmpty(settings.LastCrashOffered))
            {
                // No baseline yet: crashes from before this check existed are not offered
                settings.LastCrashOffered = entry?.Stamp ?? CrashLog.NoEntry;
                settings.Save();
                return;
            }

            if (entry == null)
                return;

            if (settings.LastCrashOffered != CrashLog.NoEntry && string.CompareOrdinal(entry.Stamp, settings.LastCrashOffered) <= 0)
                return;

            settings.LastCrashOffered = entry.Stamp;
            settings.Save();

            var model = new MessageBoxModel
            {
                Text = $"ZenTimings closed unexpectedly on {entry.Stamp}.\n\nDo you want to send a debug report to the developer?",
                Caption = "ZenTimings",
                Icon = MessageBoxImage.Warning,
                Buttons = new[]
                {
                    MessageBoxButtons.Custom("Send report...", SendButtonId),
                    MessageBoxButtons.Ok("Not now"),
                },
            };

            MessageBox.Show(owner, model);

            if (model.ButtonPressed != null && ReferenceEquals(model.ButtonPressed.Id, SendButtonId))
                ShowSendDialog(owner, entry.Text, "ZenTimings closed unexpectedly");
        }

        /// <summary>The error message, the exception and where crash.log is, as the error details of a report.</summary>
        public static string BuildDetails(string message, Exception exception = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine(message);

            if (exception != null)
            {
                sb.AppendLine();
                sb.AppendLine(exception.ToString());
            }

            if (CrashLog.LastPath != null)
            {
                sb.AppendLine();
                sb.AppendLine($"Crash log: {CrashLog.LastPath}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// What makes two errors the same: the type of the innermost exception and where it was thrown (the first
        /// line of its stack trace), or the caption and message of an error without an exception.
        /// </summary>
        private static string GetSignature(string message, string caption, Exception exception)
        {
            if (exception == null)
                return "message|" + caption + "|" + message;

            Exception root = exception.GetBaseException();
            return "exception|" + root.GetType().FullName + "|" + FirstLine(root.StackTrace?.Trim());
        }

        private static string FirstLine(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";

            int end = text.IndexOfAny(new[] { '\r', '\n' });
            return end < 0 ? text : text.Substring(0, end);
        }
    }
}
