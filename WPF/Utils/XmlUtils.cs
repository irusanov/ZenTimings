using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace ZenTimings.Utils
{
    internal class XmlUtils
    {
        // A StringWriter reports UTF-16, which the serializer then declares in the XML. The files are written as UTF-8.
        private sealed class Utf8StringWriter : StringWriter
        {
            public override Encoding Encoding => Encoding.UTF8;
        }

        public static string SerializeToXml<T>(T obj)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(T));
            using (StringWriter writer = new Utf8StringWriter())
            {
                serializer.Serialize(writer, obj);
                return writer.ToString();
            }
        }

        /// <summary>
        /// Writes the text (UTF-8) to a temporary file next to the target and swaps it in, so a crash or a full disk
        /// in the middle of the write leaves the old file instead of a cut-off one.
        /// </summary>
        public static void WriteAllTextAtomic(string path, string content)
        {
            string temp = path + ".tmp";
            File.WriteAllText(temp, content);

            try
            {
                if (File.Exists(path))
                    File.Replace(temp, path, null);
                else
                    File.Move(temp, path);
            }
            catch
            {
                try { File.Delete(temp); } catch { }
                throw;
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
