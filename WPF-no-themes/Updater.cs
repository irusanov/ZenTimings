using AutoUpdaterDotNET;
using System;
using System.IO;
using System.Net;
using System.Windows;
using System.Xml.Serialization;
using ZenTimings.Windows;

namespace ZenTimings
{
    public class Updater
    {
        public event EventHandler UpdateCheckCompleteEvent;

        //public static int status = 0;
        private static bool manual;
        private static string ChangelogText { get; set; }
#if DEBUG
        private const string url = "https://zentimings.com/AutoUpdater_debug.xml";
#else
        private const string url = "https://zentimings.com/AutoUpdater.xml";
#endif
        protected virtual void OnUpdateCheckCompleteEvent(EventArgs e)
        {
            // Make a temporary copy of the event to avoid possibility of
            // a race condition if the last subscriber unsubscribes
            // immediately after the null check and before the event is raised.
            UpdateCheckCompleteEvent?.Invoke(this, e);
        }

        public Updater()
        {
            AutoUpdater.RunUpdateAsAdmin = true;
            AutoUpdater.Synchronous = true;
            AutoUpdater.LetUserSelectRemindLater = false;
            AutoUpdater.RemindLaterTimeSpan = RemindLaterFormat.Days;
            AutoUpdater.RemindLaterAt = 3;
            AutoUpdater.DownloadPath = Environment.CurrentDirectory;
            AutoUpdater.PersistenceProvider = new UpdaterPersistenceProvider();
            //status = 1;
        }

        public void CheckForUpdate(bool manualUpdate = false)
        {
            /*if (status == 0)
            {
                Init((Application.Current as App).settings);
            }*/

            AutoUpdater.ParseUpdateInfoEvent -= AutoUpdaterOnParseUpdateInfoEvent;
            AutoUpdater.CheckForUpdateEvent -= AutoUpdaterOnCheckForUpdateEvent;
            AutoUpdater.ParseUpdateInfoEvent += AutoUpdaterOnParseUpdateInfoEvent;
            AutoUpdater.CheckForUpdateEvent += AutoUpdaterOnCheckForUpdateEvent;

            if (!manualUpdate) SplashWindow.Loading("Checking for updates");

            manual = manualUpdate;

            AutoUpdater.Start(url);
        }

        private void AutoUpdaterOnParseUpdateInfoEvent(ParseUpdateInfoEventArgs args)
        {
            try
            {
                ChangelogText = $"\nChangelog{Environment.NewLine}";

                using (StringReader txtReader = new StringReader(args.RemoteData))
                {
                    XmlSerializer xmls = new XmlSerializer(typeof(UpdaterArgs));

                    if (xmls.Deserialize(txtReader) is UpdaterArgs updaterArgs)
                    {
                        args.UpdateInfo = new UpdateInfoEventArgs
                        {
                            CurrentVersion = updaterArgs.Version,
                            DownloadURL = updaterArgs.Url,
                            ChangelogURL = updaterArgs.Changelog,
                            Mandatory = new Mandatory
                            {
                                Value = manual,
                                UpdateMode = Mode.Normal
                            },
                            CheckSum = new CheckSum
                            {
                                Value = updaterArgs.Checksum.Value,
                                HashingAlgorithm = updaterArgs.Checksum.algorithm
                            }
                        };

                        foreach (string change in updaterArgs.Changes)
                            ChangelogText += $" - {change}{Environment.NewLine}";
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private void AutoUpdaterOnCheckForUpdateEvent(UpdateInfoEventArgs args)
        {
            if (args != null && args.Error == null)
            {
                if (args.IsUpdateAvailable)
                {
                    MessageBoxResult result = MessageBox.Show(
                        $"There is new version {args.CurrentVersion} available.{Environment.NewLine}" +
                        $"You are using version {args.InstalledVersion}.{Environment.NewLine}" +
                        $"{ChangelogText}{Environment.NewLine}" +
                        "Do you want to update the application now?",
                        @"Update Available",
                        MessageBoxButton.YesNo
                    );

                    if (!manual) SplashWindow.splash.Hide();

                    if (result.Equals(MessageBoxResult.Yes))
                    {
                        try
                        {
                            if (AutoUpdater.DownloadUpdate(args))
                            {
                                if (!manual) SplashWindow.Stop();
                                Application.Current.Shutdown();
                                Environment.Exit(0);
                            }
                        }
                        catch (Exception exception)
                        {
                            MessageBox.Show(
                                exception.Message,
                                exception.GetType().ToString(),
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                        }
                    }

                    if (!manual) SplashWindow.splash.Show();
                }
                else if (manual)
                {
                    OnUpdateCheckCompleteEvent(new EventArgs());
                }
            }
            else
            {
                if (args.Error is WebException)
                {
                    MessageBox.Show(
                        @"There is a problem reaching update server. Please check your internet connection and try again later.",
                        @"Update Check Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                else
                {
                    MessageBox.Show(
                        @"There is a problem reaching update server. The reason is unknown.",
                        @"Update Check Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }
    }
}