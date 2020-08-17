using System;
using System.Threading;
using System.Windows.Forms;

namespace ZenTimings
{
    static class Program
    {
        /// <summary>
        /// Name of our mutex
        /// </summary>
        private const string mutexName = "Local\\ZenTimings";

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            try
            {
                Mutex.OpenExisting(mutexName);
                NativeMethods.PostMessage((IntPtr)NativeMethods.HWND_BROADCAST, NativeMethods.WM_SHOWME, IntPtr.Zero, IntPtr.Zero);
                return;
            }
            catch
            {
                using (Mutex instanceMutex = new Mutex(true, mutexName, out _))
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    //SplashForm.ShowSplashScreen();
                    Application.Run(new MainForm());
                    GC.KeepAlive(instanceMutex);
                }
            }
        }
    }
}
