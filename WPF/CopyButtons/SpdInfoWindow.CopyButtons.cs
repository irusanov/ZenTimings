using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ZenTimings.CopyButtons;

namespace ZenTimings.Windows
{
    public partial class SpdInfoWindow
    {
        static SpdInfoWindow()
        {
            EventManager.RegisterClassHandler(typeof(SpdInfoWindow), LoadedEvent,
                new RoutedEventHandler((s, e) => ((SpdInfoWindow)s).AttachCopyButton()));
        }

        // In the free toolbar column to the left of "Dump SPD"
        private void AttachCopyButton()
        {
            if (!(ButtonDumpSpd.Parent is Grid toolbar) || toolbar.Children.OfType<object>().Any(CopyButton.IsCopyButton))
                return;

            Button button = CopyButton.CreateButton(CopyTab_Click, "Copy the selected tab to clipboard");
            button.HorizontalAlignment = HorizontalAlignment.Right;
            button.Margin = new Thickness(0, 0, 10, 0);
            Grid.SetColumn(button, Math.Max(0, Grid.GetColumn(ButtonDumpSpd) - 1));
            toolbar.Children.Add(button);
        }

        private void CopyTab_Click(object sender, RoutedEventArgs e)
        {
            var tab = ProfilesTabControl.SelectedItem as TabItem;
            if (!(tab?.Content is DataGrid grid))
                return;

            // The serial number identifies the exact module, keep it out of text that is meant to be pasted elsewhere
            var lines = CopyButton.GridToText(grid)
                .Split(new[] { Environment.NewLine }, StringSplitOptions.None)
                .Select(l => l.StartsWith("ModuleSerialNumber\t") ? "ModuleSerialNumber\t(hidden)" : l);

            string title = $"SPD {(ComboSlots.SelectedItem as SlotItem)?.Display} - {tab.Header}";
            CopyButton.Copy($"{title}{Environment.NewLine}{string.Join(Environment.NewLine, lines)}", sender as Button);
        }
    }
}
