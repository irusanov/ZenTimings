using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZenStates.Core.Hardware.Apob;
using ZenStates.Core.OHWM;
using ZenTimings.Utils;
using ZenTimings.Localization;

namespace ZenTimings.Windows
{
    /// <summary>
    /// The BIOS option tokens of the APCB in the SPI flash: the options of the APCB the firmware booted with, named
    /// from the CBS / AOD setup code (<see cref="ApcbTokenNames"/>), next to the defaults of the backup APCB.
    /// </summary>
    public partial class ApcbInfoWindow : ThemedWindow
    {
        private const uint TYPE_WRITABLE = 0x60;
        private const uint TYPE_BACKUP = 0x68;

        private sealed class Row
        {
            public string Option { get; set; }
            public string Value { get; set; }
            public string Default { get; set; }
            public string Name { get; set; }
            public string Token { get; set; }
            public string Tooltip { get; set; }
            public string Group { get; set; }
            public string Apob { get; set; }
            public bool IsUnknown { get; set; }
            public bool IsChanged { get; set; }
        }

        private readonly Apob _apob;

        public ApcbInfoWindow(Apob apob)
        {
            InitializeComponent();
            _apob = apob;

            OptionsSection.Visibility = Visibility.Collapsed;
            DefaultsSection.Visibility = Visibility.Collapsed;
            ImagesSection.Visibility = Visibility.Collapsed;
            OverviewSection.Visibility = Visibility.Collapsed;
            TrainingSection.Visibility = Visibility.Collapsed;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (_apob == null)
            {
                StatusText.Text = UiText.Get("ApcbNeedsApob");
                return;
            }

            List<ApcbBlob> blobs;
            uint? liveInstance;
            try
            {
                // Reading the flash takes a moment; keep the window responsive
                blobs = await Task.Run(() => _apob.ReadApcb());
                liveInstance = _apob.LiveApcbInstance;
            }
            catch (Exception ex)
            {
                StatusText.Text = UiText.Get("ApcbReadFailed") + ex.Message;
                return;
            }

            if (blobs == null)
            {
                // The trained values still come from the APOB
                FillTraining(null, null);
                StatusText.Text = UiText.Get("ApcbLiveSystemOnly");
                return;
            }

            try
            {
                Fill(blobs, liveInstance);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"APCB window: {ex}");
                StatusText.Text = UiText.Get("ApcbDisplayFailed") + ex.Message;
            }
        }

        private void Fill(List<ApcbBlob> blobs, uint? liveInstance)
        {
            if (blobs.Count == 0)
            {
                StatusText.Text = UiText.Get("ApcbNotFound");
                return;
            }

            // The booted APCB is the one whose unique instance the APOB records; without it, every writable APCB is listed
            List<ApcbBlob> booted = liveInstance.HasValue
                ? blobs.Where(b => b.UniqueInstance == liveInstance.Value).ToList()
                : new List<ApcbBlob>();
            bool haveBooted = booted.Count > 0;
            if (!haveBooted)
                booted = blobs.Where(b => b.EntryType == TYPE_WRITABLE).ToList();

            ApcbBlob backup = blobs.FirstOrDefault(b => b.EntryType == TYPE_BACKUP && b.Tokens.Count > 0);
            Dictionary<uint, ApcbToken> defaults = TokenMap(backup);

            // Options of the booted APCB, one row per token (the first listing wins, as in the firmware lookup)
            var options = new List<Row>();
            var seen = new HashSet<uint>();
            foreach (ApcbBlob blob in booted)
            {
                foreach (ApcbToken t in blob.Tokens)
                {
                    if (!seen.Add(t.Id))
                        continue;

                    ApcbToken def;
                    defaults.TryGetValue(t.Id, out def);
                    Row row = MakeRow(t);
                    row.Default = def != null ? FormatValue(def, false) : UiText.Get("Automatic");
                    row.IsChanged = def == null || Mask(def) != Mask(t);
                    options.Add(row);
                }
            }
            SortRows(options);

            var defaultRows = new List<Row>();
            if (backup != null)
            {
                foreach (ApcbToken t in defaults.Values)
                    defaultRows.Add(MakeRow(t));
                SortRows(defaultRows);
            }

            var images = new List<Row>();
            foreach (ApcbBlob blob in blobs)
            {
                bool active = haveBooted && blob.UniqueInstance == liveInstance.Value;
                string status = blob.Error != null ? "error: " + blob.Error
                    : $"{blob.Tokens.Count} token(s), checksum {(blob.ChecksumValid ? "OK" : "BAD")}{(active ? ", booted" : "")}";
                images.Add(new Row
                {
                    Option = $"0x{blob.EntryType:X2}" + (blob.EntryType == TYPE_BACKUP ? " backup" : blob.EntryType == TYPE_WRITABLE ? " writable" : ""),
                    Value = $"0x{blob.Address:X}",
                    Default = $"0x{blob.Size:X}",
                    Name = $"0x{blob.UniqueInstance:X}",
                    Token = status,
                    IsChanged = active
                });
            }

            int named = options.Count(r => !r.IsUnknown);
            var overview = new List<Row>
            {
                Pair(UiText.Get("BootedApcbInstance"), liveInstance.HasValue
                    ? $"0x{liveInstance.Value:X}" + (haveBooted ? UiText.Get("BootedApcbNotFound") : " (not found in the flash, listing all writable APCBs)")
                    : UiText.Get("BootedApcbUnknown")),
                Pair(UiText.Get("ApcbSetOptions"), UiText.Format("ApcbNamedOptionsCount", options.Count, named)),
                Pair(UiText.Get("ChangedFromDefault"), $"{options.Count(r => r.IsChanged)}"),
                Pair(UiText.Get("DefaultTokens"), backup != null ? $"{defaultRows.Count}" : UiText.Get("NoBackupApcb")),
                Pair(UiText.Get("Names"), UiText.Format("KnownTokens", ApcbTokenNames.Count))
            };

            FillTraining(TokenMap(booted), defaults);

            OverviewGrid.ItemsSource = overview;
            OptionsGrid.ItemsSource = options;
            DefaultsGrid.ItemsSource = defaultRows;
            ImagesGrid.ItemsSource = images;

            OverviewSection.Visibility = Visibility.Visible;
            OptionsSection.Visibility = options.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            DefaultsSection.Visibility = defaultRows.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            ImagesSection.Visibility = Visibility.Visible;

            StatusText.Text = options.Count > 0
                ? UiText.Get("ApcbOptionsHint")
                : UiText.Get("NoApcbOptions");
        }

        private static Dictionary<uint, ApcbToken> TokenMap(ApcbBlob blob)
        {
            return TokenMap(blob != null ? new List<ApcbBlob> { blob } : null);
        }

        // First listing wins, as in the firmware lookup
        private static Dictionary<uint, ApcbToken> TokenMap(IEnumerable<ApcbBlob> blobs)
        {
            var map = new Dictionary<uint, ApcbToken>();
            if (blobs == null)
                return map;

            foreach (ApcbBlob blob in blobs)
            {
                foreach (ApcbToken t in blob.Tokens)
                {
                    if (!map.ContainsKey(t.Id))
                        map[t.Id] = t;
                }
            }

            return map;
        }

        #region DFE / 2D training

        private static string NotInApob => UiText.Get("NotInApob");

        // The DDR training options of the CBS DDR menu, by group
        private static readonly KeyValuePair<string, uint>[] TrainingTokens =
        {
            new KeyValuePair<string, uint>("RX DFE", 0xF6E0D48D),   // CbsCmnMemNumberOfRxDfeTaps
            new KeyValuePair<string, uint>("TX DFE", 0x664706F1),   // CbsCmnMemNumOfTxDfeTaps
            new KeyValuePair<string, uint>("TX DFE", 0x3F801A16),   // AodTx2DDumpTrainedDfeTapBiasValues
            new KeyValuePair<string, uint>("RX2D", 0x3B208051),     // CbsCmnMemCtrllerRx2DTrainOptDdr
            new KeyValuePair<string, uint>("RX2D", 0xC68DD7A9),     // CbsCmnMemCtrllerRx2DDfeDdr
            new KeyValuePair<string, uint>("RX2D", 0x1A3E550A),     // CbsCmnMemCtrllerRx2DVoltStepSizeDdr
            new KeyValuePair<string, uint>("RX2D", 0x32458581),     // CbsCmnMemCtrllerRx2DDelayStepSizeDdr
            new KeyValuePair<string, uint>("TX2D", 0x3866D592),     // CbsCmnMemCtrllerTx2DTrainOptDdr
            new KeyValuePair<string, uint>("TX2D", 0x740F1006),     // CbsCmnMemCtrllerTx2DDfeDdr
            new KeyValuePair<string, uint>("TX2D", 0x2DB96E84),     // CbsCmnMemCtrllerTx2DVoltStepSizeDdr
            new KeyValuePair<string, uint>("TX2D", 0xCC3BA555),     // CbsCmnMemCtrllerTx2DVoltStepSizeMultDdr
            new KeyValuePair<string, uint>("TX2D", 0xE5D0EC4B),     // CbsCmnMemCtrllerTx2DDelayStepSizeDdr
            new KeyValuePair<string, uint>("TX2D", 0x58B905C1),     // CbsCmnMemCtrllerTx2DDelayStepSizeMultDdr
            new KeyValuePair<string, uint>("TX2D", 0x1545B8F9),     // AodMemCtrllerTx2DVoltStepSizeDdr
        };

        /// <summary>
        /// The RX/TX DFE and RX2D/TX2D options: the BIOS setting from the booted APCB (Auto when the option isn't
        /// stored) next to what the APOB shows of the training. TX DFE taps come from the DRAM's MR111 (PMU
        /// message block); for RX only "1 tap or less" vs "2 or more" is known, from the trained RX DFE Tap-2 of the
        /// PHY replay. The 2D options are inputs to training and have no trace in the APOB.
        /// </summary>
        private void FillTraining(Dictionary<uint, ApcbToken> booted, Dictionary<uint, ApcbToken> defaults)
        {
            try
            {
                string rxApob = RxDfeFromApob();
                string txApob = TxDfeFromApob();
                bool haveTokens = booted != null;

                var rows = new List<Row>();
                foreach (KeyValuePair<string, uint> entry in TrainingTokens)
                {
                    uint id = entry.Value;
                    ApcbToken token = null;
                    ApcbToken def = null;
                    booted?.TryGetValue(id, out token);
                    defaults?.TryGetValue(id, out def);

                    // AOD-only tokens are listed when set, the CBS options always
                    ApcbTokenName name = ApcbTokenNames.Get(id);
                    bool isAod = name != null && name.Source != ApcbTokenSource.Cbs;
                    if (isAod && token == null)
                        continue;

                    string apob = NotInApob;
                    if (id == 0xF6E0D48D)
                        apob = rxApob;
                    else if (id == 0x664706F1)
                        apob = txApob;

                    string value;
                    if (!haveTokens)
                        value = "n/a";
                    else if (token != null)
                        value = FormatValue(token, true);
                    else
                        value = def != null ? UiText.Format("AutomaticWithValue", FormatValue(def, false)) : UiText.Get("Automatic");

                    string prompt = name == null ? $"0x{id:X8}" : string.IsNullOrEmpty(name.Prompt) ? name.Name : name.Prompt;
                    rows.Add(new Row
                    {
                        Group = entry.Key,
                        Option = prompt,
                        Value = value,
                        Apob = apob,
                        Name = name?.Name ?? "",
                        Token = token != null ? $"0x{id:X8} {token.TypeName}" : $"0x{id:X8}",
                        IsChanged = token != null,
                        Tooltip = token != null ? BuildTooltip(token, name) : (name != null ? name.Name + Environment.NewLine + UiText.Get("AutoValueNotInApcb") : null)
                    });
                }

                TrainingGrid.ItemsSource = rows;
                TrainingSection.Visibility = rows.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"APCB window, training: {ex.Message}");
                TrainingSection.Visibility = Visibility.Collapsed;
            }
        }

        private string TxDfeFromApob()
        {
            ApobPmuMsgBlock pmu = _apob?.PmuMsgBlock;
            if (pmu == null || pmu.Channels.Count == 0)
                return NotInApob;

            return JoinChannels(pmu.Channels.Select(c =>
                $"{c.TxDfeTaps} (MR111 0x{c.Mr111:X2}{(c.Mr111Uniform ? "" : ", devices differ")})").ToList());
        }

        private string RxDfeFromApob()
        {
            ApobS3PhyReplay phy = _apob?.S3PhyReplay;
            if (phy == null || phy.Channels.Count == 0)
                return NotInApob;

            var values = new List<string>();
            foreach (ApobS3PhyReplay.ChannelPhy c in phy.Channels)
            {
                ApobS3PhyReplay.PhyArray tap2 = c.Arrays.FirstOrDefault(a => a.Label.StartsWith("RxDfeTap2", StringComparison.Ordinal));
                if (tap2 == null)
                    values.Add("?");
                else
                    values.Add(tap2.Values.Any(v => v != 0) ? UiText.Get("AtLeastTwoDfeTaps") : UiText.Get("FewerThanTwoDfeTaps"));
            }

            return JoinChannels(values);
        }

        // "x" when every channel agrees, else "Ch A: x, Ch B: y"
        private static string JoinChannels(IList<string> values)
        {
            if (values.Count == 0)
                return NotInApob;
            if (values.Distinct().Count() == 1)
                return values[0];

            return string.Join(", ", values.Select((v, i) => $"Ch {(char)('A' + i)}: {v}"));
        }

        #endregion

        private static Row MakeRow(ApcbToken t)
        {
            ApcbTokenName name = t.Name;
            string prompt = name == null ? null : string.IsNullOrEmpty(name.Prompt) ? name.Name : name.Prompt;

            return new Row
            {
                Option = prompt ?? UiText.Get("Unknown"),
                Value = FormatValue(t, true),
                Name = name?.Name ?? "",
                Token = $"0x{t.Id:X8} {t.TypeName}",
                IsUnknown = name == null,
                Tooltip = BuildTooltip(t, name)
            };
        }

        private static string BuildTooltip(ApcbToken t, ApcbTokenName name)
        {
            var lines = new List<string>();
            if (name != null)
            {
                if (!string.IsNullOrEmpty(name.Prompt))
                    lines.Add(name.Prompt);
                lines.Add(name.Name);
                switch (name.Source)
                {
                    case ApcbTokenSource.Cbs: lines.Add(UiText.Get("FromAmdCbs")); break;
                    case ApcbTokenSource.Aod: lines.Add(UiText.Get("FromAmdOverclocking")); break;
                    case ApcbTokenSource.AodDerived: lines.Add(UiText.Get("DerivedByAmdOverclocking")); break;
                    case ApcbTokenSource.Oxide: lines.Add(UiText.Get("FromOxideApcb")); break;
                }
            }
            else
            {
                lines.Add(UiText.Get("UnknownToken"));
            }

            lines.Add(UiText.Format("ApcbTokenDescription", t.Id, t.TypeName, t.Instance, t.PriorityMask));
            return string.Join(Environment.NewLine, lines);
        }

        private static uint Mask(ApcbToken t)
        {
            switch (t.TypeId)
            {
                case 0: return t.Value != 0 ? 1u : 0u;
                case 1: return t.Value & 0xFF;
                case 2: return t.Value & 0xFFFF;
                default: return t.Value;
            }
        }

        // "RZQ/3 (80) [3]", "46 (0x2E)", "true"
        private static string FormatValue(ApcbToken t, bool withRaw)
        {
            uint value = Mask(t);
            string label = t.Name?.GetValueLabel(value);

            if (t.TypeId == 0)
                return label ?? (value != 0 ? UiText.Get("EnabledOne") : UiText.Get("DisabledZero"));

            string raw = value < 10 ? value.ToString(CultureInfo.InvariantCulture)
                : string.Format(CultureInfo.InvariantCulture, "{0} (0x{0:X})", value);

            if (label == null)
                return raw;

            return withRaw ? $"{label} [{value.ToString(CultureInfo.InvariantCulture)}]" : label;
        }

        // Named options first, by option text; unknown tokens last, by id
        private static void SortRows(List<Row> rows)
        {
            rows.Sort((a, b) =>
            {
                int c = a.IsUnknown.CompareTo(b.IsUnknown);
                if (c != 0)
                    return c;
                if (a.IsUnknown)
                    return string.CompareOrdinal(a.Token, b.Token);
                c = string.Compare(a.Option, b.Option, StringComparison.OrdinalIgnoreCase);
                return c != 0 ? c : string.CompareOrdinal(a.Name, b.Name);
            });
        }

        private static Row Pair(string name, string value)
        {
            return new Row { Option = name, Value = value };
        }

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

            ClipboardUtils.Copy($"{section.Header}{Environment.NewLine}{ClipboardUtils.GridToText(grid)}", button);
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            InteropMethods.EmptyWorkingSet(System.Diagnostics.Process.GetCurrentProcess().Handle);
        }
    }
}
