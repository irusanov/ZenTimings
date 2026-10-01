using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ZenStates.Core.Hardware.DRAM.DDR4.Profiles;
using ZenStates.Core.Hardware.DRAM.DDR4.Spd;
using ZenStates.Core.Hardware.DRAM.DDR5.Profiles;
using ZenStates.Core.Hardware.DRAM.DDR5.Spd;

namespace ZenTimings.Windows
{
    /// <summary>A label / value line of an SPD section.</summary>
    internal sealed class SpdRow
    {
        public SpdRow(string label, string value, bool isSerial = false)
        {
            Label = label;
            Value = value;
            IsSerial = isSerial;
        }

        public string Label { get; }
        public string Value { get; }

        /// <summary>The module serial number, which is left out of copied text.</summary>
        public bool IsSerial { get; }
    }

    /// <summary>A timing of a profile: the minimum time from the SPD and the clocks it takes at the profile speed.</summary>
    internal sealed class SpdTimingRow
    {
        public SpdTimingRow(string name, int cycles, string value)
        {
            Name = name;
            Cycles = cycles > 0 ? cycles.ToString(CultureInfo.InvariantCulture) : "";
            Value = value;
        }

        public string Name { get; }
        public string Cycles { get; }
        public string Value { get; }
    }

    /// <summary>A speed profile: JEDEC base, XMP or EXPO.</summary>
    internal sealed class SpdProfileView
    {
        public string Title { get; set; }

        /// <summary>Speed and primary timings, e.g. "DDR5-4800 40-40-40-77".</summary>
        public string Summary { get; set; }

        public List<SpdRow> Details { get; } = new List<SpdRow>();
        public List<SpdTimingRow> Timings { get; } = new List<SpdTimingRow>();
    }

    /// <summary>The SPD of one module arranged for reading: module, components and profiles.</summary>
    internal sealed class SpdModuleView
    {
        public List<SpdRow> Module { get; } = new List<SpdRow>();
        public List<SpdRow> Components { get; } = new List<SpdRow>();
        public List<SpdProfileView> Profiles { get; } = new List<SpdProfileView>();

        public static SpdModuleView Message(string text)
        {
            var view = new SpdModuleView();
            view.Module.Add(new SpdRow("Info", text));
            return view;
        }

        /// <summary>The module and components sections as text, without the serial number.</summary>
        public string ModuleText()
        {
            var sb = new StringBuilder();
            AppendRows(sb, "Module", Module);
            if (Components.Count > 0)
            {
                sb.AppendLine();
                AppendRows(sb, "Components", Components);
            }
            return sb.ToString();
        }

        /// <summary>All profiles as text.</summary>
        public string ProfilesText()
        {
            var sb = new StringBuilder();
            foreach (SpdProfileView profile in Profiles)
            {
                if (sb.Length > 0)
                    sb.AppendLine();

                sb.AppendLine(string.IsNullOrEmpty(profile.Summary) ? profile.Title : $"{profile.Title}: {profile.Summary}");
                foreach (SpdRow row in profile.Details)
                    sb.AppendLine($"  {row.Label,-16} {row.Value}");

                if (profile.Timings.Count > 0)
                {
                    sb.AppendLine($"  {"Timing",-16} {"Cycles",7}  Value");
                    foreach (SpdTimingRow row in profile.Timings)
                        sb.AppendLine($"  {row.Name,-16} {row.Cycles,7}  {row.Value}");
                }
            }
            return sb.ToString();
        }

        private static void AppendRows(StringBuilder sb, string title, IEnumerable<SpdRow> rows)
        {
            sb.AppendLine(title);
            foreach (SpdRow row in rows)
                sb.AppendLine($"  {row.Label,-16} {(row.IsSerial ? "(hidden)" : row.Value)}");
        }
    }

    /// <summary>Turns a decoded SPD (DDR4 or DDR5 / LPDDR5) into the sections of the SPD window.</summary>
    internal static class SpdPresenter
    {
        public static SpdModuleView Build(object info)
        {
            if (info is Ddr5SpdInfo ddr5)
                return Build(ddr5);

            if (info is Ddr4SpdInfo ddr4)
                return Build(ddr4);

            return SpdModuleView.Message("No SPD data available.");
        }

        private static SpdModuleView Build(Ddr5SpdInfo info)
        {
            if (!info.IsValid)
                return SpdModuleView.Message($"Invalid or unsupported SPD (device type 0x{info.DeviceType:X2})");

            var view = new SpdModuleView();
            AddModule(view.Module, info);
            AddComponents(view.Components, info);
            AddProfiles(view.Profiles, info);
            return view;
        }

        private static SpdModuleView Build(Ddr4SpdInfo info)
        {
            if (!info.IsValid)
                return SpdModuleView.Message($"Invalid or unsupported SPD (device type 0x{info.DeviceType:X2})");

            var view = new SpdModuleView();
            AddModule(view.Module, info);
            AddComponents(view.Components, info);
            AddProfiles(view.Profiles, info);
            return view;
        }

        // Clocks of a minimum time at a cycle time, rounded the way each decoder rounds them
        private delegate int ClockConverter(int ps, int tCKps);

        private static readonly ClockConverter Ddr5Clocks = Ddr5SpdTimingMath.ToNck;
        private static readonly ClockConverter Ddr4Clocks = Ddr4SpdDecoder.ToClocks;

        #region Module

        private static void AddModule(List<SpdRow> rows, Ddr5SpdInfo info)
        {
            AddIdentity(rows, info.ModuleManufacturer, info.ModulePartNumber, info.ModuleSerialNumber, info.ModuleMfgDate,
                info.MemoryFamily, info.ModuleTypeString, info.IsHybrid, info.HybridTypeString);

            string ranks = info.RanksPerChannel > 0 ? Plural(info.RanksPerChannel, "rank") : null;
            if (ranks != null && info.LogicalRanksPerChannel > info.RanksPerChannel)
                ranks += $", {info.LogicalRanksPerChannel} logical";
            Add(rows, "Capacity", FormatCapacity(info.TotalCapacityMB, ranks));
            Add(rows, "DRAM maker", ShortVendor(info.DramManufacturer));
            Add(rows, "DRAM stepping", $"0x{info.DramStepping:X2}");
            Add(rows, "SPD revision", info.SpdRevision);

            if (info.PrimaryBusWidthBits > 0)
            {
                int total = info.PrimaryBusWidthBits * Math.Max(1, info.SubChannelsPerDimm);
                string width = info.SubChannelsPerDimm > 1
                    ? $"{total}-bit ({info.SubChannelsPerDimm}x {info.PrimaryBusWidthBits}-bit sub-channels)"
                    : $"{total}-bit";
                if (info.BusWidthExtensionBits > 0)
                    width += $" + {info.BusWidthExtensionBits}-bit ECC per sub-channel";
                Add(rows, "Bus width", width);
            }

            Add(rows, "Temperature", info.OperatingTemperatureRange);
            AddSdram(rows, info.FirstDieDensityMbit, info.FirstDieCount, info.IsAsymmetric, info.SecondDieDensityMbit,
                info.FirstRowBits, info.FirstColumnBits, info.FirstDeviceWidthBits, info.FirstBankGroups, info.FirstBanksPerBankGroup);

            if (!string.IsNullOrEmpty(info.FirstPackageType))
                Add(rows, "Package", info.FirstPackageType);

            if (!info.IsLpddr5)
                Add(rows, "Voltage", $"VDD {info.VddString}, VDDQ {info.VddqString}, VPP {info.VppString}");

            AddOutline(rows, info.ModuleHeight, info.ModuleThickness, info.ReferenceRawCard, info.HeatSpreader);

            if (!info.IsPartial)
                Add(rows, "Checksum", info.BaseCrcValid ? "OK" : "Bad");

            if (info.HasXmp)
                Add(rows, "XMP", DescribeProfiles($"XMP {info.XmpRevision}", XmpNumbers(info.XmpProfiles)));

            if (info.HasExpo)
            {
                Add(rows, "EXPO version", info.ExpoCrcValid ? info.ExpoRevision : $"{info.ExpoRevision} (bad checksum)");
                Add(rows, "EXPO profiles", DescribeProfiles(null, ExpoNumbers(info.ExpoProfile1, info.ExpoProfile2)));
            }
        }

        private static void AddModule(List<SpdRow> rows, Ddr4SpdInfo info)
        {
            AddIdentity(rows, info.ModuleManufacturer, info.ModulePartNumber, info.ModuleSerialNumber, info.ModuleMfgDate,
                info.MemoryFamily, info.ModuleTypeString, info.IsHybrid, info.HybridTypeString);

            string ranks = info.PackageRanks > 0 ? Plural(info.PackageRanks, "rank") : null;
            if (ranks != null && info.LogicalRanks > info.PackageRanks)
                ranks += $", {info.LogicalRanks} logical";
            Add(rows, "Capacity", FormatCapacity(info.TotalCapacityMB, ranks));
            Add(rows, "DRAM maker", ShortVendor(info.DramManufacturer));
            Add(rows, "DRAM stepping", $"0x{info.DramStepping:X2}");
            Add(rows, "SPD revision", info.SpdRevision);

            if (info.PrimaryBusWidthBits > 0)
                Add(rows, "Bus width", info.HasEcc
                    ? $"{info.PrimaryBusWidthBits}-bit + {info.BusWidthExtensionBits}-bit ECC"
                    : $"{info.PrimaryBusWidthBits}-bit");

            AddSdram(rows, info.DieDensityMbit, info.DieCount, info.IsAsymmetric, info.SecondDieDensityMbit,
                info.RowBits, info.ColumnBits, info.DeviceWidthBits, info.BankGroups, info.BanksPerGroup);

            if (!string.IsNullOrEmpty(info.PackageType))
            {
                string package = info.PackageType;
                if (!string.IsNullOrEmpty(info.SignalLoading))
                    package += ", " + info.SignalLoading;
                Add(rows, "Package", package);
            }

            Add(rows, "Voltage", info.VddString);
            Add(rows, "PPR", info.PostPackageRepair + (info.SoftPpr ? ", soft PPR" : ""));
            AddOutline(rows, info.ModuleHeight, info.ModuleThickness, info.ReferenceRawCard, false);

            if (!info.IsPartial)
                Add(rows, "Checksum", info.BaseCrcValid && info.ModuleCrcValid ? "OK" : "Bad");

            if (info.HasXmp)
                Add(rows, "XMP", DescribeProfiles($"XMP {info.XmpRevision}", XmpNumbers(info.XmpProfiles)));
        }

        private static void AddIdentity(List<SpdRow> rows, string manufacturer, string partNumber, string serial, string date,
            string family, string moduleType, bool isHybrid, string hybridType)
        {
            Add(rows, "Manufacturer", ShortVendor(manufacturer));
            Add(rows, "Part number", partNumber);
            if (!string.IsNullOrEmpty(serial))
                rows.Add(new SpdRow("Serial", serial, true));
            Add(rows, "Manufactured", date);

            string type = moduleType;
            if (isHybrid && !string.IsNullOrEmpty(hybridType))
                type += $" ({hybridType})";
            Add(rows, "Module type", $"{family} {type}".Trim());
        }

        private static void AddSdram(List<SpdRow> rows, int densityMbit, int dieCount, bool asymmetric, int secondDensityMbit,
            int rowBits, int columnBits, int deviceWidth, int bankGroups, int banksPerGroup)
        {
            if (densityMbit > 0)
            {
                string density = FormatDensity(densityMbit);
                if (dieCount > 1)
                    density += $", {dieCount} dies per package";
                if (asymmetric && secondDensityMbit > 0)
                    density += $" (odd ranks {FormatDensity(secondDensityMbit)})";
                Add(rows, "Die density", density);
            }

            if (rowBits > 0 && columnBits > 0)
                Add(rows, "Addressing", $"{rowBits} row / {columnBits} column bits");

            if (deviceWidth > 0)
                Add(rows, "SDRAM width", $"x{deviceWidth}");

            if (bankGroups > 0 && banksPerGroup > 0)
                Add(rows, "Banks", $"{Plural(bankGroups, "group")} x {banksPerGroup} = {bankGroups * banksPerGroup} banks");
        }

        private static void AddOutline(List<SpdRow> rows, string height, string thickness, string rawCard, bool heatSpreader)
        {
            if (!string.IsNullOrEmpty(height))
                Add(rows, "Height", heatSpreader ? $"{height}, heat spreader" : height);
            Add(rows, "Thickness", thickness);
            Add(rows, "Raw card", rawCard);
        }

        #endregion

        #region Components

        private static void AddComponents(List<SpdRow> rows, Ddr5SpdInfo info)
        {
            AddDevice(rows, "SPD hub", info.SpdDevice);
            AddDevice(rows, "PMIC 0", info.Pmic0);
            AddDevice(rows, "PMIC 1", info.Pmic1);
            AddDevice(rows, "PMIC 2", info.Pmic2);

            if (info.ThermalSensors != null && info.ThermalSensors.Installed)
            {
                string which = info.ThermalSensor0Present && info.ThermalSensor1Present ? "TS0, TS1"
                    : info.ThermalSensor0Present ? "TS0" : "TS1";
                Add(rows, "Thermal sensors", $"{DescribeDevice(info.ThermalSensors)}, {which}");
            }
        }

        private static void AddComponents(List<SpdRow> rows, Ddr4SpdInfo info)
        {
            if (!string.IsNullOrEmpty(info.RegisterManufacturer))
                Add(rows, "Register", $"{ShortVendor(info.RegisterManufacturer)} (rev 0x{info.RegisterRevision:X2})");

            if (info.HasThermalSensor)
                Add(rows, "Thermal sensor", "TSOD (JC-42.4)");
        }

        private static void AddDevice(List<SpdRow> rows, string label, Ddr5SpdDevice device)
        {
            if (device != null && device.Installed)
                Add(rows, label, DescribeDevice(device));
        }

        private static string DescribeDevice(Ddr5SpdDevice device)
        {
            string vendor = ShortVendor(device.Manufacturer);
            return string.IsNullOrEmpty(vendor)
                ? $"{device.TypeName} (rev {device.Revision})"
                : $"{vendor} {device.TypeName} (rev {device.Revision})";
        }

        #endregion

        #region Profiles

        private static void AddProfiles(List<SpdProfileView> profiles, Ddr5SpdInfo info)
        {
            if (info.tCKAVGminPs > 0)
            {
                int tCK = info.tCKAVGminPs;
                var jedec = new SpdProfileView { Title = "JEDEC Base" };
                jedec.Summary = Summary(info.SpeedGrade, Ddr5Clocks, tCK, info.CL, info.tRCDminPs, info.tRPminPs, info.tRASminPs);
                AddSpeed(jedec, info.SpeedMTs, info.ClockMHz, tCK);
                if (info.SupportedCLs != null && info.SupportedCLs.Count > 0)
                    jedec.Details.Add(new SpdRow("Supported CL", string.Join(", ", info.SupportedCLs)));

                if (info.IsLpddr5)
                {
                    // LPDDR5: tAA gives the read latency at the CK clock; precharge is all banks / per bank
                    if (info.tAAminPs > 0)
                        jedec.Timings.Add(new SpdTimingRow("tAA (RL)", info.CL, $"{info.tAAminPs} ps"));
                    AddPs(jedec, Ddr5Clocks, tCK, "tRCD", info.tRCDminPs);
                    AddPs(jedec, Ddr5Clocks, tCK, "tRPab", info.tRPminPs);
                    AddPs(jedec, Ddr5Clocks, tCK, "tRPpb", info.tRPpbMinPs);
                    AddPs(jedec, Ddr5Clocks, tCK, "tRFCab", info.tRFCabMinPs, true);
                    AddPs(jedec, Ddr5Clocks, tCK, "tRFCpb", info.tRFCpbMinPs, true);
                }
                else
                {
                    AddPrimaryTimings(jedec, Ddr5Clocks, tCK, info.CL, info.tAAminPs, info.tRCDminPs, info.tRPminPs, info.tRASminPs, info.tRCminPs, info.tWRminPs);
                    AddNs(jedec, Ddr5Clocks, tCK, "tRFC1", info.tRFC1minNs);
                    AddNs(jedec, Ddr5Clocks, tCK, "tRFC2", info.tRFC2minNs);
                    AddNs(jedec, Ddr5Clocks, tCK, "tRFCsb", info.tRFCsbMinNs);
                }

                profiles.Add(jedec);
            }

            if (info.XmpProfiles != null)
            {
                foreach (Ddr5XmpProfile xmp in info.XmpProfiles)
                {
                    if (xmp == null || !xmp.IsValid)
                        continue;

                    string title = $"XMP {xmp.ProfileNumber}";
                    if (!string.IsNullOrWhiteSpace(xmp.ProfileName))
                        title += $" \u201C{xmp.ProfileName.Trim()}\u201D";

                    SpdProfileView view = Ddr5Profile(title, xmp.SpeedGrade, xmp.SpeedMTs, xmp.ClockMHz, xmp.tCKAVGminPs, xmp.CL,
                        xmp.tRCDminPs, xmp.tRPminPs, xmp.tRASminPs);
                    view.Details.Add(new SpdRow("Voltages", $"VDD {Volts(xmp.VddMv)}, VDDQ {Volts(xmp.VddqMv)}, VPP {Volts(xmp.VppMv)}"));
                    if (xmp.SupportedCLs != null && xmp.SupportedCLs.Count > 0)
                        view.Details.Add(new SpdRow("Supported CL", string.Join(", ", xmp.SupportedCLs)));
                    if (xmp.DynamicMemoryBoost)
                        view.Details.Add(new SpdRow("Memory boost", "Dynamic Memory Boost"));
                    AddDdr5ProfileTimings(view, xmp.tCKAVGminPs, xmp.CL, xmp.tAAminPs, xmp.tRCDminPs, xmp.tRPminPs, xmp.tRASminPs,
                        xmp.tRCminPs, xmp.tWRminPs, xmp.tRFC1minNs, xmp.tRFC2minNs, xmp.tRFCsbMinNs);
                    profiles.Add(view);
                }
            }

            foreach (Ddr5ExpoProfile expo in new[] { info.ExpoProfile1, info.ExpoProfile2 })
            {
                if (expo == null || !expo.IsValid)
                    continue;

                SpdProfileView view = Ddr5Profile($"EXPO {expo.ProfileNumber}", expo.SpeedGrade, expo.SpeedMTs, expo.ClockMHz,
                    expo.tCKAVGminPs, expo.CL, expo.tRCDminPs, expo.tRPminPs, expo.tRASminPs);
                view.Details.Add(new SpdRow("Voltages", $"VDD {Volts(expo.VddMv)}, VDDQ {Volts(expo.VddqMv)}, VPP {Volts(expo.VppMv)}"));
                AddDdr5ProfileTimings(view, expo.tCKAVGminPs, expo.CL, expo.tAAminPs, expo.tRCDminPs, expo.tRPminPs, expo.tRASminPs,
                    expo.tRCminPs, expo.tWRminPs, expo.tRFC1minNs, expo.tRFC2minNs, expo.tRFCsbMinNs);
                profiles.Add(view);
            }
        }

        private static SpdProfileView Ddr5Profile(string title, string speedGrade, int speedMTs, double clockMHz, int tCK,
            int cl, int tRCD, int tRP, int tRAS)
        {
            var view = new SpdProfileView
            {
                Title = title,
                Summary = Summary(speedGrade, Ddr5Clocks, tCK, cl, tRCD, tRP, tRAS)
            };
            AddSpeed(view, speedMTs, clockMHz, tCK);
            return view;
        }

        private static void AddDdr5ProfileTimings(SpdProfileView view, int tCK, int cl, int tAA, int tRCD, int tRP, int tRAS,
            int tRC, int tWR, int tRFC1Ns, int tRFC2Ns, int tRFCsbNs)
        {
            AddPrimaryTimings(view, Ddr5Clocks, tCK, cl, tAA, tRCD, tRP, tRAS, tRC, tWR);
            AddNs(view, Ddr5Clocks, tCK, "tRFC1", tRFC1Ns);
            AddNs(view, Ddr5Clocks, tCK, "tRFC2", tRFC2Ns);
            AddNs(view, Ddr5Clocks, tCK, "tRFCsb", tRFCsbNs);
        }

        private static void AddProfiles(List<SpdProfileView> profiles, Ddr4SpdInfo info)
        {
            if (info.tCKAVGminPs > 0)
            {
                int tCK = info.tCKAVGminPs;
                var jedec = new SpdProfileView { Title = "JEDEC Base" };
                jedec.Summary = Summary(info.SpeedGrade, Ddr4Clocks, tCK, info.CL, info.tRCDminPs, info.tRPminPs, info.tRASminPs);
                AddSpeed(jedec, info.SpeedMTs, info.ClockMHz, tCK);
                if (info.SupportedCLs != null && info.SupportedCLs.Count > 0)
                    jedec.Details.Add(new SpdRow("Supported CL", string.Join(", ", info.SupportedCLs)));

                AddPrimaryTimings(jedec, Ddr4Clocks, tCK, info.CL, info.tAAminPs, info.tRCDminPs, info.tRPminPs, info.tRASminPs, info.tRCminPs, info.tWRminPs);
                AddDdr4Timings(jedec, tCK, info.tRFC1minPs, info.tRFC2minPs, info.tRFC4minPs,
                    info.tRRD_SminPs, info.tRRD_LminPs, info.tCCD_LminPs, info.tFAWminPs);
                AddPs(jedec, Ddr4Clocks, tCK, "tWTR_S", info.tWTR_SminPs);
                AddPs(jedec, Ddr4Clocks, tCK, "tWTR_L", info.tWTR_LminPs);
                profiles.Add(jedec);
            }

            if (info.XmpProfiles == null)
                return;

            foreach (Ddr4XmpProfile xmp in info.XmpProfiles)
            {
                if (xmp == null || !xmp.IsValid)
                    continue;

                int tCK = xmp.tCKAVGminPs;
                var view = new SpdProfileView
                {
                    Title = $"XMP {xmp.ProfileNumber}",
                    Summary = Summary(xmp.SpeedGrade, Ddr4Clocks, tCK, xmp.CL, xmp.tRCDminPs, xmp.tRPminPs, xmp.tRASminPs)
                };
                AddSpeed(view, xmp.SpeedMTs, xmp.ClockMHz, tCK);
                view.Details.Add(new SpdRow("Voltage", $"VDD {Volts(xmp.VddMv)}"));
                if (xmp.DimmsPerChannel > 0)
                    view.Details.Add(new SpdRow("DIMMs/channel", xmp.DimmsPerChannel.ToString(CultureInfo.InvariantCulture)));
                if (xmp.SupportedCLs != null && xmp.SupportedCLs.Count > 0)
                    view.Details.Add(new SpdRow("Supported CL", string.Join(", ", xmp.SupportedCLs)));

                AddPrimaryTimings(view, Ddr4Clocks, tCK, xmp.CL, xmp.tAAminPs, xmp.tRCDminPs, xmp.tRPminPs, xmp.tRASminPs, xmp.tRCminPs, 0);
                AddDdr4Timings(view, tCK, xmp.tRFC1minPs, xmp.tRFC2minPs, xmp.tRFC4minPs,
                    xmp.tRRD_SminPs, xmp.tRRD_LminPs, xmp.tCCD_LminPs, xmp.tFAWminPs);
                profiles.Add(view);
            }
        }

        private static void AddDdr4Timings(SpdProfileView view, int tCK,
            int tRFC1, int tRFC2, int tRFC4, int tRRD_S, int tRRD_L, int tCCD_L, int tFAW)
        {
            AddPs(view, Ddr4Clocks, tCK, "tRFC1", tRFC1, true);
            AddPs(view, Ddr4Clocks, tCK, "tRFC2", tRFC2, true);
            AddPs(view, Ddr4Clocks, tCK, "tRFC4", tRFC4, true);
            AddPs(view, Ddr4Clocks, tCK, "tRRD_S", tRRD_S);
            AddPs(view, Ddr4Clocks, tCK, "tRRD_L", tRRD_L);
            AddPs(view, Ddr4Clocks, tCK, "tCCD_L", tCCD_L);
            AddPs(view, Ddr4Clocks, tCK, "tFAW", tFAW);
        }

        private static void AddPrimaryTimings(SpdProfileView view, ClockConverter clocks, int tCK, int cl,
            int tAA, int tRCD, int tRP, int tRAS, int tRC, int tWR)
        {
            // CL is tAA rounded up to a CAS latency the module supports (even on DDR5, in the SPD CL mask)
            if (tAA > 0)
                view.Timings.Add(new SpdTimingRow("tAA (CL)", cl, $"{tAA} ps"));
            AddPs(view, clocks, tCK, "tRCD", tRCD);
            AddPs(view, clocks, tCK, "tRP", tRP);
            AddPs(view, clocks, tCK, "tRAS", tRAS);
            AddPs(view, clocks, tCK, "tRC", tRC);
            AddPs(view, clocks, tCK, "tWR", tWR);
        }

        private static void AddSpeed(SpdProfileView view, int speedMTs, double clockMHz, int tCK)
        {
            if (speedMTs > 0)
                view.Details.Add(new SpdRow("Speed", $"{speedMTs} MT/s ({clockMHz.ToString("0.#", CultureInfo.InvariantCulture)} MHz), tCK {tCK} ps"));
        }

        private static void AddPs(SpdProfileView view, ClockConverter clocks, int tCK, string name, int ps, bool asNs = false)
        {
            if (ps <= 0)
                return;

            string value = asNs && ps % 1000 == 0 ? $"{ps / 1000} ns" : $"{ps} ps";
            view.Timings.Add(new SpdTimingRow(name, clocks(ps, tCK), value));
        }

        private static void AddNs(SpdProfileView view, ClockConverter clocks, int tCK, string name, int ns)
        {
            if (ns > 0)
                view.Timings.Add(new SpdTimingRow(name, clocks(ns * 1000, tCK), $"{ns} ns"));
        }

        // "DDR5-4800 40-40-40-77": CL-tRCD-tRP-tRAS at the profile speed
        private static string Summary(string speedGrade, ClockConverter clocks, int tCK, int cl, int tRCD, int tRP, int tRAS)
        {
            if (cl <= 0)
                return speedGrade;

            string summary = $"{speedGrade} {cl}-{clocks(tRCD, tCK)}-{clocks(tRP, tCK)}";
            return tRAS > 0 ? $"{summary}-{clocks(tRAS, tCK)}" : summary;
        }

        private static IEnumerable<int> XmpNumbers(Ddr5XmpProfile[] profiles)
        {
            return profiles == null ? Enumerable.Empty<int>() : profiles.Where(p => p != null && p.IsValid).Select(p => p.ProfileNumber);
        }

        private static IEnumerable<int> XmpNumbers(Ddr4XmpProfile[] profiles)
        {
            return profiles == null ? Enumerable.Empty<int>() : profiles.Where(p => p != null && p.IsValid).Select(p => p.ProfileNumber);
        }

        private static IEnumerable<int> ExpoNumbers(params Ddr5ExpoProfile[] profiles)
        {
            return profiles.Where(p => p != null && p.IsValid).Select(p => p.ProfileNumber);
        }

        #endregion

        #region Formatting

        private static void Add(List<SpdRow> rows, string label, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                rows.Add(new SpdRow(label, value.Trim()));
        }

        private static string FormatCapacity(long megabytes, string ranks)
        {
            if (megabytes <= 0)
                return ranks;

            string size = megabytes % 1024 == 0 ? $"{megabytes / 1024} GB" : $"{megabytes} MB";
            return ranks != null ? $"{size} ({ranks})" : size;
        }

        private static string FormatDensity(int megabits)
        {
            return megabits % 1024 == 0 ? $"{megabits / 1024} Gb" : $"{megabits} Mb";
        }

        private static string Plural(int count, string noun)
        {
            return count == 1 ? $"1 {noun}" : $"{count} {noun}s";
        }

        private static string Volts(int millivolts)
        {
            return (millivolts / 1000.0).ToString("0.00#", CultureInfo.InvariantCulture) + " V";
        }

        // "XMP 3.0: profiles 1, 2"
        private static string DescribeProfiles(string prefix, IEnumerable<int> profileNumbers)
        {
            List<string> numbers = profileNumbers.Select(n => n.ToString(CultureInfo.InvariantCulture)).ToList();

            string list = numbers.Count == 0 ? "none" : (numbers.Count == 1 ? "profile " : "profiles ") + string.Join(", ", numbers);
            return prefix == null ? list : $"{prefix.Trim()}: {list}";
        }

        // JEP106 names carry legal suffixes ("Rambus Inc", "Richtek Power"); the first word is enough in a list
        private static readonly string[] VendorSuffixes =
        {
            " Inc.", " Inc", " Corporation", " Corp.", " Corp", " Co., Ltd.", " Co. Ltd.", " Co., Ltd", " Ltd.", " Ltd",
            " Limited", " LLC", " GmbH", " AG", " S.A.", " B.V.", " Technology Group", " Technologies", " Technology",
            " Electronics", " Semiconductor", " Power"
        };

        private static string ShortVendor(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            if (name.StartsWith("Invalid", StringComparison.Ordinal) || name.StartsWith("Unknown", StringComparison.Ordinal))
                return name;

            string result = name.Trim().TrimEnd(',');
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (string suffix in VendorSuffixes)
                {
                    if (result.Length > suffix.Length && result.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    {
                        result = result.Substring(0, result.Length - suffix.Length).TrimEnd(',', ' ');
                        changed = true;
                    }
                }
            }

            return result;
        }

        #endregion
    }
}
