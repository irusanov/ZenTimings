using AdonisUI.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ZenStates.Core;

namespace ZenTimings.Windows
{
    /// <summary>
    /// Interaction logic for SystemInfoWindow.xaml
    /// </summary>
    public partial class SystemInfoWindow : AdonisWindow
    {
        private class GridItem
        {
            public string Name { get; set; }
            public string Value { get; set; }
        }

        public SystemInfoWindow(SystemInfo si, MemoryConfig mc)
        {
            InitializeComponent();
            Type type = si.GetType();
            PropertyInfo[] properties = type.GetProperties();
            List<GridItem> items;

            try
            {
                items = new List<GridItem>
                {
                    new GridItem() { Name = "OS", Value = new Microsoft.VisualBasic.Devices.ComputerInfo().OSFullName }
                };

                foreach (PropertyInfo property in properties)
                {
                    if (property.Name == "CpuId" || property.Name == "PatchLevel" || property.Name == "SmuTableVersion")
                        items.Add(new GridItem() { Name = property.Name, Value = $"{property.GetValue(si, null):X8}" });
                    else if (property.Name == "SmuVersion")
                        items.Add(new GridItem() { Name = property.Name, Value = si.GetSmuVersionString() });
                    else
                        items.Add(new GridItem() { Name = property.Name, Value = property.GetValue(si, null).ToString() });
                }

                TestGrid.ItemsSource = items;
            }
            catch
            {
            }

            type = mc.GetType();
            properties = type.GetProperties();

            try
            {
                items = new List<GridItem>();
                foreach (PropertyInfo property in properties)
                {
                    items.Add(new GridItem() { Name = property.Name, Value = property.GetValue(mc, null).ToString() });
                }

                MemCfgGrid.ItemsSource = items;
            }
            catch
            {
            }
        }

        private void AdonisWindow_Activated(object sender, EventArgs e)
        {
            InteropMethods.EmptyWorkingSet(System.Diagnostics.Process.GetCurrentProcess().Handle);
        }
    }
}
