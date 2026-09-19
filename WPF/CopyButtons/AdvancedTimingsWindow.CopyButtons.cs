using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using ZenTimings.CopyButtons;

namespace ZenTimings.Windows
{
    public partial class AdvancedTimingsWindow
    {
        static AdvancedTimingsWindow()
        {
            EventManager.RegisterClassHandler(typeof(AdvancedTimingsWindow), LoadedEvent,
                new RoutedEventHandler((s, e) => ((AdvancedTimingsWindow)s).AttachCopyButton()));
        }

        // Next to the "Extended" check box
        private void AttachCopyButton()
        {
            if (!(ExtendedTimingsCheckBox.Parent is Panel panel) || panel.Children.OfType<object>().Any(CopyButton.IsCopyButton))
                return;

            Button button = CopyButton.CreateButton(CopyTimings_Click, "Copy the shown timings to clipboard");
            button.Margin = new Thickness(10, 0, 0, 0);
            panel.Children.Add(button);
        }

        private void CopyTimings_Click(object sender, RoutedEventArgs e)
        {
            var text = new StringBuilder();
            text.AppendLine(MemorySticksText.Text);
            text.AppendLine();

            if (BaseTimingsGrid.Visibility == Visibility.Visible)
                text.AppendLine(CopyButton.GridToText(BaseTimingsGrid));
            if (ExtendedTimingsGrid.Visibility == Visibility.Visible)
                text.Append(CopyButton.GridToText(ExtendedTimingsGrid));

            CopyButton.Copy(text.ToString(), sender as Button);
        }
    }
}
