using AdonisUI;
using System;
using System.IO;
using System.Windows;
using System.Xml.Serialization;

namespace ZenTimings
{
    [Serializable]
    public sealed class AppSettings
    {
        private const int VERSION_MAJOR = 1;
        private const int VERSION_MINOR = 0;

        private const string filename = "settings.xml";

        public AppSettings Create()
        {
            AutoRefresh = true;
            AutoRefreshInterval = 2000;
            AdvancedMode = true;
            DarkMode = false;
            IsRestarting = false;

            Save();

            return this;
        }

        public AppSettings Load()
        {
            if (File.Exists(filename))
            {
                using (StreamReader sw = new StreamReader(filename))
                {
                    XmlSerializer xmls = new XmlSerializer(typeof(AppSettings));
                    return xmls.Deserialize(sw) as AppSettings;
                }
            }
            else
            {
                return Create();
            }
        }

        public void Save()
        {
            using (StreamWriter sw = new StreamWriter(filename))
            {
                XmlSerializer xmls = new XmlSerializer(typeof(AppSettings));
                xmls.Serialize(sw, this);
            }
        }

        public void ChangeTheme()
        {
            Uri DarkColorScheme = new Uri("pack://application:,,,/ZenTimings;component/Themes/Dark.xaml", UriKind.Absolute);
            Uri LightColorScheme = new Uri("pack://application:,,,/ZenTimings;component/Themes/Light.xaml", UriKind.Absolute);

            if (DarkMode)
                ResourceLocator.SetColorScheme(Application.Current.Resources, DarkColorScheme);
            else
                ResourceLocator.SetColorScheme(Application.Current.Resources, LightColorScheme);

            //DarkMode = !DarkMode;
        }

        public bool AutoRefresh { get; set; }
        public int AutoRefreshInterval { get; set; }
        public bool AdvancedMode { get; set; }
        public bool DarkMode { get; set; }
        public bool IsRestarting { get; set; }
    }
}
