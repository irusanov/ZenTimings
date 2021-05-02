using System.Windows;

namespace ZenTimings.Windows
{
    /// <summary>
    /// Interaction logic for SplashWindow.xaml
    /// </summary>
    public partial class SplashWindow : Window
    {
        public static readonly SplashWindow splash = new SplashWindow();

        // To refresh the UI immediately
        private delegate void RefreshDelegate();
        private static void Refresh(DependencyObject obj)
        {
            obj.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Render,
                (RefreshDelegate)delegate { });
        }

        public SplashWindow()
        {
            InitializeComponent();
        }

        public static void Start(AppSettings settings)
        {
            splash.Show();

            if (settings.DarkMode)
                settings.ChangeTheme();

            if (settings.CheckForUpdates)
            {
                Updater.Init(settings);
                Updater.CheckForUpdate();
            }
        }

        public static void Stop() => splash.Close();

        public static void Loading(string status)
        {
            splash.status.Content = status;
            Refresh(splash.status);
        }
    }
}
