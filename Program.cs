using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace ZenTimings
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Form MainForm = new MainForm();
            string appString = $"{Application.ProductName} {Application.ProductVersion.Substring(0, Application.ProductVersion.LastIndexOf('.'))}";
#if DEBUG
            appString += " (debug)";
#endif
            MainForm.Text = appString;
            Application.Run(MainForm);
        }
    }
}
