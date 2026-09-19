using System;
using System.Diagnostics;
using System.IO;
using ZenTimings.Utils;

namespace ZenTimings.Export
{
    [Serializable]
    public sealed class ExportSettings
    {
        private static readonly string Filename = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings_export.xml");

        private static ExportSettings _instance;

        public static ExportSettings Instance
        {
            get
            {
                if (_instance == null)
                    _instance = Load();

                return _instance;
            }
        }

        public SnapshotSections ExportSections { get; set; } = SnapshotSections.Default;
        public bool IncludeLegend { get; set; } = true;
        public bool LiveSnapshotEnabled { get; set; } = false;
        public SnapshotFormat LiveSnapshotFormat { get; set; } = SnapshotFormat.Json;
        public SnapshotSections LiveSnapshotSections { get; set; } = SnapshotSections.Default;
        public int LiveSnapshotIntervalMs { get; set; } = 5000;

        // Empty means the application folder
        public string LiveSnapshotDirectory { get; set; } = "";

        // Without extension, empty means the default name
        public string LiveSnapshotFileName { get; set; } = "";

        private static ExportSettings Load()
        {
            try
            {
                if (File.Exists(Filename))
                {
                    return XmlUtils.DeserializeFromXmlFile<ExportSettings>(Filename);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

            return new ExportSettings();
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
