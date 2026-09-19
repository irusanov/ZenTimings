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
            public ImageSource Image;
        }

        internal sealed class Result
        {
            public readonly List<Channel> Channels = new List<Channel>();
            public List<Rect> Highlights;
        }

        public static Result Run(FrameworkElement panel, MainViewModel viewModel, IList<MemoryModule> modules,
            IDictionary<byte, Ddr5SpdInfo> spdInfo, Func<uint, BaseDramTimings> readTimings)
        {
            var result = new Result();
            BaseDramTimings original = viewModel.Timings;
            float[] savedRails = { viewModel.SwaAdcV, viewModel.SwbAdcV, viewModel.VppAdcV };
            var channelTimings = new List<BaseDramTimings>();

            // SPD entries match the modules by index. Without a readable PMIC, or any SPD data as for a debug
            // report, the rails are not per module and are left as they are.
            List<Ddr5SpdInfo> spds = spdInfo?.Values.ToList();
            bool perModuleRails = spds != null && spds.Any(spd => spd?.PmicData != null && spd.PmicData.IsValid);

            // A SizeToContent window resizes on every layout pass, which would flicker the main window.
            Window owner = Window.GetWindow(panel);
            SizeToContent sizing = owner.SizeToContent;
            owner.SizeToContent = SizeToContent.Manual;

            try
            {
                var channels = modules
                    .Select((module, index) => new { Module = module, Spd = spds?.ElementAtOrDefault(index) })
                    .GroupBy(entry => entry.Module.DctOffset);

                foreach (var channel in channels)
                {
                    var channelModules = channel.ToList();
                    bool shared = channelModules.Count > 1;
                    BaseDramTimings timings = readTimings(channel.Key);

                    // Bindings update synchronously on the UI thread, so the render sees these values and
                    // the screen never does. A shared channel has no single rail value; each module line
                    // carries its own instead.
                    viewModel.Timings = timings;
                    if (perModuleRails)
                        SetRails(viewModel, shared ? new float[3] : RailsOf(channelModules[0].Spd?.PmicData));
                    panel.UpdateLayout();

                    channelTimings.Add(timings);
                    result.Channels.Add(new Channel
                    {
                        Header = string.Join(" / ", channelModules.Select(m => m.Module.Slot)),
                        ModuleLines = channelModules.Select(m => Describe(m.Module, m.Spd, shared)).ToList(),
                        Image = VisualCapture.Render(panel),
                    });
                }
            }
            finally
            {
                viewModel.Timings = original;
                SetRails(viewModel, savedRails);
                panel.UpdateLayout();
                owner.SizeToContent = sizing;
            }

            result.Highlights = FindDifferingCells(panel, channelTimings);
            return result;
        }

        private static string Describe(MemoryModule module, Ddr5SpdInfo spd, bool withRails)
        {
            var parts = new List<string> { module.ToString() };

            if (!string.IsNullOrEmpty(spd?.DramManufacturer))
                parts.Add($"{spd.DramManufacturer} {VendorUtils.GetDramDieName(spd.DramManufacturer, spd.DramStepping)}".Trim());

            Ddr5PmicData pmic = spd?.PmicData;
            if (pmic != null && pmic.IsValid)
            {
                parts.Add($"PMIC {pmic.VendorName} rev {pmic.RevisionMajor}.{pmic.RevisionMinor}");

                if (withRails)
                {
                    float[] rails = RailsOf(pmic);
                    parts.Add($"VDD {VoltageText(rails[0])}");
                    parts.Add($"VDDQ {VoltageText(rails[1])}");
                    parts.Add($"VPP {VoltageText(rails[2])}");
                }
            }

            // Non-breaking inside a part, so a wrapped line only breaks between parts.
            return string.Join("  ·  ", parts.Select(part => part.Replace(' ', '\u00A0')));
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
