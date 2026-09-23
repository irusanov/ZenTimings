using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using ZenStates.Core.Hardware;
using ZenStates.Core.Hardware.Aod;
using ZenStates.Core.Hardware.DRAM;
using ZenStates.Core.OHWM;
using ZenTimings.Common;
using ZenTimings.Settings;
using ZenTimings.Utils;
using static ZenTimings.Common.BiosMemController;

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

        private class TimingGridItem
        {
            public string PropertyName { get; set; }
            public string[] Values { get; set; }
            public bool IsMismatch { get; set; }
        }

        public SystemInfoWindow(MemoryConfig mc, Resistances? mcConfig, List<AsusSensorInfo> asusSensors)
        {
            InitializeComponent();
            SystemInfo si = CpuSingleton.Instance.systemInfo;
            AodData aodData = CpuSingleton.Instance.info.aod?.Table?.Data;
            Type type = si.GetType();
            PropertyInfo[] properties = type.GetProperties();
            List<GridItem> items;

            try
            {
                items = new List<GridItem>
                {
                    new GridItem() {Name = "OS", Value = new Microsoft.VisualBasic.Devices.ComputerInfo().OSFullName}
                };

                var itemsToSkip = new HashSet<string> { "SMBios", "Hardware", "SensorGroups" };

                foreach (PropertyInfo property in properties)
                    if (property.Name == "CpuId" || property.Name == "PatchLevel" || property.Name == "SmuTableVersion")
                        items.Add(new GridItem() { Name = property.Name, Value = $"{property.GetValue(si, null):X8}" });
                    else if (property.Name == "SmuVersion")
                        items.Add(new GridItem() { Name = property.Name, Value = si.SmuVersion.ToString() });
                    else if (!itemsToSkip.Contains(property.Name))
                        items.Add(new GridItem() { Name = property.Name, Value = property.GetValue(si, null).ToString() });

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

                var rows = props
                    .Where(p => p.Name != "Item")
                    .Select(property =>
                    {
                        var values = uniqueTimings.Select(t => $"{t.Value[property.Name]}").ToArray();
                        return new TimingGridItem
                        {
                            PropertyName = property.Name,
                            Values = values,
                            IsMismatch = HasMismatch(values)
                        };
                    })
                    .ToList();

                MemCfgGrid.ItemsSource = rows;

                // Ensure columns exist for each unique timing
                if (MemCfgGrid.Columns.Count < uniqueTimings.Count + 1)
                {
                    MemCfgGrid.Columns.Clear();

                    var nameColumn = new System.Windows.Controls.DataGridTextColumn
                    {
                        Header = "Name",
                        Binding = new System.Windows.Data.Binding("PropertyName"),
                        ElementStyle = (System.Windows.Style)this.FindResource("TimingNameTextStyle"),
                        Width = 150
                    };
                    MemCfgGrid.Columns.Add(nameColumn);

                    for (int i = 0; i < uniqueTimings.Count; i++)
                    {
                        var valueColumn = new System.Windows.Controls.DataGridTextColumn
                        {
                            Header = $"DCT {uniqueTimings[i].Key >> 20}",
                            Binding = new System.Windows.Data.Binding($"Values[{i}]"),
                            ElementStyle = (System.Windows.Style)this.FindResource("TimingValueTextStyle")
                        };
                        MemCfgGrid.Columns.Add(valueColumn);
                    }
                }
            }
            catch
            {
                // ignored
            }

            try
            {
                if (mcConfig != null && (mc.Type == MemType.DDR4 || mc.Type == MemType.LPDDR4))
                {
                    MemControllerGrid.ItemsSource = GetItemsFromObject(mcConfig);
                }
                else
                {
                    MemControllerGrid.ItemsSource = GetItemsFromObject(aodData);
                }

                if (CpuSingleton.Instance.info.apob.IsValid)
                {
                    ApobTableGrid.ItemsSource = GetItemsFromObject(CpuSingleton.Instance.info.apob.Data);
                }
            }
            catch
            {
                // ignored
            }

            //AsusWmiGrid.ItemsSource = asusSensors;

            DataContext = new
            {
                asusSensors
            };
        }

        private static List<GridItem> GetItemsFromObject(object obj)
        {
            var items = new List<GridItem>();
            if (obj == null)
                return items;

            var type = obj.GetType();
            var properties = type.GetProperties();

            foreach (var property in properties)
            {
                object value = property.GetValue(obj);
                items.Add(new GridItem() { Name = property.Name, Value = $"{value ?? "-"}" });
            }

            return items;
        }

        private static bool HasMismatch(IReadOnlyList<string> values)
        {
            if (values == null || values.Count <= 1)
                return false;

            var first = values[0] ?? string.Empty;
            for (int i = 1; i < values.Count; i++)
                if (!string.Equals(first, values[i] ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                    return true;

            return false;
        }

        private void CopySection_Click(object sender, RoutedEventArgs e)
        {
            // The button comes from the header template, the group box that uses it names its grid in Tag
            if (!(sender is Button button)
                || !((button.TemplatedParent as FrameworkElement)?.TemplatedParent is GroupBox section)
                || !(section.Tag is DataGrid grid))
                return;

            ClipboardUtils.Copy($"{section.Header}{Environment.NewLine}{ClipboardUtils.GridToText(grid)}", button);
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