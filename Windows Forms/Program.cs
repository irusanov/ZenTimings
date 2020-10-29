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
                Mutex existingInstane = Mutex.OpenExisting(mutexName);
                InteropMethods.PostMessage((IntPtr)InteropMethods.HWND_BROADCAST, InteropMethods.WM_SHOWME, IntPtr.Zero, IntPtr.Zero);

                if (Properties.Settings.Default.IsRestarting)
                {
                    existingInstane.ReleaseMutex();
                    Properties.Settings.Default.IsRestarting = true;
                    Properties.Settings.Default.Save();
                    throw new Exception("Restart requested, release mutex");
                }

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
