using System;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using ZenStates.Core;
using ZenTimings.Windows;

namespace ZenTimings
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        internal const string mutexName = "Local\\ZenTimings";
        internal static Mutex instanceMutex;
        internal bool createdNew;
        public Updater updater;

        protected override void OnStartup(StartupEventArgs e)
        {
            instanceMutex = new Mutex(true, mutexName, out createdNew);

            if (!createdNew)
            {
                // App is already running! Exit the application and show the other window.
                InteropMethods.PostMessage((IntPtr)InteropMethods.HWND_BROADCAST, InteropMethods.WM_SHOWME,
                    IntPtr.Zero, IntPtr.Zero);
                Current.Shutdown();
                Environment.Exit(0);
            }

            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");
            FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(
                        XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)));

            updater = new Updater();

            GC.KeepAlive(instanceMutex);
            SplashWindow.Start();
            base.OnStartup(e);
        }
    }
}