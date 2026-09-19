using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using ZenTimings.Controls;

namespace ZenTimings.Utils
{
    internal static class ClipboardUtils
    {
        private const string CopyIcon = "IconCopy";
        private const string CopiedIcon = "IconCheck";

        /// <summary>Returns the visible columns and rows of a grid as tab separated text.</summary>
        public static string GridToText(DataGrid grid)
        {
            var sb = new StringBuilder();
            if (grid == null)
                return string.Empty;

            List<DataGridColumn> columns = grid.Columns
                .Where(c => c.Visibility == Visibility.Visible)
                .OrderBy(c => c.DisplayIndex)
                .ToList();

            if (columns.Any(c => c.Header != null))
                sb.AppendLine(string.Join("\t", columns.Select(c => $"{c.Header}")));

            foreach (object item in grid.Items)
            {
                if (item == CollectionView.NewItemPlaceholder)
                    continue;

                sb.AppendLine(string.Join("\t", columns.Select(c => $"{c.OnCopyingCellClipboardContent(item)}")));
            }

            return sb.ToString();
        }

        // The copy icon of the button turns into a check mark for a moment
        public static void Copy(string text, Button button)
        {
            try
            {
                Clipboard.SetDataObject(text ?? string.Empty, true);
            }
            catch
            {
                return;
            }

            if (!(button?.Content is AppIcon icon) || icon.Kind == CopiedIcon)
                return;

            icon.Kind = CopiedIcon;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                icon.Kind = CopyIcon;
            };
            timer.Start();
        }
    }
}
