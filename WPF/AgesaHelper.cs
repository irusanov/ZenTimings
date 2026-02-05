using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using ZenStates.Core;
using ZenTimings.Decompressor;

namespace ZenTimings
{
    internal static class AgesaHelper
    {
        private const int HeaderSize = 0x18;
        private const uint StartAddress = 0x44000000;
        private const uint EndAddress = 0x45BB0000;
        private static readonly bool[] Allowed = BuildAllowedTable();

        /// <summary>
        /// Dumps the BIOS image from flash memory.
        /// </summary>
        public static byte[] DumpImage()
        {
            byte[] image = new byte[EndAddress - StartAddress];

            try
            {
                Thread thread = new Thread(() =>
                {
                    for (uint i = 0; i < image.Length - 4; i += 4)
                    {
                        uint dataChunk = 0xFFFFFFFF;
                        if (Mutexes.WaitPciBus(10))
                        {
                            try
                            {
                                bool ok = CpuSingleton.Instance.ReadDwordEx(StartAddress + i, ref dataChunk);
                                if (!ok)
                                    dataChunk = 0xFFFFFFFF;
                                Buffer.BlockCopy(BitConverter.GetBytes(dataChunk), 0, image, (int)i, 4);
                            }
                            finally
                            {
                                Mutexes.ReleasePciBus();
                            }
                        }
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

            if (image == null || image.Length == 0)
                return AGESA_UNKNOWN;

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

        public static string FindAgesaVersionInMemory()
        {
            string agesaVersion = "";
            try
            {
                var CHUNK_SIZE = 4096;

                for (var i = 0x900000; i < 0x9FFFFFF; i += CHUNK_SIZE)
                {
                    var chunkData = CpuSingleton.Instance.io.ReadMemory(new IntPtr(i), CHUNK_SIZE);

                    byte[] testSequence = System.Text.Encoding.ASCII.GetBytes("AGESA!V9");
                    int targetOffset = Utils.FindSequence(chunkData, 0, testSequence);
                    if (targetOffset != -1)
                    {
                        targetOffset += testSequence.Length;
                        Debug.WriteLine($"Found target sequence at offset 0x{targetOffset:X} in chunk starting at 0x{i:X}");
                        // Find the end of the string (null-terminated sequence)
                        int endPos = Utils.FindSequence(chunkData, targetOffset, new byte[] { 0x00, 0x00 });
                        if (endPos > targetOffset)
                        {
                            agesaVersion = Encoding.ASCII.GetString(chunkData, targetOffset, endPos - targetOffset).Trim('\0').Trim();
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Could not find AGESA version: {ex.Message}");
            }

            return agesaVersion;
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
