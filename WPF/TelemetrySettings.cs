using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace ZenTimings
{
    [Serializable]
    public sealed class TelemetrySettings
    {
        private static readonly string Filename = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "telemetry_settings.xml");

        private static TelemetrySettings _instance;

        public static TelemetrySettings Instance
        {
            get
            {
                if (_instance == null)
                    _instance = Load();

                return _instance;
            }
        }

        public List<string> HiddenSensors { get; set; } = new List<string>();

        private static TelemetrySettings Load()
        {
            try
            {
                if (File.Exists(Filename))
                {
                    return XmlUtils.DeserializeFromXml<TelemetrySettings>(Filename);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

            return new TelemetrySettings();
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
