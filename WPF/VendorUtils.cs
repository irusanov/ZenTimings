using ZenStates.Core;
using ZenStates.Core.DRAM;

namespace ZenTimings
{
    internal class VendorUtils
    {
        internal static bool IsRogMotherboard(SystemInfo info)
        {
            return info.MbVendor.ToLower().Contains("asus") && info.MbName.ToLower().StartsWith("rog");
        }
        internal static bool IsAYWMotherboard(SystemInfo info)
        {
            return info.MbVendor.ToLower().Contains("asus") && info.MbName.ToLower().Contains("ayw");
        }

        internal static bool IsProArtMotherboard(SystemInfo info)
        {
            return info.MbVendor.ToLower().Contains("asus") && info.MbName.ToLower().Contains("proart");
        }

        internal static string GetMotherboardLink(SystemInfo info)
        {
            if (!IsRogMotherboard(info) && !IsAYWMotherboard(info))
                return null;

            string[] mbNameParts = info.MbName.ToLower().Split(' ');
            string name = string.Join("-", mbNameParts);

            if (IsAYWMotherboard(info))
            {
                return $"https://www.asus.com/motherboards-components/motherboards/others/{name}";
            }

            if (!(info.MbName.Contains("X870") || info.MbName.Contains("B850") || info.MbName.Contains("B840")))
            {
                name = $"{name}-model";
            }

            string series = $"{mbNameParts[0]}-{mbNameParts[1]}";

            return $"https://rog.asus.com/motherboards/{series}/{name}";
        }

        internal static string GetMotherboardLogo(SystemInfo systemInfo)
        {
            if (IsRogMotherboard(systemInfo)) return "rogLogo";
            if (IsAYWMotherboard(systemInfo)) return "aywLogo";
            if (IsProArtMotherboard(systemInfo)) return "proartLogo";
            //if (IsMegMotherboard(systemInfo)) return "megLogo";

            return null;
        }

        private static bool IsMegMotherboard(SystemInfo info)
        {
            return (info.MbVendor.ToLower().StartsWith("msi") || info.MbVendor.StartsWith("Microstar International")) && info.MbName.ToLower().StartsWith("meg");
        }

        internal static string GetMemoryModuleLogo(MemoryModule module)
        {
            if (IsGSkillModule(module))
            {
                return "gskillLogo";
            }
            else if (IsBiwinModule(module) && IsOriginCodeModule(module))
            {
                return "originCodeLogo";
            }
            else if (IsBiwinModule(module))
            {
                return "biwinLogo";
            }
            else if (IsColorfulModule(module))
            {
                return "igameLogo";
            }

            return null;
        }

        internal static bool IsGSkillModule(MemoryModule module)
        {
            return module.Manufacturer.ToLowerInvariant().Contains("skill")
                || module.PartNumber.ToLowerInvariant().Trim().StartsWith("f5-")
                || module.PartNumber.ToLowerInvariant().Trim().StartsWith("f4-");
        }

        internal static bool IsBiwinModule(MemoryModule module)
        {
            return module.Manufacturer.ToLowerInvariant().Contains("biwin") && 
                ( module.PartNumber.ToLowerInvariant().Trim().StartsWith("bm")
                || module.PartNumber.ToLowerInvariant().Trim().StartsWith("bx")
                || module.PartNumber.ToLowerInvariant().Trim().StartsWith("ba")
                || module.PartNumber.ToLowerInvariant().Trim().StartsWith("ocl")
                || module.PartNumber.ToLowerInvariant().Trim().StartsWith("ocb"));
        }

        internal static bool IsOriginCodeModule(MemoryModule module)
        {
            return module.PartNumber.ToLowerInvariant().Trim().StartsWith("ocl")
                || module.PartNumber.ToLowerInvariant().Trim().StartsWith("ocb");
        }

        internal static bool IsColorfulModule(MemoryModule module)
        {
            //return module.Manufacturer.ToLowerInvariant().Contains("colorful")
            //    && module.PartNumber.ToLowerInvariant().Trim().StartsWith("ig");
            return module.PartNumber.ToLowerInvariant().Trim().StartsWith("ig");
        }

        internal static string GetCpuNameString(SystemInfo info)
        {
            try
            {
                var name = info.CpuName;
                if (name.Contains("Eng Sample"))
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
    }
}
