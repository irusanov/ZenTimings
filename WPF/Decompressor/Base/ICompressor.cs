using System.IO;

namespace ZenStates.Core
{
    /// <summary>
    /// ICompressor
    /// </summary>
    public interface ICompressor
    {
        /// <summary>
        /// Compressor name
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Compression method
        /// </summary>
        CompressionMethod Method { get; }

        /// <summary>
        /// Compress bytes
        /// </summary>
        /// <param name="bytes">Bytes</param>
        /// <returns>Returns compressed bytes</returns>
        byte[] Compress(byte[] bytes);

        /// <summary>
        /// Decompress bytes
        /// </summary>
        /// <param name="compressedBytes">Compressed bytes</param>
        /// <returns>Returns uncompressed bytes</returns>
        byte[] Decompress(byte[] compressedBytes);

        /// <summary>
        /// Compress input stream into output stream
        /// </summary>
        /// <param name="inputStream">Input stream</param>
        /// <param name="outputStream">Output stream</param>
        void Compress(Stream inputStream, Stream outputStream);

        /// <summary>
        /// Decompress input stream into output stream
        /// </summary>
        /// <param name="inputStream">Input stream</param>
        /// <param name="outputStream">Output stream</param>
        void Decompress(Stream inputStream, Stream outputStream);
    }
}