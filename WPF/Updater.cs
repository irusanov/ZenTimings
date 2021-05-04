using AutoUpdaterDotNET;
using System;
using System.Collections.Generic;
using System.Net;
using System.Windows;
using System.Xml.Linq;
using ZenTimings.Windows;

namespace ZenTimings
{
    public partial class Updater
    {
        public static int status = 0;
        private static bool manual = false;
        private static string ChangelogText { get; set; }

        public static void Init(AppSettings settings)
        {
            AutoUpdater.RunUpdateAsAdmin = true;
            AutoUpdater.Synchronous = true;
            AutoUpdater.LetUserSelectRemindLater = false;
            AutoUpdater.RemindLaterTimeSpan = RemindLaterFormat.Days;
            AutoUpdater.RemindLaterAt = 3;
            AutoUpdater.DownloadPath = Environment.CurrentDirectory;
            AutoUpdater.PersistenceProvider = new UpdaterPersistenceProvider(settings);
            AutoUpdater.CheckForUpdateEvent += AutoUpdaterOnCheckForUpdateEvent;
            AutoUpdater.ParseUpdateInfoEvent += AutoUpdaterOnParseUpdateInfoEvent;
            status = 1;
        }

        public static void CheckForUpdate(bool manualUpdate = false)
        {
            if (status == 0)
            {
                Init((Application.Current as App).settings);
            }

            if (!manualUpdate)
            {
                SplashWindow.Loading("Checking for updates");
            }

            manual = manualUpdate;

            AutoUpdater.Start("https://zentimings.protonrom.com/AutoUpdater.xml");
        }

        private static void AutoUpdaterOnParseUpdateInfoEvent(ParseUpdateInfoEventArgs args)
        {
            XElement data = XElement.Parse(args.RemoteData);
            if (data != null)
            {
                args.UpdateInfo = new UpdateInfoEventArgs
                {
                    CurrentVersion = data.Element("version").Value,
                    DownloadURL = data.Element("url").Value,
                    ChangelogURL = data.Element("changelog").Value,
                    Mandatory = new Mandatory
                    {
                        Value = Convert.ToBoolean(data.Element("mandatory").Value),
                    },
                    CheckSum = new CheckSum
                    {
                        Value = data.Element("checksum").Value,
                        HashingAlgorithm = "MD5"
                    }
                };

                ChangelogText = $"\nChangelog{Environment.NewLine}";
                IEnumerable<XElement> changes = data.Descendants("change");
                foreach (XElement change in changes)
                    ChangelogText += $"- {change.Value}{Environment.NewLine}";
            }
        }

        private static void AutoUpdaterOnCheckForUpdateEvent(UpdateInfoEventArgs args)
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
        }
    }
}
