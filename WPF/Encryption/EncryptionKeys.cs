using System;
using System.IO;
using System.Security.Cryptography;

namespace ZenTimings.Encryption
{
    internal class EncryptionKeys
    {
        private readonly string KeyFilePath = "key.bin";
        private readonly string IVFilePath = "iv.bin";

        public byte[] Key { get; private set; }
        public byte[] IV { get; private set; }

        public EncryptionKeys()
        {
            LoadOrGenerateKeys();
        }

        private void LoadOrGenerateKeys()
        {
            if (File.Exists(KeyFilePath) && File.Exists(IVFilePath))
            {
                Key = File.ReadAllBytes(KeyFilePath);
                IV = File.ReadAllBytes(IVFilePath);
            }
            else
            {
                using (Aes aes = Aes.Create())
                {
                    aes.GenerateKey();
                    aes.GenerateIV();

                    Key = aes.Key;
                    IV = aes.IV;

                    File.WriteAllBytes(KeyFilePath, Key);
                    File.WriteAllBytes(IVFilePath, IV);

                    Console.WriteLine("Encryption keys generated and stored.");
                }
            }
        }
    }
}
