using System;
using ZenStates.Core;
using ZenStates.Core.DRAM;

namespace ZenTimings
{
    internal static class VendorUtils
    {
        internal static bool IsRogMotherboard(SystemInfo info) =>
            IsAsusMotherboard(info) &&
            StartsWith(info?.MbName, "rog");

        internal static bool IsAYWMotherboard(SystemInfo info) =>
            IsAsusMotherboard(info) &&
            Contains(info?.MbName, "ayw");

        internal static bool IsProArtMotherboard(SystemInfo info) =>
            IsAsusMotherboard(info) &&
            Contains(info?.MbName, "proart");

        internal static string GetMotherboardLink(SystemInfo info)
        {
            if (info == null || string.IsNullOrWhiteSpace(info.MbName))
                return null;

            if (IsMsiMotherboard(info))
            {
                string mbName = info.MbName.Trim().ToLowerInvariant();

                if (StartsWith(mbName, "meg"))
                {
                    return "https://msi.com/Motherboards/Products#?tag=MEG-Series";
                }
                else if (StartsWith(mbName, "mag"))
                {
                    return "https://msi.com/Motherboards/Products#?tag=MAG-Series";
                }
                else if (StartsWith(mbName, "mpg"))
                {
                    return "https://msi.com/Motherboards/Products#?tag=MPG-Series";
                }
                else if (StartsWith(mbName, "pro"))
                {
                    return "https://msi.com/Motherboards/Products#?tag=PRO-Series";
                }
                else
                {
                    return $"https://msi.com/Motherboards/";
                }
            }

            if (!IsRogMotherboard(info) && !IsAYWMotherboard(info) && !IsProArtMotherboard(info))
                return null;

            string[] mbNameParts = info.MbName
                .Trim()
                .ToLowerInvariant()
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (mbNameParts.Length == 0)
                return null;

            string name = string.Join("-", mbNameParts);

            if (IsAYWMotherboard(info))
            {
                return $"https://asus.com/motherboards-components/motherboards/others/{name}";
            }

            if (IsProArtMotherboard(info))
            {
                return $"https://asus.com/motherboards-components/motherboards/proart/{name}";
            }

            if (!ContainsAny(info.MbName, "X870", "B850", "B840"))
            {
                name = $"{name}-model";
            }

            if (mbNameParts.Length < 2)
                return $"https://rog.asus.com/motherboards/{mbNameParts[0]}/{name}";
            try
            {
                string series = $"{mbNameParts[0]}-{mbNameParts[1]}";
                return $"https://rog.asus.com/motherboards/{series}/{name}";
            }
            catch
            {
                return $"https://asus.com/motherboards-components/motherboards/";
            }
        }

        internal static string GetMotherboardLogo(SystemInfo info)
        {
            if (info == null)
                return null;

            if (IsRogMotherboard(info)) return "rogLogo";
            if (IsAYWMotherboard(info)) return "aywLogo";
            if (IsProArtMotherboard(info)) return "proartLogo";
            if (IsMegMotherboard(info)) return "megLogo";
            if (IsMagMotherboard(info)) return "magLogo";
            if (IsMpgMotherboard(info)) return "mpgLogo";
            if (IsMsiMotherboard(info)) return "msiLogo";
            if (IsMsiProMotherboard(info)) return "msiProLogo";

            return null;
        }

        internal static string GetMemoryModuleLogo(MemoryModule module)
        {
            if (module == null)
                return null;

            if (IsGSkillModule(module)) return "gskillLogo";
            if (IsOriginCodeModule(module)) return "originCodeLogo";
            if (IsBiwinModule(module)) return "biwinLogo";
            if (IsColorfulModule(module) && IsIgameModule(module)) return "igameLogo";

            return null;
        }

        internal static bool IsGSkillModule(MemoryModule module)
        {
            if (module == null)
                return false;

            string manufacturer = module.Manufacturer?.Trim() ?? string.Empty;
            string partNumber = module.PartNumber?.Trim() ?? string.Empty;

            return Contains(manufacturer, "skill")
                || StartsWith(partNumber, "f5-")
                || StartsWith(partNumber, "f4-");
        }

        internal static bool IsBiwinModule(MemoryModule module)
        {
            if (module == null)
                return false;

            return Contains(module.Manufacturer, "biwin");
        }

        internal static bool IsOriginCodeModule(MemoryModule module)
        {
            if (module == null)
                return false;

            return Contains(module.Manufacturer, "origin");
        }

        internal static bool IsColorfulModule(MemoryModule module)
        {
            if (module == null)
                return false;

            return Contains(module.Manufacturer, "colorful");
        }

        internal static bool IsIgameModule(MemoryModule module)
        {
            if (module == null)
                return false;

            return StartsWith(module.PartNumber, "ig");
        }

        internal static string GetCpuNameString(SystemInfo info)
        {
            if (info == null)
                return "Error getting CPU name";

            try
            {
                var name = info.CpuName ?? string.Empty;

                if (Contains(name, "Eng Sample"))
                {
                    return $"{name} | {info.CodeName} | 0x{info.CpuId:X6}";
                }

                return name;
            }
            catch
            {
                return "Error getting CPU name";
            }
        }

        private static bool IsAsusMotherboard(SystemInfo info) =>
            Contains(info?.MbVendor, "asus");

        private static bool IsMsiMotherboard(SystemInfo info) =>
            StartsWith(info?.MbVendor, "msi") ||
            StartsWith(info?.MbVendor, "Micro Star") ||
            StartsWith(info?.MbVendor, "Microstar") ||
            StartsWith(info?.MbVendor, "Micro-Star");

        private static bool IsMegMotherboard(SystemInfo info) =>
            IsMsiMotherboard(info) &&
            StartsWith(info?.MbName, "meg");

        private static bool IsMagMotherboard(SystemInfo info) =>
            IsMsiMotherboard(info) &&
            StartsWith(info?.MbName, "mag");

        private static bool IsMpgMotherboard(SystemInfo info) =>
            IsMsiMotherboard(info) &&
            StartsWith(info?.MbName, "mpg");

        private static bool IsMsiProMotherboard(SystemInfo info) =>
            IsMsiMotherboard(info) &&
            StartsWith(info?.MbName, "pro");

        private static bool Contains(string source, string value) =>
            !string.IsNullOrEmpty(source) &&
            source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;

        private static bool StartsWith(string source, string value) =>
            !string.IsNullOrEmpty(source) &&
            source.StartsWith(value, StringComparison.OrdinalIgnoreCase);

        private static bool ContainsAny(string source, params string[] values)
        {
            if (string.IsNullOrEmpty(source) || values == null)
                return false;

            foreach (var value in values)
            {
                if (Contains(source, value))
                    return true;
            }

            return false;
        }
    }
}