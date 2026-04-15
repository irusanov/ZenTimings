using System;
using System.Windows;
using System.Windows.Threading;

namespace ZenTimings.Windows
{
    public partial class UpdateProgressWindow : ThemedAdonisWindow
    {
        public bool IsCancelled { get; private set; }

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

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            IsCancelled = true;
            Close();
        }
    }
}
