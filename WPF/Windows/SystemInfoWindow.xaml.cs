using AdonisUI.Controls;
using System;
using System.Collections.Generic;
using System.Reflection;
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

        public SystemInfoWindow(SystemInfo si, MemoryConfig mc, List<AsusSensorInfo> asusSensors)
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
            catch { }

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
            catch { }

            //AsusWmiGrid.ItemsSource = asusSensors;

            DataContext = new
            {
                asusSensors
            };
        }

        private void AdonisWindow_Activated(object sender, EventArgs e)
        {
            InteropMethods.EmptyWorkingSet(System.Diagnostics.Process.GetCurrentProcess().Handle);
        }
    }
}
