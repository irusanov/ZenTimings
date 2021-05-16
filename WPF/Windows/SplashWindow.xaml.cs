using System.Windows;

namespace ZenTimings.Windows
{
    /// <summary>
    /// Interaction logic for SplashWindow.xaml
    /// </summary>
    public partial class SplashWindow : Window
    {
        public static readonly SplashWindow splash = new SplashWindow();
        private static readonly Updater updater = (Application.Current as App).updater;

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
            // One-time notification dialog
            if (!settings.NotifiedForAutoUpdate)
            {
                MessageBoxResult result = MessageBox.Show(
                    "The app now includes Auto Update and will check for a new version each startup.\n" +
                    "Do you want to disable the feature now?\n\n" +
                    "You can always change the setting in Options dialog.",
                    "AutoUpdate",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result.Equals(MessageBoxResult.Yes))
                    settings.CheckForUpdates = false;

                settings.NotifiedForAutoUpdate = true;
                settings.Save();
            }

            splash.Show();

            if (settings.DarkMode)
                settings.ChangeTheme();

            if (settings.CheckForUpdates)
            {
                updater.CheckForUpdate();
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
