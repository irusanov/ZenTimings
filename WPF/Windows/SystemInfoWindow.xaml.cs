using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using ZenStates.Core;
using static ZenTimings.BiosMemController;

namespace ZenTimings.Windows
{
    /// <summary>
    /// Interaction logic for SystemInfoWindow.xaml
    /// </summary>
    public partial class SystemInfoWindow
    {
        internal readonly AppSettings appSettings = (Application.Current as App)?.settings;
        private class GridItem
        {
            public string Name { get; set; }
            public string Value { get; set; }
        }

        public SystemInfoWindow(SystemInfo si, MemoryConfig mc, Resistances mcConfig, AodData aodData, List<AsusSensorInfo> asusSensors)
        {
            InitializeComponent();
            Type type = si.GetType();
            PropertyInfo[] properties = type.GetProperties();
            List<GridItem> items;

            try
            {
                items = new List<GridItem>
                {
                    new GridItem() {Name = "OS", Value = new Microsoft.VisualBasic.Devices.ComputerInfo().OSFullName}
                };

                foreach (PropertyInfo property in properties)
                    if (property.Name == "CpuId" || property.Name == "PatchLevel" || property.Name == "SmuTableVersion")
                        items.Add(new GridItem() { Name = property.Name, Value = $"{property.GetValue(si, null):X8}" });
                    else if (property.Name == "SmuVersion")
                        items.Add(new GridItem() { Name = property.Name, Value = si.GetSmuVersionString() });
                    else
                        items.Add(new GridItem()
                        { Name = property.Name, Value = property.GetValue(si, null).ToString() });

                TestGrid.ItemsSource = items;
            }
            catch
            {
                // ignored
            }

            type = mc.GetType();
            properties = type.GetProperties();

            try
            {
                items = new List<GridItem>();
                foreach (PropertyInfo property in properties)
                    items.Add(new GridItem() { Name = property.Name, Value = property.GetValue(mc, null).ToString() });

                MemCfgGrid.ItemsSource = items;
            }
            catch
            {
                // ignored
            }

            if (mc.Type == MemoryConfig.MemType.DDR4)
            {
                type = mcConfig.GetType();
                FieldInfo[] fields = type.GetFields();
                try
                {
                    items = new List<GridItem>();
                    foreach (FieldInfo property in fields)
                        items.Add(new GridItem() { Name = property.Name, Value = property.GetValue(mcConfig).ToString() });

                    MemControllerGrid.ItemsSource = items;
                }
                catch
                {
                    // ignored
                }
            }
            else
            {
                type = aodData.GetType();
                FieldInfo[] fields = type.GetFields();
                try
                {
                    items = new List<GridItem>();
                    foreach (FieldInfo property in fields)
                        items.Add(new GridItem() { Name = property.Name, Value = property.GetValue(aodData).ToString() });

                    MemControllerGrid.ItemsSource = items;
                }
                catch
                {
                    // ignored
                }
            }

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

        private void AdonisWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (appSettings.SaveWindowPosition)
            {
                appSettings.SysInfoWindowLeft = Left;
                appSettings.SysInfoWindowTop = Top;
                appSettings.SysInfoWindowHeight = Height;
                appSettings.SysInfoWindowWidth = Width;
                appSettings.Save();
            }
        }
    }
}