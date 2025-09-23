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
            if (IsRogMotherboard(systemInfo))
            {
                return "ROGLogo";
            }
            else if (IsAYWMotherboard(systemInfo))
            {
                return "aywLogo";
            }

            return null;
        }

        internal static string GetMemoryModuleLogo(MemoryModule module)
        {
            if (IsGSkillModule(module))
            {
                return "GskillLogo";
            }
            else if (IsBiwinModule(module))
            {
                return "biwinLogo";
            }

            return null;
        }

        internal static bool IsGSkillModule(MemoryModule module)
        {
            return module.Manufacturer.ToLowerInvariant().Contains("skill")
                || module.PartNumber.ToLowerInvariant().StartsWith("f5-")
                || module.PartNumber.ToLowerInvariant().StartsWith("f4-");
        }

        internal static bool IsBiwinModule(MemoryModule module)
        {
            return module.Manufacturer.ToLowerInvariant().Contains("biwin")
                || module.PartNumber.ToLowerInvariant().StartsWith("bm")
                || module.PartNumber.ToLowerInvariant().StartsWith("bx")
                || module.PartNumber.ToLowerInvariant().StartsWith("ba")
                || module.PartNumber.ToLowerInvariant().StartsWith("ocl")
                || module.PartNumber.ToLowerInvariant().StartsWith("ocb");
        }
    }
}
