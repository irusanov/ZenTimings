using System;
using System.Text;
using System.Threading;
using ZenStates.Core;
using ZenTimings.Decompressor;

namespace ZenTimings
{
    internal static class AgesaHelper
    {
        public const int HeaderSize = 0x18;

        public static byte[] DumpImage()
        {
            uint start = 0x45000000;
            uint end = 0x45BB0000;
            byte[] image = new byte[end - start];
            uint dataChunk = 0;

            try
            {
                Thread thread = new Thread(() =>
                {
                    for (uint i = 0; i < image.Length - 4; i += 4)
                    {
                        CpuSingleton.Instance.ReadDwordEx(start + i, ref dataChunk);
                        var buf = BitConverter.GetBytes(dataChunk);
                        Buffer.BlockCopy(buf, 0, image, (int)i, buf.Length);
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
                Console.WriteLine($"Error: {ex.Message}");
            }

            return image;
        }

        public static string FindAgesaVersion(byte[] image)
        {
            string agesaVersion = AppSettings.AGESA_UNKNOWN;

            try
            {
                int fileOffset = -1;

                byte[] fileGuid = new Guid("9E21FD93-9C72-4C15-8C4B-E77F1DB2D792").ToByteArray();

                // Step 1: Find the GUID in the BIOS data
                fileOffset = Utils.FindSequence(image, 0, fileGuid);
                if (fileOffset != -1)
                {
                    byte[] buffer = new byte[4];
                    Buffer.BlockCopy(image, fileOffset + HeaderSize, buffer, 0, 3);
                    int compressedSize = BitConverter.ToInt32(buffer, 0);
                    //int decompressedSize = BitConverter.ToInt32(image, fileOffset + 0x35);

                    // Start of lzma block
                    fileOffset += 0x30;
                    byte[] body = new byte[compressedSize];
                    Buffer.BlockCopy(image, fileOffset, body, 0, compressedSize);

                    byte[] decompressedData = LZMACompressor.Decompress(body);

                    byte[] targetSequence = Encoding.ASCII.GetBytes("AGESA!V9");
                    int targetOffset = Utils.FindSequence(decompressedData, 0, targetSequence);
                    if (targetOffset != -1)
                    {
                        targetOffset += targetSequence.Length;
                        Console.WriteLine($"Found target sequence at offset 0x{targetOffset:X}");
                        // Find the end of the string (null-terminated sequence)
                        int endPos = Utils.FindSequence(decompressedData, targetOffset, new byte[] { 0x00, 0x00 });
                        if (endPos > targetOffset)
                        {
                            agesaVersion = Encoding.ASCII.GetString(decompressedData, targetOffset, endPos - targetOffset).Trim('\0').Trim();
                        }
                    }
                    else
                    {
                        Console.WriteLine("Target sequence not found.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            return agesaVersion;
        }
    }
}
