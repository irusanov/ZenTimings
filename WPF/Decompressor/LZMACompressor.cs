using System;
using System.IO;

namespace ZenTimings.Decompressor
{
    internal static class LZMACompressor
    {
        public static byte[] Decompress(byte[] compressedBytes)
        {
            using (var inputStream = new MemoryStream(compressedBytes))
            using (var outputStream = new MemoryStream())
            {
                Decompress(inputStream, outputStream);
                return outputStream.ToArray();
            }
        }

        public static void Decompress(MemoryStream inputStream, MemoryStream outputStream)
        {
            var decoder = new SevenZip.Compression.LZMA.Decoder();
            var properties = new byte[5];

            inputStream.Read(properties, 0, 5);
            decoder.SetDecoderProperties(properties);

            var fileLengthBytes = new byte[8];
            inputStream.Read(fileLengthBytes, 0, 8);
            var fileLength = BitConverter.ToInt64(fileLengthBytes, 0);

            decoder.Code(inputStream, outputStream, inputStream.Length, fileLength, null);

            outputStream.Flush();
        }
    }
}
