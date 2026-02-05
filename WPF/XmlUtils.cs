using System.IO;
using System.Xml.Serialization;

namespace ZenTimings
{
    internal class XmlUtils
    {
        public static string SerializeToXml<T>(T obj)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(T));
            using (StringWriter writer = new StringWriter())
            {
                serializer.Serialize(writer, obj);
                return writer.ToString();
            }
        }

        public static T DeserializeFromXml<T>(string xml)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(T));
            using (StreamReader reader = new StreamReader(xml))
            {
                return (T)serializer.Deserialize(reader);
            }
        }
    }
}
