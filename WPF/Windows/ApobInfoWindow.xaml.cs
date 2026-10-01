using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using ZenStates.Core.Hardware.Apob;
using ZenStates.Core.OHWM;
using ZenTimings.Utils;

namespace ZenTimings.Windows
{
    /// <summary>
    /// The memory training data the ABL leaves in the APOB: the timings and ODT / drive strengths of each channel
    /// (and memory P-state), the DIMMs, the memory map, the core map, the event log and the entries of the table.
    /// </summary>
    public partial class ApobInfoWindow : ThemedWindow
    {
        private const string SerialRowName = "Serial number";

        private sealed class Row
        {
            public string Name { get; set; }
            public string[] Values { get; set; }
            public bool IsMismatch { get; set; }
            public bool IsTentative { get; set; }

            public string Value
            {
                get { return Values != null && Values.Length > 0 ? Values[0] : string.Empty; }
            }
        }

        private readonly Apob _apob;

        public ApobInfoWindow(Apob apob)
        {
            InitializeComponent();
            _apob = apob;
            Fill();
        }

        private void Fill()
        {
            if (_apob == null || !_apob.IsAvailable)
            {
                foreach (GroupBox section in AllSections())
                    section.Visibility = Visibility.Collapsed;

                StatusText.Text = _apob?.ErrorReason ?? "The APOB table is not available on this system.";
                return;
            }

            Run(OverviewSection, FillOverview);
            Run(TimingsSection, FillTimings);
            Run(OdtSection, FillOdt);
            Run(TrainingOdtSection, FillTrainingOdt);
            Run(MemorySettingsSection, FillMemorySettings);
            Run(DimmSection, FillDimms);
            Run(ProfileSection, FillProfile);
            Run(MemoryMapSection, FillMemoryMap);
            Run(CoreMapSection, FillCoreMap);
            Run(EventLogSection, FillEventLog);
            Run(EntriesSection, FillEntries);

            var status = new List<string> { _apob.ProfileName ?? "Unsupported CPU" };
            if (_apob.Entries != null && _apob.Entries.Count > 0)
                status.Add($"{_apob.Entries.Count} entries");
            if (_apob.ChannelTimings != null && _apob.ChannelTimings.Count > 0)
                status.Add($"{_apob.ChannelTimings.Count} timing blocks");
            if (!string.IsNullOrEmpty(_apob.ErrorReason))
                status.Add(_apob.ErrorReason);

            StatusText.Text = string.Join(" | ", status);
        }

        private IEnumerable<GroupBox> AllSections()
        {
            return new[]
            {
                OverviewSection, TimingsSection, OdtSection, TrainingOdtSection, MemorySettingsSection, DimmSection,
                ProfileSection, MemoryMapSection, CoreMapSection, EventLogSection, EntriesSection
            };
        }

        /// <summary>Fills a section, hides it when it has nothing to show or fails.</summary>
        private static void Run(GroupBox section, Func<bool> fill)
        {
            bool hasData;
            try
            {
                hasData = fill();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"APOB window, {section.Header}: {ex.Message}");
                hasData = false;
            }

            section.Visibility = hasData ? Visibility.Visible : Visibility.Collapsed;
        }

        #region Sections

        private bool FillOverview()
        {
            var rows = new List<Row>();

            rows.Add(Pair("Address", _apob.Address == 0xFFFFFFFF ? "n/a (loaded from a file)" : $"0x{_apob.Address:X8}"));
            rows.Add(Pair("Version", $"{_apob.Header.Version}"));
            rows.Add(Pair("Table size", $"{_apob.Header.TableSize} bytes"));
            rows.Add(Pair("Layout profile", _apob.ProfileName ?? "Unsupported"));

            if (_apob.Entries != null && _apob.Entries.Count > 0)
            {
                int encrypted = _apob.Entries.Count(e => e.IsEncrypted);
                rows.Add(Pair("Entries", $"{_apob.Entries.Count} ({encrypted} encrypted)"));
            }

            if (_apob.ActiveMemClk > 0)
                rows.Add(Pair("Active MemClk", $"{_apob.ActiveMemClk} MHz"));

            ApobBootInfo boot = _apob.BootInfo;
            if (boot != null)
            {
                DateTime? date = boot.LastTrainingDate;
                rows.Add(Pair("Last memory training", date.HasValue ? date.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "Unknown"));
                rows.Add(Pair("Active APCB instance", $"0x{boot.ApcbActiveInstance:X}"));
            }

            ApobDmiInfo dmi = _apob.DmiInfo;
            if (dmi != null)
                rows.Add(Pair("Memory type", dmi.MemoryTypeName + (dmi.EccCapable ? ", ECC capable" : "")));

            if (_apob.MemoryMap != null)
                rows.Add(Pair("Top of memory", $"{_apob.MemoryMap.TopOfSystemMemory >> 20} MB (0x{_apob.MemoryMap.TopOfSystemMemory:X})"));

            if (_apob.CcdlData.Tccdl != 0)
                rows.Add(Pair("tCCD_L / WR / WR2", $"{_apob.CcdlData.Tccdl} / {_apob.CcdlData.Tccdlwr} / {_apob.CcdlData.Tccdlwr2}"));

            rows.Add(Pair("PMU training failure", NonZeroText(_apob.TrainingFailureDataBytes)));
            rows.Add(Pair("MBIST result", NonZeroText(_apob.MbistResultDataBytes)));

            if (_apob.EventLog != null)
                rows.Add(Pair("Event log", _apob.EventLog.Count == 0 ? "No events" : $"{_apob.EventLog.Count} event(s)"));

            OverviewGrid.ItemsSource = rows;
            return true;
        }

        private bool FillTimings()
        {
            List<ApobChannelTimings> blocks = _apob.ChannelTimings;
            if (blocks == null || blocks.Count == 0)
                return false;

            var rows = new List<Row>
            {
                BlockRow("Data rate", blocks, b => $"{b.DataRate}"),
                BlockRow("MemClk", blocks, b => $"{b.MemClk}")
            };

            if (_apob.ActiveMemClk > 0)
                rows.Add(BlockRow("Active", blocks, b => b.IsActive ? "Yes" : "-"));

            // The fields are the same in every block of a layout
            List<ApobTimingValue> fields = blocks[0].Values;
            for (int f = 0; f < fields.Count; f++)
            {
                int index = f;
                ApobTimingField field = fields[f].Field;
                Row row = BlockRow(field.Tentative ? field.Name + " (?)" : field.Name, blocks,
                    b => index < b.Values.Count ? $"{b.Values[index].Value}" : "");
                row.IsTentative = field.Tentative;
                rows.Add(row);
            }

            SetupGrid(TimingsGrid, "Timing", BlockHeaders(blocks), rows, 110);
            return true;
        }

        private bool FillOdt()
        {
            // DDR4: the impedances are in the memory general configuration info, per channel
            if (FillDdr4Odt())
                return true;

            List<ApobData> channels = _apob.ChannelData;
            if (channels == null || channels.Count == 0)
            {
                // No per channel records known for this CPU, show the main block alone
                if (_apob.Data == null)
                    return false;

                channels = new List<ApobData> { _apob.Data };
            }

            string[] headers = channels.Select((c, i) => $"Ch {ChannelLetter(i)}").ToArray();
            List<Row> rows = DataRows(channels.Cast<object>().ToList(), null);
            if (rows.Count == 0)
                return false;

            SetupGrid(OdtGrid, "Setting", headers, rows, 150);
            return true;
        }

        private bool FillDdr4Odt()
        {
            ApobMemGeneralConfig config = _apob.MemGeneralConfig;
            List<ApobDdr4ChannelConfig> channels = config?.Channels.Where(c => c.IsPopulated).ToList();
            if (channels == null || channels.Count == 0)
                return false;

            var rows = new List<Row>
            {
                ChannelRow("ProcOdt", channels, c => c.ProcOdt),
                ChannelRow("RttNom", channels, c => c.RttNom),
                ChannelRow("RttWr", channels, c => c.RttWr),
                ChannelRow("RttPark", channels, c => c.RttPark),
                ChannelRow("AddrCmdSetup", channels, c => c.AddrCmdSetup),
                ChannelRow("CsOdtSetup", channels, c => c.CsOdtSetup),
                ChannelRow("CkeSetup", channels, c => c.CkeSetup),
                ChannelRow("ClkDrvStren", channels, c => c.ClkDrvStren),
                ChannelRow("AddrCmdDrvStren", channels, c => c.AddrCmdDrvStren),
                ChannelRow("CsOdtCmdDrvStren", channels, c => c.CsOdtCmdDrvStren),
                ChannelRow("CkeDrvStren", channels, c => c.CkeDrvStren)
            };

            SetupGrid(OdtGrid, "Setting", channels.Select(c => $"Ch {ChannelLetter(c.Channel)}").ToArray(), rows, 150);
            return true;
        }

        private static Row ChannelRow(string name, IList<ApobDdr4ChannelConfig> channels, Func<ApobDdr4ChannelConfig, ZenStates.Core.Common.EncodedValueBase> value)
        {
            string[] values = channels.Select(c => value(c)?.ToString() ?? "N/A").ToArray();
            return new Row { Name = name, Values = values, IsMismatch = HasMismatch(values, null) };
        }

        private bool FillMemorySettings()
        {
            ApobMemGeneralConfig config = _apob.MemGeneralConfig;
            if (config == null)
                return false;

            var rows = new List<Row>
            {
                Pair("MemClkFreq", $"{config.MemClkFreq} MHz"),
                Pair("DdrMaxRate", $"{config.DdrMaxRate}"),
                Pair("ECC enabled", config.EccEnable.Any(e => e) ? "Yes" : "No"),
                Pair("Channel interleave", config.ChannelInterleave ? "Yes" : "No"),
                Pair("Interleave mode", $"0x{config.InterleaveCurrentMode:X} (capability 0x{config.InterleaveCapability:X}, size 0x{config.InterleaveSize:X})")
            };

            // Unused chip select interleave entries of absent channels have a status code of 0
            foreach (ApobMemSetting setting in config.Settings.Where(s => s.StatusCode != 0))
                rows.Add(Pair(setting.Name, $"{setting.Value} (status 0x{setting.StatusCode:X4})"));

            MemorySettingsGrid.ItemsSource = rows;
            return true;
        }

        private bool FillTrainingOdt()
        {
            List<ApobChannelTimings> blocks = _apob.ChannelTimings?.Where(b => b.ExtendedData != null).ToList();
            if (blocks == null || blocks.Count == 0)
            {
                // Older search result: the extended block of the first channel
                if (_apob.ExtendedData == null)
                    return false;

                List<Row> single = DataRows(new List<object> { _apob.ExtendedData }, null);
                SetupGrid(TrainingOdtGrid, "Setting", new[] { "Ch A" }, single, 150);
                return single.Count > 0;
            }

            List<Row> rows = DataRows(blocks.Select(b => (object)b.ExtendedData).ToList(), blocks);
            if (rows.Count == 0)
                return false;

            SetupGrid(TrainingOdtGrid, "Setting", BlockHeaders(blocks), rows, 150);
            return true;
        }

        private bool FillDimms()
        {
            ApobBootInfo boot = _apob.BootInfo;
            ApobDmiInfo dmi = _apob.DmiInfo;
            if (boot == null && dmi == null)
                return false;

            var slots = new List<int>();
            var names = new List<string>();
            int slotCount = Math.Max(boot?.Dimms.Count ?? 0, 16);

            for (int s = 0; s < slotCount; s++)
            {
                ApobBootDimm b = BootDimm(boot, s);
                ApobDmiPhysicalDimm p = PhysicalDimm(dmi, s);
                if (b == null && p == null)
                    continue;

                slots.Add(s);
                names.Add(p != null ? p.SlotName : b.SlotName);
            }

            if (slots.Count == 0)
                return false;

            var rows = new List<Row>
            {
                SlotRow("Module manufacturer", slots, s => BootDimm(boot, s)?.ModuleManufacturer),
                SlotRow("DRAM manufacturer", slots, s => BootDimm(boot, s)?.DramManufacturer),
                SlotRow(SerialRowName, slots, s =>
                {
                    ApobBootDimm b = BootDimm(boot, s);
                    return b != null ? b.SerialNumber.ToString("X8", CultureInfo.InvariantCulture) : null;
                }),
                SlotRow("Size", slots, s =>
                {
                    ApobDmiLogicalDimm l = LogicalDimm(dmi, s);
                    return l != null && l.SizeMB > 0 ? FormatMegabytes(l.SizeMB) : null;
                }),
                SlotRow("Configured speed", slots, s =>
                {
                    ApobDmiPhysicalDimm p = PhysicalDimm(dmi, s);
                    return p != null ? $"{p.ConfiguredSpeed} MHz" : null;
                }),
                SlotRow("Configured voltage", slots, s =>
                {
                    ApobDmiPhysicalDimm p = PhysicalDimm(dmi, s);
                    return p != null ? $"{p.ConfiguredVoltage / 1000.0:0.000} V" : null;
                }),
                SlotRow("Address range", slots, s =>
                {
                    ApobDmiLogicalDimm l = LogicalDimm(dmi, s);
                    return l != null ? $"{FormatMegabytes((ulong)l.StartingAddressKb >> 10)} - {FormatMegabytes(((ulong)l.EndingAddressKb + 1) >> 10)}" : null;
                }),
                SlotRow("Interleaved", slots, s =>
                {
                    ApobDmiLogicalDimm l = LogicalDimm(dmi, s);
                    return l != null ? (l.Interleaved ? "Yes" : "No") : null;
                }),
                SlotRow("SMBIOS handle", slots, s =>
                {
                    ApobDmiPhysicalDimm p = PhysicalDimm(dmi, s);
                    return p != null ? $"0x{p.Handle:X4}" : null;
                })
            };

            rows.RemoveAll(r => r.Values.All(v => v == "-"));
            SetupGrid(DimmGrid, "Slot", names, rows, 150);
            return true;
        }

        private bool FillProfile()
        {
            ApobMemoryProfileInfo profile = _apob.MemoryProfileInfo;
            if (profile == null || (profile.MemClk == 0 && !profile.HasExpoBlock))
                return false;

            var rows = new List<Row>
            {
                Pair("MemClk", profile.MemClk > 0 ? $"{profile.MemClk} MHz (DDR5-{profile.MemClk * 2})" : "-"),
                Pair("VDD", profile.VddMv > 0 ? $"{profile.VddMv / 1000.0:0.000} V" : "-"),
                Pair("CAS", profile.Cas > 0 ? $"{profile.Cas}" : "-")
            };

            if (profile.HasExpoBlock)
            {
                rows.Add(Pair("EXPO revision", $"{profile.ExpoRevision >> 4}.{profile.ExpoRevision & 0xF}"));
                rows.Add(Pair("EXPO profiles", $"0x{profile.ExpoProfileBits:X2}"));
                rows.Add(Pair("Profile 1 tCK", $"{profile.ExpoProfile1TckPs} ps"));
                rows.Add(Pair("Profile 1 tAA", $"{profile.ExpoProfile1TaaPs} ps"));
            }

            ProfileGrid.ItemsSource = rows;
            return true;
        }

        private bool FillMemoryMap()
        {
            ApobMemoryMap map = _apob.MemoryMap;
            if (map == null || map.Holes.Count == 0)
                return false;

            var rows = map.Holes.Select(h => new Row
            {
                Name = $"0x{h.Base:X12}",
                Values = new[] { $"0x{h.End:X12}", FormatMegabytes(h.Size >> 20, h.Size), h.TypeName }
            }).ToList();

            SetupGrid(MemoryMapGrid, "Base", new[] { "End", "Size", "Type" }, rows, 120);
            return true;
        }

        private bool FillCoreMap()
        {
            if (_apob.CoreMaps == null || _apob.CoreMaps.Count == 0)
                return false;

            bool multiple = _apob.CoreMaps.Count > 1;
            var rows = new List<Row>();
            foreach (ApobCoreMap map in _apob.CoreMaps)
            {
                foreach (ApobCoreMapCore core in map.Cores)
                {
                    rows.Add(new Row
                    {
                        Name = multiple ? $"Socket {map.InstanceId} Core {core.LogicalIndex}" : $"Core {core.LogicalIndex}",
                        Values = new[]
                        {
                            $"CCD {core.LogicalCcd}  CCX {core.LogicalCcx}  Core {core.LogicalCore}",
                            $"CCD {core.PhysicalCcd}  CCX {core.PhysicalCcx}  Core {core.PhysicalCore}",
                            $"{core.EnabledThreads}"
                        }
                    });
                }
            }

            SetupGrid(CoreMapGrid, "Logical core", new[] { "Logical", "Physical", "Threads" }, rows, 110);
            return rows.Count > 0;
        }

        private bool FillEventLog()
        {
            ApobEventLog log = _apob.EventLog;
            if (log == null || log.Events.Count == 0)
                return false;

            var rows = log.Events.Select((e, i) => new Row
            {
                Name = $"{i}",
                Values = new[] { e.EventClassName, $"0x{e.EventInfo:X8}", $"0x{e.DataA:X8}", $"0x{e.DataB:X8}" }
            }).ToList();

            SetupGrid(EventLogGrid, "#", new[] { "Class", "Event", "Data A", "Data B" }, rows, 40);
            return true;
        }

        private bool FillEntries()
        {
            if (_apob.Entries == null || _apob.Entries.Count == 0)
                return false;

            var rows = _apob.Entries.Select((e, i) => new Row
            {
                Name = $"{i}",
                Values = new[]
                {
                    $"0x{e.Offset:X5}",
                    $"{e.GroupId}/{e.DataTypeId}",
                    $"{e.InstanceId}",
                    $"{e.Size}",
                    e.GroupName,
                    e.IsEncrypted ? e.Name + " (encrypted)" : e.Name
                }
            }).ToList();

            SetupGrid(EntriesGrid, "#", new[] { "Offset", "Type", "Instance", "Size", "Group", "Name" }, rows, 40);
            return true;
        }

        #endregion

        #region Helpers

        private static Row Pair(string name, string value)
        {
            return new Row { Name = name, Values = new[] { value ?? "-" } };
        }

        private static string NonZeroText(int? nonZeroBytes)
        {
            if (!nonZeroBytes.HasValue)
                return "Not available";

            return nonZeroBytes.Value == 0 ? "No data" : $"{nonZeroBytes.Value} non-zero bytes";
        }

        private static char ChannelLetter(int channel)
        {
            return (char)('A' + channel);
        }

        /// <summary>"Ch A", or "Ch A P0" when the CPU has memory P-states.</summary>
        private static string[] BlockHeaders(IList<ApobChannelTimings> blocks)
        {
            bool pStates = blocks.Any(b => b.PState > 0);
            return blocks.Select(b => pStates ? $"Ch {ChannelLetter(b.Channel)} P{b.PState}" : $"Ch {ChannelLetter(b.Channel)}").ToArray();
        }

        /// <summary>A row over the timing blocks, a mismatch when channels of the same P-state differ.</summary>
        private static Row BlockRow(string name, IList<ApobChannelTimings> blocks, Func<ApobChannelTimings, string> value)
        {
            string[] values = blocks.Select(value).ToArray();
            return new Row { Name = name, Values = values, IsMismatch = HasMismatch(values, blocks) };
        }

        private static bool HasMismatch(string[] values, IList<ApobChannelTimings> blocks)
        {
            for (int i = 0; i < values.Length; i++)
            {
                for (int j = i + 1; j < values.Length; j++)
                {
                    bool samePState = blocks == null || blocks[i].PState == blocks[j].PState;
                    if (samePState && !string.Equals(values[i], values[j], StringComparison.Ordinal))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// One row per public property of the ODT / drive strength records, without the ones none of the records
        /// has (properties the layout does not place).
        /// </summary>
        private static List<Row> DataRows(IList<object> records, IList<ApobChannelTimings> blocks)
        {
            var rows = new List<Row>();
            if (records.Count == 0)
                return rows;

            PropertyInfo[] properties = records[0].GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (PropertyInfo property in properties)
            {
                if (property.GetIndexParameters().Length > 0)
                    continue;

                object[] raw = records.Select(r => r != null ? property.GetValue(r, null) : null).ToArray();
                if (raw.All(v => v == null))
                    continue;

                string[] values = raw.Select(v => v != null ? v.ToString() : "-").ToArray();
                rows.Add(new Row { Name = property.Name, Values = values, IsMismatch = HasMismatch(values, blocks) });
            }

            return rows;
        }

        private static ApobBootDimm BootDimm(ApobBootInfo boot, int slot)
        {
            return boot?.Dimms.FirstOrDefault(d => d.Present && d.Slot == slot);
        }

        // DMI records have a channel and a DIMM, the boot info a slot index: channel * 2 + DIMM
        private static ApobDmiPhysicalDimm PhysicalDimm(ApobDmiInfo dmi, int slot)
        {
            return dmi?.PhysicalDimms.FirstOrDefault(d => d.Present && d.Channel * 2 + d.Dimm == slot);
        }

        private static ApobDmiLogicalDimm LogicalDimm(ApobDmiInfo dmi, int slot)
        {
            return dmi?.LogicalDimms.FirstOrDefault(d => d.Present && d.Channel * 2 + d.Dimm == slot);
        }

        private static Row SlotRow(string name, IList<int> slots, Func<int, string> value)
        {
            return new Row { Name = name, Values = slots.Select(s => value(s) ?? "-").ToArray() };
        }

        private static string FormatMegabytes(ulong megabytes, ulong bytes = 0)
        {
            if (megabytes == 0 && bytes > 0)
                return $"{bytes >> 10} KB";

            return megabytes >= 1024 && megabytes % 1024 == 0 ? $"{megabytes >> 10} GB" : $"{megabytes} MB";
        }

        /// <summary>A name column and a value column per header, bound to <see cref="Row"/>.</summary>
        private void SetupGrid(DataGrid grid, string nameHeader, IList<string> headers, List<Row> rows, double nameWidth)
        {
            var nameStyle = (Style)FindResource("ApobNameTextStyle");
            var valueStyle = (Style)FindResource("ApobValueTextStyle");
            var headerStyle = (Style)FindResource("SectionGridColumnHeaderStyle");

            grid.Columns.Clear();
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = nameHeader,
                Binding = new Binding("Name"),
                ElementStyle = nameStyle,
                HeaderStyle = headerStyle,
                Width = nameWidth
            });

            for (int i = 0; i < headers.Count; i++)
            {
                grid.Columns.Add(new DataGridTextColumn
                {
                    Header = headers[i],
                    Binding = new Binding($"Values[{i}]"),
                    ElementStyle = valueStyle,
                    HeaderStyle = headerStyle,
                    MinWidth = 64
                });
            }

            grid.ItemsSource = rows;
        }

        #endregion

        private void CopySection_Click(object sender, RoutedEventArgs e)
        {
            // The button comes from the header template, the group box that uses it names its grid in Tag
            if (!(sender is Button button))
                return;

            DependencyObject parent = button;
            while (parent != null && !(parent is GroupBox))
                parent = VisualTreeHelper.GetParent(parent);

            if (!(parent is GroupBox section) || !(section.Tag is DataGrid grid))
                return;

            string text = ClipboardUtils.GridToText(grid);

            // The serial numbers identify the exact modules, keep them out of text that is meant to be pasted elsewhere
            if (ReferenceEquals(grid, DimmGrid))
            {
                text = string.Join(Environment.NewLine, text
                    .Split(new[] { Environment.NewLine }, StringSplitOptions.None)
                    .Select(l => l.StartsWith(SerialRowName + "\t", StringComparison.Ordinal) ? SerialRowName + "\t(hidden)" : l));
            }

            ClipboardUtils.Copy($"{section.Header}{Environment.NewLine}{text}", button);
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            InteropMethods.EmptyWorkingSet(System.Diagnostics.Process.GetCurrentProcess().Handle);
        }
    }
}
