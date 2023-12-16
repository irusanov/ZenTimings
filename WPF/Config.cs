using System;
using System.IO;
using System.Xml.Serialization;

namespace ZenTimings
{
    public class Config
    {
        public Config(MemoryConfig memoryConfig, BiosMemController.Resistances memControllerConfig/*, PowerTable powerTable*/)
        {
            MemoryConfig = memoryConfig ?? throw new ArgumentNullException(nameof(memoryConfig));
            MemControllerConfig = memControllerConfig;
            //PowerTable = powerTable ?? throw new ArgumentNullException(nameof(powerTable));
        }

        public Config() { }

        [XmlElement("Memory")]
        public MemoryConfig MemoryConfig { get; set; }

        [XmlElement("Controller")]
        public BiosMemController.Resistances MemControllerConfig { get; set; }

        //public PowerTable PowerTable { get; set; }

        public string GetXML()
        {
            XmlSerializer x = new XmlSerializer(this.GetType());
            using (StringWriter textWriter = new StringWriter())
            {
                x.Serialize(textWriter, this);
                return textWriter.ToString();
            }
        }
    }
}
