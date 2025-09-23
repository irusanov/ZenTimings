using System;
using System.IO;
using System.Xml.Serialization;
using ZenStates.Core.DRAM;

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

        public string GetHTML()
        {
            var cpu = CpuSingleton.Instance;
            var type = cpu.systemInfo.GetType();
            var properties = type.GetProperties();

            // Example HTML structure
            string html = "<!DOCTYPE html>" +
                          "<html>" +
                          "<head><title>System Information</title></head>" +
                          "<body>" +
                          "<h1>System Information</h1>";

            html += "<h2>System Info</h2>";
            html += "<table>";

            foreach (var property in properties)
            {
                if (property.Name == "CpuId" || property.Name == "PatchLevel" || property.Name == "SmuTableVersion")
                    html += $"<tr><td>{property.Name}</td><td>{property.GetValue(cpu.systemInfo, null):X8}</td></tr>";
                else if (property.Name == "SmuVersion")
                    html += $"<tr><td>{property.Name}</td><td>{cpu.systemInfo.GetSmuVersionString()}</td></tr>";
                else if (property.Name == "Model" || property.Name == "ExtendedModel" || property.Name == "BaseModel")
                    html += $"<tr><td>{property.Name}</td><td>{property.GetValue(cpu.systemInfo, null)} (0x{property.GetValue(cpu.systemInfo, null):X})</td></tr>";
                else
                    html += $"<tr><td>{property.Name}</td><td>{property.GetValue(cpu.systemInfo, null)}</td></tr>";

            }

            html += "</table>";
            // Add system info (replace with actual data from your application)
            html += "<h2>Timings</h2>";
            html += "<p>Example timing data goes here.</p>";

            html += "<h2>Other Information</h2>";
            html += "<p>Additional system information goes here.</p>";

            html += "</body></html>";

            return html;
        }
    }
}
