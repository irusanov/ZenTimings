using System;
using System.Windows;
using System.Windows.Controls;
using ZenTimings.CopyButtons;

namespace ZenTimings.Windows
{
    public partial class SystemInfoWindow
    {
        static SystemInfoWindow()
        {
            EventManager.RegisterClassHandler(typeof(SystemInfoWindow), LoadedEvent,
                new RoutedEventHandler((s, e) => ((SystemInfoWindow)s).AttachCopyButtons()));
        }

        private void AttachCopyButtons()
        {
            CopyButton.AttachToGroupHeaders(this, CopySection_Click);
        }

        private void CopySection_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(CopyButton.GetGroupBox(button)?.Content is DataGrid grid))
                return;

            string title = CopyButton.GetTitle(button);
            CopyButton.Copy($"{title}{Environment.NewLine}{CopyButton.GridToText(grid)}", button);
        }
    }
}
