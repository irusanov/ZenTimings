using System;
using System.Linq;
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
    /// Export module glue. Nothing outside of this folder refers to the module: the window is extended from here,
    /// so the application builds and works the same when the folder is removed.
    /// </summary>
    public partial class MainWindow
    {
        private const int RefreshWaitStepMs = 20;
        private const int RefreshWaitLimitMs = 5000;

        private DateTime? lastRefreshUtc;
        private bool exportAttached;
        private MenuItem liveSnapshotMenuItem;

        static MainWindow()
        {
            EventManager.RegisterClassHandler(typeof(MainWindow), LoadedEvent,
                new RoutedEventHandler((s, e) => ((MainWindow)s).AttachExport()));
        }

        private void AttachExport()
        {
            // A debug report window has no live data to export
            if (exportAttached || isMockWindow)
                return;

            exportAttached = true;

            // All values were read during startup, right before the window was shown
            lastRefreshUtc = DateTime.UtcNow;

            MenuItem exportMenu = MainMenu.Items.OfType<MenuItem>()
                .SelectMany(m => m.Items.OfType<MenuItem>())
                .FirstOrDefault(m => m.Header as string == "Export");
            if (exportMenu == null)
                return;

            exportMenu.Items.Add(CreateExportMenuItem("As JSON...", SnapshotFormat.Json, ExportSnapshotMenuItem_Click));
            exportMenu.Items.Add(CreateExportMenuItem("As Text...", SnapshotFormat.Text, ExportSnapshotMenuItem_Click));

            var separator = new Separator();
            separator.SetResourceReference(ForegroundProperty, "PanelBackground");
            exportMenu.Items.Add(separator);
            // Checked while the live snapshot is on
            liveSnapshotMenuItem = CreateExportMenuItem("Live snapshot file...", null, LiveSnapshotMenuItem_Click);
            liveSnapshotMenuItem.IsChecked = ExportSettings.Instance.LiveSnapshotEnabled;
            exportMenu.Items.Add(liveSnapshotMenuItem);

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

        private static MenuItem CreateExportMenuItem(string header, object tag, RoutedEventHandler handler)
        {
            var item = new MenuItem { Header = header, Tag = tag };
            item.Click += handler;
            return item;
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

            liveSnapshotMenuItem.IsChecked = ExportSettings.Instance.LiveSnapshotEnabled;

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
