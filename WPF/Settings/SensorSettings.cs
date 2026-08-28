using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace ZenTimings
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
