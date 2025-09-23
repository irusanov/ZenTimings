using System;
using System.Collections.Generic;
using System.Reflection;
using ZenStates.Core;
using static ZenTimings.BiosMemController;

namespace ZenTimings.Windows
{
    /// <summary>
    /// Interaction logic for SystemInfoWindow.xaml
    /// </summary>
    public partial class SystemInfoWindow
    {
        private class GridItem
        {
            public string Name { get; set; }
            public string Value { get; set; }
        }

        public SystemInfoWindow(MemoryConfig mc, Resistances? mcConfig, List<AsusSensorInfo> asusSensors)
        {
            InitializeComponent();
            SystemInfo si = CpuSingleton.Instance.systemInfo;
            AodData aodData = CpuSingleton.Instance.info.aod.Table.Data;
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

            try
            {
                items = new List<GridItem>();

                var timings = CpuSingleton.Instance.GetMemoryConfig().Timings[0].Value;
                type = timings.GetType();
                properties = type.GetProperties();

                foreach (PropertyInfo property in properties)
                {
                    if (property.Name != "Item")
                        items.Add(new GridItem() { Name = property.Name, Value = $"{timings[property.Name]}" });
                }

                MemCfgGrid.ItemsSource = items;
            }
            catch
            {
                // ignored
            }

            if (mcConfig != null && mc.Type == MemoryConfig.MemType.DDR4)
            {
                try
                {
                    type = mcConfig.GetType();
                    FieldInfo[] fields = type.GetFields();
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
                try
                {
                    properties = aodData.GetType().GetProperties();
                    items = new List<GridItem>();
                    foreach (PropertyInfo property in properties)
                    {
                        object value = property.GetValue(aodData);
                        items.Add(new GridItem() { Name = property.Name, Value = $"{value}" });
                    }

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
            AppSettings appSettings = AppSettings.Instance;
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