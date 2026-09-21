using System;
using System.Collections.Generic;
using System.Linq;
using ZenStates.Core.Hardware.DRAM;
using ZenStates.Core.Hardware.DRAM.DDR5.Pmic;
using ZenStates.Core.Hardware.DRAM.DDR5.Spd;
using ZenTimings.Utils;

namespace ZenTimings.Windows
{
    /// <summary>
    /// Collects each memory channel's timings, PMIC data and module description for the All DIMMs window.
    /// </summary>
    internal static class AllDimmsCapture
    {
        internal sealed class Channel
        {
            //public string Header;
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
                    //Header = string.Join(" / ", channelModules.Select(m => m.Module.Slot)),
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
                parts.Add($"PMIC {pmic.VendorName} rev {pmic.RevisionMajor}.{pmic.RevisionMinor}");
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
    }
}
