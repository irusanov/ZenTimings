using System;
using System.Linq;
using System.Text;
using System.Threading;
using ZenStates.Core;
using ZenTimings.Decompressor;

namespace ZenTimings
{
    internal static class AgesaHelper
    {
        private const int HeaderSize = 0x18;
        private const uint StartAddress = 0x45000000;
        private const uint EndAddress = 0x45BB0000;
        private static readonly bool[] Allowed = BuildAllowedTable();

        /// <summary>
        /// Dumps the BIOS image from memory.
        /// </summary>
        public static byte[] DumpImage()
        {
            byte[] image = new byte[EndAddress - StartAddress];

            try
            {
                Thread thread = new Thread(() =>
                {
                    for (uint i = 0; i < image.Length; i += 4)
                    {
                        uint dataChunk = 0;
                        CpuSingleton.Instance.ReadDwordEx(StartAddress + i, ref dataChunk);
                        BitConverter.GetBytes(dataChunk).CopyTo(image, i);
                    }
                })
                {
                    IsBackground = true
                };
                thread.Start();
                thread.Join();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DumpImage error: {ex.Message}");
            }

            return image;
        }

        /// <summary>
        /// Finds the AGESA version string in a BIOS image.
        /// </summary>
        public static string FindAgesaVersion(byte[] image)
        {
            const string AGESA_UNKNOWN = AppSettings.AGESA_UNKNOWN;
            try
            {
                // Find the GUID in the BIOS data
                byte[] fileGuid = new Guid("9E21FD93-9C72-4C15-8C4B-E77F1DB2D792").ToByteArray();
                int fileOffset = Utils.FindSequence(image, 0, fileGuid);
                if (fileOffset == -1) return AGESA_UNKNOWN;

                // Read compressed size
                int compressedSize = BitConverter.ToInt32(image, fileOffset + HeaderSize) & 0xFFFFFF;
                //int decompressedSize = BitConverter.ToInt32(image, fileOffset + 0x35) & 0xFFFFFF;
                // Start of lzma block
                int lzmaStart = fileOffset + 0x30;

                byte[] compressedData = new byte[compressedSize];
                Array.Copy(image, lzmaStart, compressedData, 0, compressedSize);

                byte[] decompressedData = LZMACompressor.Decompress(compressedData);

                // Search for AGESA marker
                byte[] marker = Encoding.ASCII.GetBytes("AGESA!V9");
                int markerOffset = Utils.FindSequence(decompressedData, 0, marker);
                if (markerOffset == -1)
                {
                    Console.WriteLine("AGESA marker not found.");
                    return AGESA_UNKNOWN;
                }

                int versionStart = markerOffset + marker.Length;
                versionStart = FindFirstAllowed(decompressedData, versionStart);
                int versionEnd = FindFirstInvalid(decompressedData, versionStart);

                if (versionEnd > versionStart)
                {
                    return Encoding.ASCII.GetString(decompressedData, versionStart, versionEnd - versionStart)
                        .Trim('\0', ' ');
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FindAgesaVersion error: {ex.Message}");
            }

            return AGESA_UNKNOWN;
        }

        private static int FindFirstInvalid(byte[] data, int startIndex = 0)
        {
            for (int i = startIndex; i < data.Length; i++)
            {
                if (!Allowed[data[i]])
                    return i;
            }
            return data.Length;
        }

        private static int FindFirstAllowed(byte[] data, int startIndex = 0)
        {
            for (int i = startIndex; i < data.Length; i++)
            {
                if (Allowed[data[i]])
                    return i;
            }
            return -1;
        }

        private static bool[] BuildAllowedTable()
        {
            var table = new bool[256];

            for (int c = '0'; c <= '9'; c++) table[c] = true;
            for (int c = 'A'; c <= 'Z'; c++) table[c] = true;
            for (int c = 'a'; c <= 'z'; c++) table[c] = true;

            table[' '] = true;
            table['.'] = true;
            table['-'] = true;

            return table;
        }
    }
}
