using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using ZenStates.Core.Hardware.DRAM;
using ZenStates.Core.Hardware.DRAM.DDR5.Pmic;
using ZenStates.Core.Hardware.DRAM.DDR5.Spd;
using ZenTimings.Utils;
using ZenTimings.ViewModels;

namespace ZenTimings.Windows
{
    /// <summary>
    /// Renders the main timings panel once per memory channel and finds the cells that differ between channels.
    /// </summary>
    internal static class AllDimmsCapture
    {
        internal sealed class Channel
        {
            public string Header;
            public List<string> ModuleLines;
            public BaseDramTimings Timings;
            public Ddr5PmicData PmicData;
        }

        internal sealed class Result
        {
            public readonly List<Channel> Channels = new List<Channel>();
        }

        public static Result Run(IList<MemoryModule> modules, IDictionary<byte, Ddr5SpdInfo> spdInfo, Func<uint, BaseDramTimings> readTimings)
        {
            var result = new Result();

            // SPD entries match the modules by index. Without a readable PMIC, or any SPD data as for a debug
            // report, the rails are not per module and are left as they are.
            List<Ddr5SpdInfo> spds = spdInfo?.Values.ToList();

            var channels = modules
                .Select((module, index) => new { Module = module, Spd = spds?.ElementAtOrDefault(index) })
                .GroupBy(entry => entry.Module.DctOffset);

            foreach (var channel in channels)
            {
                var channelModules = channel.ToList();
                BaseDramTimings timings = readTimings(channel.Key);
                Ddr5PmicData channelPmic = ChannelPmicData(channelModules.Select(m => m.Spd?.PmicData));

                result.Channels.Add(new Channel
                {
                    Header = string.Join(" / ", channelModules.Select(m => m.Module.Slot)),
                    ModuleLines = channelModules.Select(m => Describe(m.Module, m.Spd)).ToList(),
                    Timings = timings,
                    PmicData = channelPmic,
                });
            }

            return result;
        }

        private static string Describe(MemoryModule module, Ddr5SpdInfo spd)
        {
            var parts = new List<string> { module.ToString() };

            if (!string.IsNullOrEmpty(spd?.DramManufacturer))
                parts.Add($"{spd.DramManufacturer} {VendorUtils.GetDramDieName(spd.DramManufacturer, spd.DramStepping)}".Trim());

            Ddr5PmicData pmic = spd?.PmicData;
            if (pmic != null && pmic.IsValid)
            {
                float[] rails = RailsOf(pmic);
                parts.Add($"PMIC {pmic.VendorName} rev {pmic.RevisionMajor}.{pmic.RevisionMinor}");
                parts.Add($"VDD {VoltageText(rails[0])}");
                parts.Add($"VDDQ {VoltageText(rails[1])}");
                parts.Add($"VPP {VoltageText(rails[2])}");
            }

            // Non-breaking inside a part, so a wrapped line only breaks between parts.
            return string.Join("  ·  ", parts.Select(part => part.Replace(' ', '\u00A0')));
        }

        private static Ddr5PmicData ChannelPmicData(IEnumerable<Ddr5PmicData> pmics)
        {
            List<Ddr5PmicData> validPmics = pmics.Where(pmic => pmic != null && pmic.IsValid).ToList();
            if (validPmics.Count == 0)
                return null;

            Ddr5PmicData first = validPmics[0];
            if (validPmics.All(pmic => SameRails(first, pmic)))
                return first;

            return null;
        }

        private static bool SameRails(Ddr5PmicData left, Ddr5PmicData right)
        {
            return left.SwaAdcMv == right.SwaAdcMv &&
                   left.SwbAdcMv == right.SwbAdcMv &&
                   left.SwcAdcMv == right.SwcAdcMv;
        }

        private static float[] RailsOf(Ddr5PmicData pmic)
        {
            if (pmic == null || !pmic.IsValid)
                return new float[3];

            return new[] { Volts(pmic.SwaAdcMv), Volts(pmic.SwbAdcMv), Volts(pmic.SwcAdcMv) };
        }

        private static float Volts(int millivolts) => millivolts > 0 ? millivolts / 1000.0f : 0;

        private static string VoltageText(float volts) => volts > 0 ? $"{volts:F4}V" : "N/A";

        private static void SetRails(MainViewModel viewModel, float[] rails)
        {
            viewModel.SwaAdcV = rails[0];
            viewModel.SwbAdcV = rails[1];
            viewModel.VppAdcV = rails[2];
        }

        // Measured on the restored panel; every render shares its layout, so the same rectangles fit them all.
        private static List<Rect> FindDifferingCells(FrameworkElement panel, List<BaseDramTimings> channels)
        {
            return Descendants(panel)
                .OfType<TextBlock>()
                .Where(text => text.IsVisible && Differs(channels, BindingOperations.GetBinding(text, TextBlock.TextProperty)?.Path?.Path))
                .Select(text =>
                {
                    Rect bounds = text.TransformToAncestor(panel).TransformBounds(new Rect(text.RenderSize));
                    bounds.Inflate(3, 1);
                    return bounds;
                })
                .ToList();
        }

        // Only bound timings are compared, resolved the way the binding resolves them: some getters are
        // computed (Frequency can read MMIO), and Ddr5Timings hides RFCns with 'new'.
        private static bool Differs(List<BaseDramTimings> channels, string path)
        {
            const string prefix = "Timings.";
            if (path == null || !path.StartsWith(prefix, StringComparison.Ordinal))
                return false;

            PropertyDescriptor property = TypeDescriptor.GetProperties(channels[0])[path.Substring(prefix.Length)];
            if (property == null)
                return false;

            object first = property.GetValue(channels[0]);
            return channels.Skip(1).Any(timings => !Equals(property.GetValue(timings), first));
        }

        private static IEnumerable<DependencyObject> Descendants(DependencyObject node)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(node, i);
                yield return child;

                foreach (DependencyObject descendant in Descendants(child))
                    yield return descendant;
            }
        }
    }
}
