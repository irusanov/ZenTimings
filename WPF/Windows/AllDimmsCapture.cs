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
        internal sealed class ModuleInfo
        {
            public string LogoResourceName;
            public string VendorLine;
            public string DetailLine;
        }

        internal sealed class Channel
        {
            //public string Header;
            public List<ModuleInfo> Modules;
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
                    Modules = channelModules.Select(m => Describe(m.Module, m.Spd)).ToList(),
                    Timings = timings,
                    PmicData = channelPmic,
                });
            }

            return result;
        }

        private static ModuleInfo Describe(MemoryModule module, Ddr5SpdInfo spd)
        {
            string vendor = !string.IsNullOrEmpty(module.Manufacturer) ? module.Manufacturer : null;
            string vendorAndPart = string.Join(" ", new[] { vendor, module.PartNumber }.Where(s => !string.IsNullOrEmpty(s)));

            string vendorLine;
            if (string.IsNullOrEmpty(module.Slot))
                vendorLine = vendorAndPart;
            else if (string.IsNullOrEmpty(vendorAndPart))
                vendorLine = module.Slot;
            else
                vendorLine = $"{module.Slot}: {vendorAndPart}";

            var detailParts = new List<string>();

            if (!string.IsNullOrEmpty(spd?.DramManufacturer))
                detailParts.Add($"{spd.DramManufacturer} {VendorUtils.GetDramDieName(spd.DramManufacturer, spd.DramStepping)}".Trim());

            Ddr5PmicData pmic = spd?.PmicData;
            if (pmic != null && pmic.IsValid)
                detailParts.Add($"PMIC {pmic.VendorName} rev {pmic.RevisionMajor}.{pmic.RevisionMinor}");

            return new ModuleInfo
            {
                LogoResourceName = VendorUtils.GetMemoryModuleLogo(module),
                VendorLine = vendorLine.Replace(' ', ' '),
                DetailLine = string.Join("  ·  ", detailParts.Select(part => part.Replace(' ', ' '))),
            };
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
