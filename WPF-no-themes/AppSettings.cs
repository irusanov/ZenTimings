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
            CheckForUpdates = true;
            IsRestarting = false;

            Save();

            return this;
        }

        public AppSettings Load()
        {
            if (File.Exists(filename))
            {
                using (StreamReader sr = new StreamReader(filename))
                {
                    try
                    {
                        XmlSerializer xmls = new XmlSerializer(typeof(AppSettings));
                        return xmls.Deserialize(sr) as AppSettings;
                    }
                    catch (InvalidOperationException ex)
                    {
                        Console.WriteLine(ex.Message);
                        sr.Close();
                        MessageBox.Show(
                            "Invalid settings file!\nSettings will be reset to defaults.",
                            "Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return Create();
                    }
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

        public bool AutoRefresh { get; set; } = true;
        public int AutoRefreshInterval { get; set; } = 2000;
        public bool AdvancedMode { get; set; } = true;
        public bool DarkMode { get; set; }
        public bool CheckForUpdates { get; set; } = true;
        public string UpdaterSkippedVersion { get; set; } = "";
        public string UpdaterRemindLaterAt { get; set; } = "";
        public bool IsRestarting { get; set; }
    }
}
