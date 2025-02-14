// Ignore Spelling: lzma

using SevenZip.Compression.LZMA;
using System;
using System.IO;

[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S4136:Method overloads should be grouped together")]

namespace ZenStates.Core
{
    /// <summary>
    /// LZMA compressor
    /// </summary>
    /// <remarks>
    /// https://gist.github.com/ststeiger/cb9750664952f775a341
    /// </remarks>
#pragma warning disable S101 // Types should be named in PascalCase
    public class LZMACompressor : BaseCompressor
#pragma warning restore S101 // Types should be named in PascalCase
    {
        private DictionarySize dictionarySize = DictionarySize.Smallest_64KB;
        private LZMACompressionLevel compressionLevel = LZMACompressionLevel.UltraFast;

        /// <summary>
        /// Provides a default shared (thread-safe) instance.
        /// </summary>
        public static LZMACompressor Shared { get; } = new LZMACompressor(name: "shared");

        /// <inheritdoc/>
        public override CompressionMethod Method => CompressionMethod.LZMA;

        /// <summary>
        /// Gets or sets the compression level.
        /// </summary>
        /// <value>
        /// The compression level.
        /// </value>
        public LZMACompressionLevel CompressionLevel
        {
            get => compressionLevel;
            set
            {
                compressionLevel = value;
                Properties = LZMAProperties.GetProperties(compressionLevel, dictionarySize);
            }
        }

        /// <summary>
        /// Gets or sets the size of the dictionary.
        /// </summary>
        /// <value>
        /// The size of the dictionary.
        /// </value>
        public DictionarySize DictionarySize
        {
            get => dictionarySize;
            set
            {
                dictionarySize = value;
                Properties = LZMAProperties.GetProperties(compressionLevel, dictionarySize);
            }
        }

        /// <summary>
        /// The properties
        /// </summary>
        protected object[] Properties { get; set; }

        #region Constructors
        /// <summary>
        /// Initializes a new instance of the <see cref="LZMACompressor"/> class.
        /// </summary>
        public LZMACompressor()
            : this(name: null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LZMACompressor"/> class.
        /// </summary>
        /// <param name="name">The name.</param>
        public LZMACompressor(string name)
            : this(name, LZMACompressionLevel.UltraFast)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LZMACompressor"/> class.
        /// </summary>
        /// <param name="compressionLevel">The compression level. (Defaults to <see cref="LZMACompressionLevel.UltraFast"/></param>
        public LZMACompressor(LZMACompressionLevel compressionLevel)
            : this(name: null, compressionLevel)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LZMACompressor"/> class.
        /// </summary>
        /// <param name="compressionLevel">The compression level. (Defaults to <see cref="LZMACompressionLevel.UltraFast"/></param>
        /// <param name="dictionarySize">Size of the dictionary. (Defaults to <see cref="DictionarySize.Smallest_64KB"/> <c>(64 KB)</c> )</param>
        public LZMACompressor(LZMACompressionLevel compressionLevel, DictionarySize dictionarySize)
            : this(name: null, compressionLevel, dictionarySize)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LZMACompressor"/> class.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="compressionLevel">The compression level. (Defaults to <see cref="LZMACompressionLevel.UltraFast"/></param>
        public LZMACompressor(string name, LZMACompressionLevel compressionLevel)
#pragma warning disable RCS1196 // Call extension method as instance method
            : this(name, compressionLevel, LZMAProperties.GetDictionarySize(compressionLevel))
#pragma warning restore RCS1196 // Call extension method as instance method
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LZMACompressor"/> class.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="compressionLevel">The compression level. (Defaults to <see cref="LZMACompressionLevel.UltraFast"/></param>
        /// <param name="dictionarySize">Size of the dictionary. (Defaults to <see cref="DictionarySize.Smallest_64KB"/> <c>(64 KB)</c> )</param>
        public LZMACompressor(string name, LZMACompressionLevel compressionLevel, DictionarySize dictionarySize)
        {
            Name = name;
            CompressionLevel = compressionLevel;
            DictionarySize = dictionarySize;
        }
        #endregion

        /// <inheritdoc/>
        protected override byte[] BaseCompress(byte[] bytes)
        {
            using (var inputStream = new MemoryStream(bytes))
            using (var outputStream = new MemoryStream())
            {
                BaseCompress(inputStream, outputStream);
                return outputStream.ToArray();
            }
        }

        /// <inheritdoc/>
        protected override byte[] BaseDecompress(byte[] compressedBytes)
        {
            using (var inputStream = new MemoryStream(compressedBytes))
            using (var outputStream = new MemoryStream())
            {
                BaseDecompress(inputStream, outputStream);
                return outputStream.ToArray();
            }
        }

        /// <inheritdoc/>
        protected override void BaseCompress(Stream inputStream, Stream outputStream)
        {
            var encoder = new Encoder();

            encoder.SetCoderProperties(LZMAProperties.PropIDs, Properties);

            // Write the encoder properties
            encoder.WriteCoderProperties(outputStream);

            var fileSize = BitConverter.GetBytes(inputStream.Length);
            // Write the decompressed file size.
            outputStream.Write(fileSize, 0, fileSize.Length);

            // Encode
            encoder.Code(inputStream, outputStream, inputStream.Length, -1, null);

            outputStream.Flush(); //It's needed because of FileStream internal buffering
        }

        /// <inheritdoc/>
        protected override void BaseDecompress(Stream inputStream, Stream outputStream)
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

        /// <inheritdoc/>
        public override string ToString()
        {
            return Name ?? $"{GetType().Name}(Level:{CompressionLevel}, DictionarySize:{DictionarySize})";
        }
    }
}