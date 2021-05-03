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

        private static void AutoUpdaterOnCheckForUpdateEvent(UpdateInfoEventArgs args)
        {
            if (args.Error == null)
            {
                if (args.IsUpdateAvailable)
                {
                    WebClient client = new WebClient();
                    string xml = client.DownloadString("https://zentimings.protonrom.com/AutoUpdater.xml");

                    XElement data = XElement.Parse(xml);
                    if (data != null)
                    {
                        IEnumerable<XElement> version = data.Descendants("version");
                        foreach (XElement v in version)
                            Console.WriteLine((string)v);
                    }

                    string text = $"\nChangelog{Environment.NewLine}" +
                        $"- Add Cezanne support (5000 Zen3 series APU){Environment.NewLine}" +
                        $"- Add Lucienne support(5000 Zen2 series APU){Environment.NewLine}" +
                        $"- Improve Epyc Rome support{Environment.NewLine}" +
                        $"- Add separate DCT readings for each installed DIMM{Environment.NewLine}" +
                        $"- Add Asus WMI sensors reading for boards that support it{Environment.NewLine}" +
                        $"- Fix startup for unsupported CPUs{Environment.NewLine}" +
                        $"- Reduce minimum.NET framework version for legacy app to 3.5{Environment.NewLine}";

                    var messageBox = new AdonisUI.Controls.MessageBoxModel
                    {
                        Text = $"There is new version {args.CurrentVersion} available.{Environment.NewLine}" +
                            $"You are using version {args.InstalledVersion}.{Environment.NewLine}" +
                            $"{text}{Environment.NewLine}" +
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
