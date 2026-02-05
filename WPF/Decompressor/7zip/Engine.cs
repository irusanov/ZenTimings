using SevenZip.Compression.LZMA;
using System;
using System.IO;

namespace ZenStates.Core
{
    public static class Engine
    {

        static void Decompress(Stream inputStream, Stream outputStream)
        {
            var decoder = new Decoder();

            var properties = new byte[5];

            // Read the decoder properties
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
        inputStream.Read((Span<byte>)properties);
#else
            inputStream.Read(properties, 0, 5);
#endif
            decoder.SetDecoderProperties(properties);

            // Read in the decompress file size.
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
        Span<byte> fileLengthBytes = stackalloc byte[8];
        inputStream.Read(fileLengthBytes);
        var fileLength = BitConverter.ToInt64((ReadOnlySpan<byte>)fileLengthBytes);
#else
            var fileLengthBytes = new byte[8];
            inputStream.Read(fileLengthBytes, 0, 8);
            var fileLength = BitConverter.ToInt64(fileLengthBytes, 0);
#endif

            // Decode
            decoder.Code(inputStream, outputStream, inputStream.Length, fileLength, null);

            outputStream.Flush(); //It's needed because of FileStream internal buffering
        }
    }
}