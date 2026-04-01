using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ZenStates.Core;
using ZenStates.Core.DRAM;
using static ZenTimings.BiosMemController;

namespace ZenTimings.Windows
{
    /// <summary>
    /// Interaction logic for SystemInfoWindow.xaml
    /// </summary>
    public partial class SystemInfoWindow : ThemedAdonisWindow
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
                    else if (property.Name != "SMBios")
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
                var memConfigs = CpuSingleton.Instance.GetMemoryConfig();
                var allTimings = memConfigs.Timings;
                var props = allTimings[0].Value.GetType().GetProperties();

                // Filter timings to only include unique DctOffset values
                var uniqueTimings = allTimings
                    .GroupBy(t => t.Key)
                    .Select(g => g.First())
                    .ToList();

                // Create dynamic object with properties for each timing column
                var rows = props
                    .Where(p => p.Name != "Item")
                    .Select(property => new
                    {
                        PropertyName = property.Name,
                        Values = uniqueTimings.Select(t => t.Value[property.Name].ToString()).ToArray()
                    })
                    .ToList();

                MemCfgGrid.ItemsSource = rows;

                // Ensure columns exist for each unique timing
                if (MemCfgGrid.Columns.Count < uniqueTimings.Count + 1)
                {
                    MemCfgGrid.Columns.Clear();

                    // Add property name column with default text color
                    var nameColumn = new System.Windows.Controls.DataGridTextColumn
                    {
                        Header = "Name",
                        Binding = new System.Windows.Data.Binding("PropertyName"),
                        Foreground = (System.Windows.Media.Brush)this.FindResource("TextColor"),
                        Width = 150
                    };
                    MemCfgGrid.Columns.Add(nameColumn);

                    // Add column for each unique timing with accent text color
                    for (int i = 0; i < uniqueTimings.Count; i++)
                    {
                        var valueColumn = new System.Windows.Controls.DataGridTextColumn
                        {
                            Header = $"DCT {uniqueTimings[i].Key >> 20}",
                            Binding = new System.Windows.Data.Binding($"Values[{i}]"),
                            Foreground = (System.Windows.Media.Brush)this.FindResource("AccentTextColor")
                        };
                        MemCfgGrid.Columns.Add(valueColumn);
                    }
                }
            }
            catch
            {
                // ignored
            }

            if (mcConfig != null && mc.Type == MemType.DDR4 || mc.Type == MemType.LPDDR4)
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

            if (CpuSingleton.Instance.info.apob.IsAvailable)
            {
                try
                {
                    var apobData = CpuSingleton.Instance.info.apob.Data;
                    type = apobData.GetType();
                    properties = type.GetProperties();
                    items = new List<GridItem>();
                    foreach (PropertyInfo property in properties)
                    {
                        object value = property.GetValue(apobData);
                        items.Add(new GridItem() { Name = property.Name, Value = $"{value}" });
                    }
                    ApobTableGrid.ItemsSource = items;
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