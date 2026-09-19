using ZenStates.Core.Hardware.DRAM;

namespace ZenTimings.Export
{
    public sealed partial class SnapshotBuilder
    {
        // A reader that was not written for this file, such as an AI assistant, cannot tell a regular timing from
        // a raw register field or a measured voltage from a BIOS set point. Only the exported sections are explained.
        private SnapshotObject BuildLegend()
        {
            var legend = new SnapshotObject()
                .Add("about", "Snapshot of an AMD Ryzen system made by ZenTimings. It shows what is applied right now, which can differ from what was entered in BIOS.")
                .Add("structure", "static = hardware that does not change while the system runs, config = memory configuration applied at boot, readings = live values sampled at readings.sampled_at.")
                .Add("null", "null means the value is not available or does not exist on this platform. When the reason is known it is listed under unavailable with the path.");

            bool ddr4 = memoryType == MemType.DDR4 || memoryType == MemType.LPDDR4;
            bool timings = options.Has(SnapshotSections.Timings);
            bool registers = options.Has(SnapshotSections.Registers);

            if (timings || registers)
                legend.Add("dct", "dct is the index of a memory channel (DRAM controller), per_dct has one entry per populated channel. Names in dct_mismatch differ between channels, the main ZenTimings window shows one channel at a time.");

            if (timings)
            {
                legend.Add("timings", "Read back from the memory controller. Names are the usual AMD ones without the leading t (CL = tCL, RCDRD = tRCDRD, RDRDSCL = tRDRDSCL). The unit is memory clock cycles, except RFCns and REFIns (nanoseconds), Frequency (MT/s) and Ratio (memory clock multiplier).");

                // The refresh timings and the way the active one is chosen differ between the generations
                legend.Add("refresh", ddr4
                    ? "Only one refresh timing is in use: RFC when RefreshMode is NORMAL, otherwise RFC2 or RFC4, FGR says which one (2 or 4). RFCns is the active one in nanoseconds."
                    : "Only one refresh timing is in use: RFC when RefreshMode is NORMAL, otherwise RFC2, together with RFCsb when it is MIXED. RFCns is the active one in nanoseconds (active timing = RFCns x MCLK in GHz), the inactive ones keep their BIOS values.");

                legend.Add("not_tunable", "PHYRDL, PHYWRL and PHYWRD are results of memory training, a PHYRDL difference between channels is common. SD and DD variants (RDRDSD, RDRDDD, WRWRSD, WRWRDD) only matter with two ranks or two DIMMs per channel.");

                if (!ddr4)
                    legend.Add("nitro", "Nitro is the DDR5 Nitro mode: RxData, TxData and CtrlLine set the timing between the memory controller and the PHY.");
            }

            if (registers)
                legend.Add("registers", "registers holds raw bit fields of the memory controller that have no public documentation. Do not guess their meaning and do not base tuning advice on them.");

            if (options.Has(SnapshotSections.Aod) || options.Has(SnapshotSections.Apob))
                legend.Add("aod_apob", "aod is the ACPI table BIOS publishes with its memory settings, apob is the block the platform firmware (PSP) writes during memory training. Both hold resistances, drive strengths and voltages and they can disagree, ZenTimings shows apob by default. A value is {raw, text}: text is what BIOS shows (ohms or RZQ/x), raw is the register code. {mv} is a voltage set point in millivolts. In apob, main is what ZenTimings shows, extended is a second copy that is only used where main has no value.");

            if (options.Has(SnapshotSections.BiosController))
                legend.Add("ddr4_bios_controller", "BIOS table with the DDR4 termination, drive strength and setup settings as raw register codes. MemVddio, MemVtt and MemVpp are voltage set points in millivolts.");

            if (options.Has(SnapshotSections.PowerTable))
                legend.Add("power_table", "From the SMU power table. mclk = memory clock (MT/s divided by 2), uclk = unified memory controller clock, fclk = Infinity Fabric clock; mclk equal to uclk is the 1:1 mode. vddcr_soc, cldo_vddp, cldo_vddg and vdd_misc are CPU rails.");

            if (options.Has(SnapshotSections.Sensors))
                legend.Add("sensors", "Read from the sensor chip of the motherboard. Names like Voltage #6 are inputs the application has no label for, what they measure is unknown. A null value means nothing is connected or the reading is invalid. min and max are counted since the application started.");

            if (options.Has(SnapshotSections.DimmTelemetry))
                legend.Add("dimm", "Measured on the module by its PMIC and SPD hub: vdd, vddq and vpp are actual voltages, unlike the set points in aod.");

            if (options.Has(SnapshotSections.Spd))
                legend.Add("spd", "What the module reports about itself: JEDEC, XMP and EXPO profiles are what it is rated for, not what is applied. Times ending with Ps are picoseconds, with Ns nanoseconds. When IsPartial is true only the fields read at startup are listed, the SPD window in ZenTimings loads the rest. In pmic the set points are decoded two ways, ...Mv (JEDEC 7-bit) and ...Mv8bit (vendor extension used in HighVoltageMode), compare them with the measured voltages in readings.dimm to see which one applies.");

            return legend;
        }
    }
}
