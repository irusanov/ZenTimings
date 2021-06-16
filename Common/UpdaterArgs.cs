namespace ZenTimings
{
    [System.Serializable()]
    [System.ComponentModel.DesignerCategory("code")]
    [System.Xml.Serialization.XmlType(AnonymousType = true)]
    [System.Xml.Serialization.XmlRoot(Namespace = "", IsNullable = false)]
    public class UpdaterArgs
    {
        public string Version { get; set; }

        public string Url { get; set; }

        public string Changelog { get; set; }

        public bool Mandatory { get; set; }

        public UpdaterArgsChecksum Checksum { get; set; }

        [System.Xml.Serialization.XmlArrayItem("Change", IsNullable = false)]
        public string[] Changes { get; set; }
    }

    [System.Serializable()]
    [System.ComponentModel.DesignerCategory("code")]
    [System.Xml.Serialization.XmlType(AnonymousType = true)]
    public class UpdaterArgsChecksum
    {
        [System.Xml.Serialization.XmlAttribute()]
        public string algorithm { get; set; }

        [System.Xml.Serialization.XmlText()]
        public string Value { get; set; }
    }
}