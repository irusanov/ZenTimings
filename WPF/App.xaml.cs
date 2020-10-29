using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading;
using System.Windows;
using ZenTimings;
using ZenTimings.Properties;

namespace ZenTimings
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private const string mutexName = "Global\\ZenTimings";
        private static Mutex instanceMutex = null;

        protected override void OnStartup(StartupEventArgs e)
        {
            instanceMutex = new Mutex(true, mutexName, out bool createdNew);

            if (!createdNew)
            {
                // App is already running! Exit the application and show the other window.
                Current.Shutdown();
                InteropMethods.PostMessage((IntPtr)InteropMethods.HWND_BROADCAST, InteropMethods.WM_SHOWME, IntPtr.Zero, IntPtr.Zero);
            }
            else
            {
                GC.KeepAlive(instanceMutex);
                base.OnStartup(e);
            }
        }
    }
}
