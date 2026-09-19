using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;

namespace ZenTimings.CopyButtons
{
    /// <summary>
    /// Optional add-on: small "copy to clipboard" buttons for the Tools windows.
    /// The windows are extended from partial class files in this folder, nothing outside of it refers to the add-on,
    /// so the project builds and works the same when the folder is removed.
    /// </summary>
    internal static class CopyButton
    {
        private const string ButtonStyleXaml =
            @"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'>
                <Setter Property='Background' Value='Transparent' />
                <Setter Property='Foreground' Value='{DynamicResource TextColor}' />
                <Setter Property='FontFamily' Value='Segoe MDL2 Assets' />
                <Setter Property='FontSize' Value='11' />
                <Setter Property='Content' Value='&#xE8C8;' />
                <Setter Property='Cursor' Value='Hand' />
                <Setter Property='Opacity' Value='0.6' />
                <Setter Property='Padding' Value='4,2' />
                <Setter Property='VerticalAlignment' Value='Center' />
                <Setter Property='Focusable' Value='False' />
                <Setter Property='Template'>
                    <Setter.Value>
                        <ControlTemplate TargetType='Button'>
                            <Border Background='{TemplateBinding Background}' Padding='{TemplateBinding Padding}'>
                                <ContentPresenter />
                            </Border>
                        </ControlTemplate>
                    </Setter.Value>
                </Setter>
                <Style.Triggers>
                    <Trigger Property='IsMouseOver' Value='True'>
                        <Setter Property='Opacity' Value='1' />
                    </Trigger>
                </Style.Triggers>
            </Style>";

        private static readonly object Marker = new object();
        private static Style buttonStyle;

        public static Button CreateButton(RoutedEventHandler click, string toolTip = "Copy to clipboard")
        {
            if (buttonStyle == null)
                buttonStyle = (Style)XamlReader.Parse(ButtonStyleXaml);

            var button = new Button { Style = buttonStyle, ToolTip = toolTip, Tag = Marker };
            button.Click += click;
            return button;
        }

        public static bool IsCopyButton(object element)
        {
            return element is Button button && ReferenceEquals(button.Tag, Marker);
        }

        /// <summary>
        /// Adds a button to the header of every group box below the root that does not have one yet.
        /// </summary>
        public static void AttachToGroupHeaders(DependencyObject root, RoutedEventHandler click)
        {
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                if (child is GroupBox box)
                    Attach(box, click);

                AttachToGroupHeaders(child, click);
            }
        }

        // The containers of a templated list are created after the window is loaded and again when its items change
        public static void Watch(ItemsControl list, RoutedEventHandler click)
        {
            list.ItemContainerGenerator.StatusChanged += (s, e) =>
            {
                if (list.ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
                    list.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => AttachToGroupHeaders(list, click)));
            };
        }

        private static void Attach(GroupBox box, RoutedEventHandler click)
        {
            if (box.Header is string title)
            {
                var header = new StackPanel { Orientation = Orientation.Horizontal };
                header.Children.Add(new TextBlock { Text = title, VerticalAlignment = VerticalAlignment.Center });

                Button button = CreateButton(click);
                button.Margin = new Thickness(6, 0, 0, 0);
                header.Children.Add(button);
                box.Header = header;
            }
            else if (box.Header is Grid grid && grid.ColumnDefinitions.Count > 1 && !grid.Children.OfType<object>().Any(IsCopyButton))
            {
                Button button = CreateButton(click);
                Grid.SetColumn(button, grid.ColumnDefinitions.Count - 1);
                grid.Children.Add(button);
            }
        }

        public static GroupBox GetGroupBox(Button button)
        {
            return (button.Parent as FrameworkElement)?.Parent as GroupBox;
        }

        public static string GetTitle(Button button)
        {
            return (button.Parent as Panel)?.Children.OfType<TextBlock>().FirstOrDefault()?.Text;
        }

        /// <summary>
        /// Returns the visible columns and rows of a grid as tab separated text.
        /// </summary>
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

        // Shows a check mark on the button for a moment
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

            if (button == null)
                return;

            button.Content = "";
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                button.ClearValue(ContentControl.ContentProperty);
            };
            timer.Start();
        }
    }
}
