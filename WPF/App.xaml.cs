using System;
using System.Threading;
using System.Windows;
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
        public readonly AppSettings settings = new AppSettings().Load();
        public Updater updater;

        protected override void OnStartup(StartupEventArgs e)
        {
            instanceMutex = new Mutex(true, mutexName, out createdNew);

            if (!createdNew)
            {
                // App is already running! Exit the application and show the other window.
                InteropMethods.PostMessage((IntPtr) InteropMethods.HWND_BROADCAST, InteropMethods.WM_SHOWME,
                    IntPtr.Zero, IntPtr.Zero);
                Current.Shutdown();
                Environment.Exit(0);
            }

            updater = new Updater(settings);

            GC.KeepAlive(instanceMutex);
            base.OnStartup(e);
            SplashWindow.Start(settings);
        }
    }
}