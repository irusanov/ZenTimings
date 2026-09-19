using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using ZenTimings.CopyButtons;
using ZenTimings.ViewModels;

namespace ZenTimings.Windows
{
    public partial class SensorsWindow
    {
        static SensorsWindow()
        {
            EventManager.RegisterClassHandler(typeof(SensorsWindow), LoadedEvent,
                new RoutedEventHandler((s, e) => ((SensorsWindow)s).AttachCopyButtons()));
        }

        private void AttachCopyButtons()
        {
            // The module and sensor groups come from item templates and are filled in after the window is loaded
            CopyButton.AttachToGroupHeaders(this, CopyGroup_Click);
            CopyButton.Watch(ModulesContainer, CopyGroup_Click);
            CopyButton.Watch(SensorGroupsContainer, CopyGroup_Click);
        }

        private void CopyGroup_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button))
                return;

            var text = new StringBuilder();
            IEnumerable<TelemetryItemViewModel> items;

            if (button.DataContext is ModuleViewModel module)
            {
                text.AppendLine(module.Header);
                text.AppendLine($"{module.Manufacturer}\t{module.PartNumber}");
                text.AppendLine($"Capacity\t{module.Capacity}\tRank\t{module.Rank}");
                text.AppendLine($"DRAM\t{module.MemoryChip}");
                if (module.HasPmic)
                    text.AppendLine($"PMIC\t{module.PmicVendor} rev {module.PmicRevision}");
                items = module.TelemetryItems;
            }
            else if (button.DataContext is SensorGroupViewModel group)
            {
                text.AppendLine(group.Header);
                items = group.TelemetryItems;
            }
            else
            {
                return;
            }

            text.AppendLine("Sensor\tCurrent\tMin\tMax\tAverage");
            foreach (TelemetryItemViewModel item in items)
                text.AppendLine($"{item.Name}\t{item.Current}\t{item.Min}\t{item.Max}\t{item.Average}");

            CopyButton.Copy(text.ToString(), button);
        }
    }
}
