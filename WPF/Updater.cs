using AutoUpdaterDotNET;
using System;
using System.IO;
using System.Net;
using System.Windows;
using System.Xml.Serialization;
using ZenTimings.Windows;

namespace ZenTimings
{
    public partial class Updater
    {
        public event EventHandler UpdateCheckCompleteEvent;

        //public static int status = 0;
        private static bool manual = false;
        private static string ChangelogText { get; set; }
        private const string url = "https://zentimings.protonrom.com/AutoUpdater.xml";

        protected virtual void OnUpdateCheckCompleteEvent(EventArgs e)
        {
            // Make a temporary copy of the event to avoid possibility of
            // a race condition if the last subscriber unsubscribes
            // immediately after the null check and before the event is raised.
            UpdateCheckCompleteEvent?.Invoke(this, e);
        }

        public Updater(AppSettings settingsInstance)
        {
            Init(settingsInstance);
        }

        public void Init(AppSettings settings)
        {
            AutoUpdater.RunUpdateAsAdmin = true;
            AutoUpdater.Synchronous = true;
            AutoUpdater.LetUserSelectRemindLater = false;
            AutoUpdater.RemindLaterTimeSpan = RemindLaterFormat.Days;
            AutoUpdater.RemindLaterAt = 3;
            AutoUpdater.DownloadPath = Environment.CurrentDirectory;
            AutoUpdater.PersistenceProvider = new UpdaterPersistenceProvider(settings);
            //status = 1;
        }

        public void CheckForUpdate(bool manualUpdate = false)
        {
            /*if (status == 0)
            {
                Init((Application.Current as App).settings);
            }*/

            AutoUpdater.ParseUpdateInfoEvent += AutoUpdaterOnParseUpdateInfoEvent;
            AutoUpdater.CheckForUpdateEvent += AutoUpdaterOnCheckForUpdateEvent;

            if (!manualUpdate)
            {
                SplashWindow.Loading("Checking for updates");
            }

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
                    UpdaterArgs updaterArgs = xmls.Deserialize(txtReader) as UpdaterArgs;

                    args.UpdateInfo = new UpdateInfoEventArgs
                    {
                        CurrentVersion = updaterArgs.Version,
                        DownloadURL = updaterArgs.Url,
                        ChangelogURL = updaterArgs.Changelog,
                        Mandatory = new Mandatory
                        {
                            Value = manual,
                            UpdateMode = Mode.Normal,
                        },
                        CheckSum = new CheckSum
                        {
                            Value = updaterArgs.Checksum.Value,
                            HashingAlgorithm = updaterArgs.Checksum.algorithm
                        }
                    };

                    foreach (string change in updaterArgs.Changes)
                    {
                        ChangelogText += $" - {change}{Environment.NewLine}";
                    }
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private void AutoUpdaterOnCheckForUpdateEvent(UpdateInfoEventArgs args)
        {
            if (args.Error == null)
            {
                if (args.IsUpdateAvailable)
                {
                    var messageBox = new AdonisUI.Controls.MessageBoxModel
                    {
                        Text = $"There is new version {args.CurrentVersion} available.{Environment.NewLine}" +
                            $"You are using version {args.InstalledVersion}.{Environment.NewLine}" +
                            $"{ChangelogText}{Environment.NewLine}" +
                            $"Do you want to update the application now?",
                        Caption = @"Update Available",
                        Buttons = AdonisUI.Controls.MessageBoxButtons.YesNo()
                    };

                    AdonisUI.Controls.MessageBox.Show(messageBox);

                    if (!manual)
                    {
                        SplashWindow.splash.Hide();
                    }

                    if (messageBox.Result.Equals(AdonisUI.Controls.MessageBoxResult.Yes))
                    {
                        try
                        {
                            if (AutoUpdater.DownloadUpdate(args))
                            {
                                if (!manual)
                                {
                                    SplashWindow.Stop();
                                }
                                Application.Current.Shutdown();
                                Environment.Exit(0);
                            }
                        }
                        catch (Exception exception)
                        {
                            AdonisUI.Controls.MessageBox.Show(
                                exception.Message,
                                exception.GetType().ToString(),
                                AdonisUI.Controls.MessageBoxButton.OK,
                                AdonisUI.Controls.MessageBoxImage.Error);
                        }
                    }

                    if (!manual)
                    {
                        SplashWindow.splash.Show();
                    }
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
                    AdonisUI.Controls.MessageBox.Show(
                        @"There is a problem reaching update server. Please check your internet connection and try again later.",
                        @"Update Check Failed",
                        AdonisUI.Controls.MessageBoxButton.OK,
                        AdonisUI.Controls.MessageBoxImage.Error);
                }
                else
                {
                    AdonisUI.Controls.MessageBox.Show(
                        args.Error.Message,
                        args.Error.GetType().ToString(),
                        AdonisUI.Controls.MessageBoxButton.OK,
                        AdonisUI.Controls.MessageBoxImage.Error);
                }
            }
            AutoUpdater.ParseUpdateInfoEvent -= AutoUpdaterOnParseUpdateInfoEvent;
            AutoUpdater.CheckForUpdateEvent -= AutoUpdaterOnCheckForUpdateEvent;
        }
    }
}
