using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace ZenTimings.Windows
{
    /// <summary>
    /// A group of sensors belonging to the same module/chip, shown as a section
    /// in the sensor settings dialog.
    /// </summary>
    public class SensorSettingsGroup
    {
        public string Header { get; set; }

        public ObservableCollection<SensorSettingsEntry> Entries { get; } = new ObservableCollection<SensorSettingsEntry>();
    }

    /// <summary>
    /// A single sensor entry with a checkbox controlling its visibility.
    /// </summary>
    public class SensorSettingsEntry : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private bool isVisible;

        public SensorSettingsEntry(string key, string name, bool isVisible)
        {
            Key = key;
            Name = name;
            this.isVisible = isVisible;
        }

        public string Key { get; }

        public string Name { get; }

        public bool IsVisible
        {
            get => isVisible;
            set
            {
                if (isVisible == value)
                    return;

                isVisible = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsVisible)));
            }
        }
    }

    public partial class SensorSettingsWindow : ThemedAdonisWindow
    {
        public ObservableCollection<SensorSettingsGroup> Groups { get; }

        public SensorSettingsWindow(List<SensorSettingsGroup> groups)
        {
            InitializeComponent();
            Groups = new ObservableCollection<SensorSettingsGroup>(groups);
            DataContext = this;
        }

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            SetAllVisibility(true);
        }

        private void BtnSelectNone_Click(object sender, RoutedEventArgs e)
        {
            SetAllVisibility(false);
        }

        private void SetAllVisibility(bool visible)
        {
            foreach (var group in Groups)
            {
                foreach (var entry in group.Entries)
                {
                    entry.IsVisible = visible;
                }
            }
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
