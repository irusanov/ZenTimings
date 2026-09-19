using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using ZenTimings.Export;
using ZenTimings.Settings;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace ZenTimings.Windows
{
    public class ExportSectionGroup
    {
        public string Header { get; set; }

        public ObservableCollection<ExportSectionEntry> Entries { get; } = new ObservableCollection<ExportSectionEntry>();
    }

    public class ExportSectionEntry : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private bool isChecked;

        public ExportSectionEntry(SnapshotSections section, string name, bool isChecked)
        {
            Section = section;
            Name = name;
            this.isChecked = isChecked;
        }

        public SnapshotSections Section { get; }

        public string Name { get; }

        // False when the running system has no data for this section
        public bool IsAvailable { get; set; } = true;

        public string Hint { get; set; }

        public bool IsChecked
        {
            get => isChecked;
            set
            {
                if (isChecked == value)
                    return;

                isChecked = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
            }
        }
    }

    /// <summary>
    /// Section selection for a one-off export (Copy / Save) or for the live snapshot file.
    /// </summary>
    public partial class ExportDialog : ThemedAdonisWindow
    {
        private readonly ExportSettings settings = ExportSettings.Instance;
        private readonly Func<SnapshotOptions, SnapshotFormat, string> generator;
        private readonly bool liveMode;
        private readonly IDictionary<SnapshotSections, string> unavailable;
        private string liveDirectory;

        public ObservableCollection<ExportSectionGroup> Groups { get; } = new ObservableCollection<ExportSectionGroup>();

        /// <param name="unavailable">Sections the system has no data for with the reason, see SnapshotBuilder.GetUnavailableSections.</param>
        public ExportDialog(Func<SnapshotOptions, SnapshotFormat, string> generator, SnapshotFormat format, bool liveMode,
            IDictionary<SnapshotSections, string> unavailable = null)
        {
            InitializeComponent();
            this.generator = generator;
            this.liveMode = liveMode;
            this.unavailable = unavailable ?? new Dictionary<SnapshotSections, string>();

            SnapshotSections selected = liveMode ? settings.LiveSnapshotSections : settings.ExportSections;

            AddGroup("Hardware", selected,
                Entry(SnapshotSections.System, "System (CPU, board, BIOS, SMU)"),
                Entry(SnapshotSections.Modules, "Memory modules"),
                Entry(SnapshotSections.Spd, "SPD, XMP/EXPO profiles, PMIC setup (DDR5 / LPDDR5)"));

            AddGroup("Memory configuration", selected,
                Entry(SnapshotSections.Timings, "Timings, all channels"),
                Entry(SnapshotSections.Registers, "Memory controller registers (low level, DDR5 / LPDDR5)"),
                Entry(SnapshotSections.Aod, "AOD table (DDR5 / LPDDR5)"),
                Entry(SnapshotSections.Apob, "APOB table (DDR5 / LPDDR5)"),
                Entry(SnapshotSections.BiosController, "BIOS memory controller (DDR4 / LPDDR4)"));

            AddGroup("Current readings", selected,
                Entry(SnapshotSections.PowerTable, "Power table (clocks, SoC voltages)"),
                Entry(SnapshotSections.DimmTelemetry, "DIMM voltages and temperatures (DDR5 / LPDDR5)"),
                Entry(SnapshotSections.Sensors, "Motherboard sensors"),
                Entry(SnapshotSections.AsusWmi, "ASUS WMI sensors"));

            if (liveMode)
            {
                Title = "Live snapshot file";
                format = settings.LiveSnapshotFormat;
                liveDirectory = settings.LiveSnapshotDirectory;
                TextLiveFileName.Text = LiveSnapshot.CleanFileName(settings.LiveSnapshotFileName);
                LivePanel.Visibility = Visibility.Visible;
                LiveSwitchPanel.Visibility = Visibility.Visible;
                Height = 680;
                CheckSerials.Visibility = Visibility.Collapsed;
                ButtonCopy.Visibility = Visibility.Collapsed;
                ButtonSave.Visibility = Visibility.Collapsed;
                ButtonSave.IsDefault = false;
                ButtonOk.Visibility = Visibility.Visible;
                ButtonOk.IsDefault = true;
                CheckLiveEnabled.IsChecked = settings.LiveSnapshotEnabled;
                UpdateLiveEnabledState();
                TextInterval.Text = Math.Max(1, settings.LiveSnapshotIntervalMs / 1000).ToString(CultureInfo.InvariantCulture);
            }

            CheckLegend.IsChecked = settings.IncludeLegend;
            RadioJson.IsChecked = format == SnapshotFormat.Json;
            RadioText.IsChecked = format == SnapshotFormat.Text;
            RadioHtml.IsChecked = format == SnapshotFormat.Html;

            DataContext = this;
        }

        private static KeyValuePair<SnapshotSections, string> Entry(SnapshotSections section, string name)
        {
            return new KeyValuePair<SnapshotSections, string>(section, name);
        }

        private void AddGroup(string header, SnapshotSections selected, params KeyValuePair<SnapshotSections, string>[] entries)
        {
            var group = new ExportSectionGroup { Header = header };
            foreach (var entry in entries)
            {
                bool available = !unavailable.TryGetValue(entry.Key, out string reason);
                group.Entries.Add(new ExportSectionEntry(entry.Key, entry.Value, available && (selected & entry.Key) == entry.Key)
                {
                    IsAvailable = available,
                    Hint = reason
                });
            }
            Groups.Add(group);
        }

        private SnapshotFormat SelectedFormat
        {
            get
            {
                if (RadioHtml.IsChecked == true)
                    return SnapshotFormat.Html;

                return RadioText.IsChecked == true ? SnapshotFormat.Text : SnapshotFormat.Json;
            }
        }

        private SnapshotSections SelectedSections
        {
            get
            {
                SnapshotSections result = SnapshotSections.None;
                foreach (var entry in Groups.SelectMany(g => g.Entries))
                {
                    if (entry.IsChecked)
                        result |= entry.Section;
                }

                return result;
            }
        }

        private void Select(SnapshotSections sections)
        {
            foreach (var entry in Groups.SelectMany(g => g.Entries))
                entry.IsChecked = entry.IsAvailable && (sections & entry.Section) == entry.Section;
        }

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e) => Select(SnapshotSections.All);

        private void BtnSelectNone_Click(object sender, RoutedEventArgs e) => Select(SnapshotSections.None);

        private void BtnSelectDefault_Click(object sender, RoutedEventArgs e) => Select(SnapshotSections.Default);

        private void CheckLiveEnabled_Changed(object sender, RoutedEventArgs e)
        {
            UpdateLiveEnabledState();
        }

        // The settings stay visible but inactive while the live snapshot is off
        private void UpdateLiveEnabledState()
        {
            bool enabled = CheckLiveEnabled.IsChecked == true;
            SectionsHeader.IsEnabled = enabled;
            SectionsViewer.IsEnabled = enabled;
            OptionsPanel.IsEnabled = enabled;
        }

        private void RadioFormat_Checked(object sender, RoutedEventArgs e)
        {
            UpdateLivePath();
        }

        private void UpdateLivePath()
        {
            if (TextLivePath == null)
                return;

            // The end of the path, with the file name, is what matters
            TextLivePath.Text = LiveSnapshot.GetFilePath(liveDirectory, TextLiveFileName.Text, SelectedFormat);
            TextLivePath.ToolTip = TextLivePath.Text;
            TextLivePath.CaretIndex = TextLivePath.Text.Length;
            TextLivePath.ScrollToHorizontalOffset(double.MaxValue);
        }

        private void TextLiveFileName_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdateLivePath();
        }

        private void BtnBrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "Folder for the live snapshot file";
                dialog.SelectedPath = string.IsNullOrWhiteSpace(liveDirectory) ? LiveSnapshot.DefaultDirectory : liveDirectory;

                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                    return;

                liveDirectory = dialog.SelectedPath;
                UpdateLivePath();
            }
        }

        private void BtnDefaultFolder_Click(object sender, RoutedEventArgs e)
        {
            liveDirectory = "";
            UpdateLivePath();
        }

        private string Generate()
        {
            SnapshotSections sections = SelectedSections;
            if (sections == SnapshotSections.None)
            {
                StatusText.Text = "Nothing is selected.";
                return null;
            }

            settings.ExportSections = sections;
            settings.IncludeLegend = CheckLegend.IsChecked == true;
            settings.Save();

            return generator(new SnapshotOptions
            {
                Sections = sections,
                IncludeSerialNumbers = CheckSerials.IsChecked == true,
                IncludeLegend = settings.IncludeLegend,
            }, SelectedFormat);
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string content = Generate();
                if (content == null)
                    return;

                Clipboard.SetText(content);
                StatusText.Text = $"Copied to clipboard ({content.Length} characters).";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Could not copy: {ex.Message}";
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string content = Generate();
                if (content == null)
                    return;

                string filter;
                string extension;

                var unixTimestamp = Convert.ToString(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMinutes, CultureInfo.InvariantCulture);
                string filename = $@"ZenTimings_snapshot_{unixTimestamp}";

                switch (SelectedFormat)
                {
                    case SnapshotFormat.Text:
                        filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
                        extension = "txt";
                        break;
                    case SnapshotFormat.Html:
                        filter = "HTML files (*.html)|*.html|All files (*.*)|*.*";
                        extension = "html";
                        break;
                    default:
                        filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";
                        extension = "json";
                        break;
                }

                var saveFileDialog = new SaveFileDialog
                {
                    Filter = filter,
                    DefaultExt = extension,
                    FileName = filename,
                    RestoreDirectory = true
                };

                if (saveFileDialog.ShowDialog() != true)
                    return;

                File.WriteAllText(saveFileDialog.FileName, content, new UTF8Encoding(false));
                StatusText.Text = $"Saved as {saveFileDialog.FileName}";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Could not save: {ex.Message}";
            }
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            SnapshotSections sections = SelectedSections;
            bool enabled = CheckLiveEnabled.IsChecked == true;

            if (enabled && sections == SnapshotSections.None)
            {
                StatusText.Text = "Nothing is selected.";
                return;
            }

            if (!int.TryParse(TextInterval.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seconds))
            {
                StatusText.Text = $"The interval must be between {TextInterval.Minimum} and {TextInterval.Maximum} seconds.";
                return;
            }

            string fileName = LiveSnapshot.CleanFileName(TextLiveFileName.Text);
            string typed = TextLiveFileName.Text.Trim();
            if (typed.Length > 0 && !typed.StartsWith(fileName, StringComparison.Ordinal))
            {
                StatusText.Text = $"This file name cannot be used, try \"{fileName}\".";
                return;
            }

            settings.IncludeLegend = CheckLegend.IsChecked == true;
            settings.LiveSnapshotFileName = fileName == LiveSnapshot.DefaultFileName ? "" : fileName;
            settings.LiveSnapshotEnabled = enabled;
            settings.LiveSnapshotFormat = SelectedFormat;
            settings.LiveSnapshotIntervalMs = seconds * 1000;
            settings.LiveSnapshotDirectory = liveDirectory ?? "";

            if (sections != SnapshotSections.None)
                settings.LiveSnapshotSections = sections;
            
            settings.Save();
            DialogResult = true;
            
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
