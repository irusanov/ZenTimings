using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using ZenStates.Core.Hardware;
using ZenStates.Core.Hardware.DRAM;

namespace ZenTimings.Utils
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

        internal static bool IsTufMotherboard(SystemInfo info) =>
            IsAsusMotherboard(info) &&
            StartsWith(info?.MbVendor, "tuf");

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

            if (IsTufMotherboard(info))
            {
                return $"https://asus.com/motherboards-components/motherboards/tuf-gaming/{name}";
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
            if (info == null || string.IsNullOrWhiteSpace(info.MbName))
                return null;

            var mbName = info.MbName.Trim().ToLowerInvariant();

            if (IsAsusMotherboard(info))
            {
                if (IsRogMotherboard(info)) return "rogLogo";
                if (IsAYWMotherboard(info)) return "aywLogo";
                if (IsProArtMotherboard(info)) return "proartLogo";
                if (IsTufMotherboard(info)) return "tufLogo";
                return null;
            }
            else if (IsMsiMotherboard(info))
            {
                if (Contains(mbName, "mpower") || Contains(mbName, "gaming")) return "msiDragonLogo";
                if (StartsWith(mbName, "meg")) return "megLogo";
                if (StartsWith(mbName, "mag")) return "magLogo";
                if (StartsWith(mbName, "mpg")) return "mpgLogo";
                if (StartsWith(mbName, "pro")) return "msiProLogo";
                return "msiLogo";
            }

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

        internal static string GetDramDieName(string manufacturer, int stepping)
        {
            if (string.IsNullOrEmpty(manufacturer))
                return null;

            // SPD byte 0x22A: bits [7:4] = die family?, bits [3:0] = minor revision
            int dieFamilyNibble = (stepping >> 4) & 0xF;

            if (Contains(manufacturer, "hynix"))
            {
                int dieIndex = stepping & 0xF;
                if (dieFamilyNibble == 4 && dieIndex >= 1 && dieIndex <= 15)
                    return $"{(char)('A' + dieIndex - 1)}-Die";
            }

            return $"(Unknown 0x{stepping:X2})";
        }

        internal static string GetCpuNameString(SystemInfo info)
        {
            if (info == null)
                return "Error getting CPU name";

            try
            {
                var name = info.CpuName ?? string.Empty;

                if (name.Contains("Eng Sample"))
                {
                    name = name.ToLower().Replace("amd eng sample", "AMD ES").Replace("eng sample", "AMD ES");
                    return $"{name} | {info.CodeName} | 0x{info.CpuId:X6}";
                }

                return name;
            }
            catch
            {
                return "Error getting CPU name";
            }
        }

        // Brands whose JEP106 / SMBIOS name reads badly when shortened ("G Skill Intl", "G.SKILL" -> "G.Skill"),
        // matched at the start of the name with spaces and punctuation removed
        private static readonly KeyValuePair<string, string>[] MemoryVendorBrands =
        {
            new KeyValuePair<string, string>("gskill", "G.Skill"),
            new KeyValuePair<string, string>("origincode", "Origin Code"),
            new KeyValuePair<string, string>("biwin", "BIWIN"),
            new KeyValuePair<string, string>("colorful", "Colorful"),
            new KeyValuePair<string, string>("teamgroup", "TeamGroup"),
            new KeyValuePair<string, string>("patriotmemory", "Patriot"),
        };

        // Manufacturer strings that name no vendor: the JEP106 lookup's "Unknown (Bank n, Code 0xnn)" and SMBIOS fillers
        private static readonly string[] VendorPlaceholders =
        {
            "Unknown", "Undefined", "Invalid", "Not Specified", "Not Available", "To Be Filled", "Manufacturer", "N/A",
        };

        // Legal suffixes of JEP106 names ("Rambus Inc", "Netac Technology Co Ltd"), removed until none is left
        private static readonly string[] CompanySuffixes =
        {
            " Inc.", " Inc", " Corporation", " Corp.", " Corp", " Co., Ltd.", " Co. Ltd.", " Co., Ltd", " Ltd.", " Ltd",
            " Co.", " Co", " Limited", " LLC", " GmbH", " AG", " S.A.", " B.V.", " Technology Group", " Technologies",
            " Technology", " Electronics", " Semiconductor", " Power"
        };

        /// <summary>
        /// The vendor name to show before a module's part number: <see cref="GetMemoryVendorName"/> of its manufacturer,
        /// or null when that names no vendor or the part number already starts with it ("Corsair CMK32GX5M2B5600C36").
        /// </summary>
        internal static string GetModuleVendorPrefix(MemoryModule module)
        {
            string vendor = GetMemoryVendorName(module?.Manufacturer);
            if (vendor == null)
                return null;

            string partNumber = module.PartNumber?.Trim() ?? string.Empty;
            return partNumber.StartsWith(vendor, StringComparison.OrdinalIgnoreCase) ? null : vendor;
        }

        /// <summary>
        /// A short vendor name for a module's manufacturer (from SMBIOS, or the JEP106 name from its SPD): the brand of a
        /// known vendor, otherwise the name without "(...)" and company suffixes. Null when it names no vendor.
        /// </summary>
        internal static string GetMemoryVendorName(string manufacturer)
        {
            if (string.IsNullOrWhiteSpace(manufacturer))
                return null;

            string name = manufacturer.Trim();
            if (IsVendorPlaceholder(name))
                return null;

            string key = LettersAndDigits(name);
            foreach (var brand in MemoryVendorBrands)
            {
                if (key.StartsWith(brand.Key, StringComparison.Ordinal))
                    return brand.Value;
            }

            // "Biwin Semiconductor (HK) Co Ltd" -> "Biwin Semiconductor Co Ltd"
            name = Regex.Replace(name, @"\s*\([^)]*\)", string.Empty).Trim();
            name = TrimCompanySuffixes(name);
            return name.Length > 0 ? name : null;
        }

        /// <summary>The name without trailing legal suffixes, e.g. "Micron Technology" -> "Micron".</summary>
        internal static string TrimCompanySuffixes(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            string result = name.Trim().TrimEnd(',');
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (string suffix in CompanySuffixes)
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

        private static bool IsVendorPlaceholder(string name)
        {
            foreach (string placeholder in VendorPlaceholders)
            {
                if (StartsWith(name, placeholder))
                    return true;
            }

            // Some BIOSes report the raw JEP106 ID, e.g. "0x04CD" or "04CD000000000000"
            string id = StartsWith(name, "0x") ? name.Substring(2) : name;
            if (id.Length < 4)
                return false;

            foreach (char c in id)
            {
                if (!Uri.IsHexDigit(c))
                    return false;
            }

            return true;
        }

        private static string LettersAndDigits(string value)
        {
            var sb = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                if (char.IsLetterOrDigit(c))
                    sb.Append(char.ToLowerInvariant(c));
            }

            return sb.ToString();
        }

        private static bool IsAsusMotherboard(SystemInfo info) =>
            Contains(info?.MbVendor, "asus");

        private static bool IsMsiMotherboard(SystemInfo info) => ContainsAny(info?.MbVendor, "msi", "Micro Star", "Microstar", "Micro-Star");
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