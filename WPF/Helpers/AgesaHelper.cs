using System;
using System.Diagnostics;
using ZenStates.Core;

namespace ZenTimings.Helpers
{
    internal static class AgesaHelper
    {
        public static string FindAgesaVersionInMemory()
        {
            try
            {
                var CHUNK_SIZE = 1024 * 256;

                for (var i = 0x9000000; i < 0x9FFFFFF; i += CHUNK_SIZE)
                {
                    var chunkData = CpuSingleton.Instance.io.ReadMemory(new IntPtr(i), CHUNK_SIZE);
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
