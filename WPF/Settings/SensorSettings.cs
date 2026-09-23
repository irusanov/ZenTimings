using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using ZenTimings.Utils;

namespace ZenTimings.Settings
{
    [Serializable]
    public sealed class SensorSettings
    {
        private static readonly string Filename = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings_sensors.xml");

        private static SensorSettings _instance;

        public static SensorSettings Instance
        {
            get
            {
                if (_instance == null)
                    _instance = Load();

                return _instance;
            }
        }

        public List<string> HiddenSensors { get; set; } = new List<string>();
        public double SensorColumnWidth { get; set; } = 120.0;
        public double CurrentColumnWidth { get; set; } = 64.0;
        public double MinColumnWidth { get; set; } = 54.0;
        public double MaxColumnWidth { get; set; } = 54.0;
        public double AverageColumnWidth { get; set; } = 64.0;

        private static SensorSettings Load()
        {
            try
            {
                if (File.Exists(Filename))
                {
                    return XmlUtils.DeserializeFromXmlFile<SensorSettings>(Filename);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

            return new SensorSettings();
        }

        public void Save()
        {
            try
            {
                string xmlContent = XmlUtils.SerializeToXml(this);
                File.WriteAllText(Filename, xmlContent);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }
    }
}
