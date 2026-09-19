using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ZenTimings.Export;
using ZenTimings.Windows;
using MessageBox = AdonisUI.Controls.MessageBox;
using MessageBoxButton = AdonisUI.Controls.MessageBoxButton;
using MessageBoxImage = AdonisUI.Controls.MessageBoxImage;
using MessageBoxResult = AdonisUI.Controls.MessageBoxResult;

namespace ZenTimings
{
    /// <summary>
    /// The main window's part of the snapshot export: the File > Export handlers and the live snapshot file.
    /// </summary>
    public partial class MainWindow
    {
        private const int RefreshWaitStepMs = 20;
        private const int RefreshWaitLimitMs = 5000;

        private DateTime? lastRefreshUtc;

        // Called once the live window is loaded, a debug report window has no live data to export
        private void InitLiveSnapshot()
        {
            // All values were read during startup, right before the window was shown
            lastRefreshUtc = DateTime.UtcNow;

            // Checked while the live snapshot is on
            menuItemLiveSnapshot.IsChecked = ExportSettings.Instance.LiveSnapshotEnabled;

            // The application's own auto refresh timer drives the live snapshot, with its interval and its start/stop rules
            PowerCfgTimer.Tick += ExportTimer_Tick;
            Application.Current.Exit += (s, e) =>
            {
                if (ExportSettings.Instance.LiveSnapshotEnabled)
                    LiveSnapshot.Stop();
            };

            if (ExportSettings.Instance.LiveSnapshotEnabled)
                UpdateLiveSnapshot();
        }

        // Runs right after the regular tick handler, which has just started the refresh task
        private void ExportTimer_Tick(object sender, EventArgs e)
        {
            lastRefreshUtc = DateTime.UtcNow;
            if (!ExportSettings.Instance.LiveSnapshotEnabled || !LiveSnapshot.IsDue)
                return;

            Task.Run(async () =>
            {
                // Wait for that refresh to finish so that the snapshot holds the new values
                for (int waited = 0; Volatile.Read(ref isRefreshing) != 0 && waited < RefreshWaitLimitMs; waited += RefreshWaitStepMs)
                    await Task.Delay(RefreshWaitStepMs);

                lastRefreshUtc = DateTime.UtcNow;
                LiveSnapshot.Update(GetSnapshotSource(true));
            });
        }

        private SnapshotSource GetSnapshotSource(bool? autoRefreshActive = null)
        {
            return new SnapshotSource
            {
                BiosMemConfig = BMC?.Config,
                AsusSensors = AsusWmi?.sensors,
                Plugins = plugins,
                LastRefreshUtc = lastRefreshUtc,
                AutoRefreshActive = autoRefreshActive ?? PowerCfgTimer.IsEnabled,
            };
        }

        private void UpdateLiveSnapshot()
        {
            SnapshotSource source = GetSnapshotSource();
            Task.Run(() =>
            {
                if (!LiveSnapshot.WriteNow(source))
                    Dispatcher.Invoke(() => HandleError($"Could not write the live snapshot file.\n{LiveSnapshot.LastError}", "Live snapshot"));
            });
        }

        private void ExportSnapshotMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SnapshotFormat format = (sender as MenuItem)?.Tag is SnapshotFormat tag ? tag : SnapshotFormat.Json;
            ExportDialog exportWnd = new ExportDialog(
                (options, selectedFormat) =>
                {
                    if (selectedFormat == SnapshotFormat.Text)
                        return SnapshotWriter.ToText(SnapshotBuilder.Build(GetSnapshotSource(), options));

                    mainViewModel.ExportSource = GetSnapshotSource();
                    mainViewModel.ExportOptions = options;
                    return mainViewModel.GetJSON();
                },
                format,
                false,
                SnapshotBuilder.GetUnavailableSections(GetSnapshotSource()))
            {
                Owner = this
            };
            exportWnd.ShowDialog();
        }

        private void LiveSnapshotMenuItem_Click(object sender, RoutedEventArgs e)
        {
            bool wasEnabled = ExportSettings.Instance.LiveSnapshotEnabled;
            ExportDialog exportWnd = new ExportDialog(null, ExportSettings.Instance.LiveSnapshotFormat, true,
                SnapshotBuilder.GetUnavailableSections(GetSnapshotSource()))
            {
                Owner = this
            };

            if (exportWnd.ShowDialog() != true)
                return;

            menuItemLiveSnapshot.IsChecked = ExportSettings.Instance.LiveSnapshotEnabled;

            // Turned off: the file is the user's to keep. A kept file is simply overwritten if the same
            // folder and name are used again later.
            string written = LiveSnapshot.LastWrittenPath;
            if (wasEnabled && !ExportSettings.Instance.LiveSnapshotEnabled && written != null && System.IO.File.Exists(written))
            {
                MessageBoxResult answer = MessageBox.Show(
                    $"The live snapshot is now off.\n\nDelete the file that was being written?\n{written}",
                    "Live snapshot",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (answer != MessageBoxResult.Yes)
                {
                    LiveSnapshot.Detach();
                    return;
                }
            }

            // Remove the previous file, the folder, the name or the format may have changed
            LiveSnapshot.Remove();
            if (ExportSettings.Instance.LiveSnapshotEnabled)
                UpdateLiveSnapshot();
        }
    }
}
