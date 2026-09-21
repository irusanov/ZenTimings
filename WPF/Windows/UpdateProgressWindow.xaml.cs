using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;

namespace ZenTimings.Windows
{
    public partial class UpdateProgressWindow : ThemedAdonisWindow
    {
        public bool IsCancelled { get; private set; }

        // Set when the updater itself closes the window (success or error);
        // any other close (title-bar X, Alt+F4, Cancel button) is a cancellation.
        private bool closingFromUpdater;
        private bool isClosed;

        public UpdateProgressWindow()
        {
            InitializeComponent();
        }

        public void SetStatus(string status)
        {
            Dispatcher.Invoke(DispatcherPriority.Render, new Action(() =>
            {
                StatusText.Text = status;
            }));
        }

        public void SetProgress(int percent, string detail = null)
        {
            Dispatcher.Invoke(DispatcherPriority.Render, new Action(() =>
            {
                if (percent < 0)
                {
                    ProgressBar.IsIndeterminate = true;
                }
                else
                {
                    ProgressBar.IsIndeterminate = false;
                    ProgressBar.Value = percent;
                }

                if (detail != null)
                    ProgressDetail.Text = detail;
            }));
        }

        public void SetIndeterminate(string status)
        {
            Dispatcher.Invoke(DispatcherPriority.Render, new Action(() =>
            {
                StatusText.Text = status;
                ProgressBar.IsIndeterminate = true;
                ProgressDetail.Text = "";
            }));
        }

        public void EnableClose()
        {
            Dispatcher.Invoke(DispatcherPriority.Render, new Action(() =>
            {
                CancelButton.Content = "Close";
            }));
        }

        /// <summary>
        /// Closes the window without marking the update as cancelled.
        /// Safe to call when the window has already been closed.
        /// </summary>
        internal void CloseFromUpdater()
        {
            if (isClosed)
                return;

            closingFromUpdater = true;
            try
            {
                Close();
            }
            catch (InvalidOperationException)
            {
                // Window is already closing
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!closingFromUpdater)
                IsCancelled = true;

            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            isClosed = true;
            base.OnClosed(e);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            IsCancelled = true;
            Close();
        }
    }
}
