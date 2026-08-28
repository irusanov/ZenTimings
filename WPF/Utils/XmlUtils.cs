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

        /// <summary>
        /// Deserializes an object of type <typeparamref name="T"/> from an XML file on disk.
        /// </summary>
        /// <param name="filePath">Path to a file containing XML content.</param>
        public static T DeserializeFromXmlFile<T>(string filePath)
        {
            using (StreamReader reader = new StreamReader(filePath))
            {
                return DeserializeFromXmlReader<T>(reader);
            }
        }

        /// <summary>
        /// Deserializes an object of type <typeparamref name="T"/> from an in-memory XML string.
        /// </summary>
        /// <param name="xml">The XML content itself (not a file path).</param>
        public static T DeserializeFromXmlString<T>(string xml)
        {
            using (StringReader reader = new StringReader(xml))
            {
                return DeserializeFromXmlReader<T>(reader);
            }
        }

        private static T DeserializeFromXmlReader<T>(TextReader reader)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(T));
            return (T)serializer.Deserialize(reader);
        }
    }
}
