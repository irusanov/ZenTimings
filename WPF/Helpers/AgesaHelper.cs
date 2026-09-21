using System;
using System.Diagnostics;
using ZenStates.Core;
using ZenTimings.Common;
using ZenTimings.Settings;

namespace ZenTimings.Helpers
{
    internal static class AgesaHelper
    {
        public static string FindAgesaVersionInMemory()
        {
            try
            {
                var io = CpuSingleton.Instance?.io;
                if (io == null)
                    return AppSettings.AGESA_UNKNOWN;

                var CHUNK_SIZE = 1024 * 256;

                for (var i = 0x9000000; i < 0x9FFFFFF; i += CHUNK_SIZE)
                {
                    var chunkData = io.ReadMemory(new IntPtr(i), CHUNK_SIZE);
                    var version = AgesaUtils.ParseVersion(chunkData);
                    if (!String.IsNullOrEmpty(version) && version != AppSettings.AGESA_UNKNOWN)
                    {
                        return version;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Could not find AGESA version: {ex.Message}");
            }

            return AppSettings.AGESA_UNKNOWN;
        }
    }
}
