using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ZenTimings.Encryption
{
    internal class AesEncryption
    {
        private readonly EncryptionKeys encryptionKeys;

        public AesEncryption()
        {
            encryptionKeys = new EncryptionKeys();
        }

        public void EncryptXmlFile<T>(T obj, string outputFile)
        {
            string xmlContent = XmlUtils.SerializeToXml(obj);
            byte[] encryptedData = EncryptString(xmlContent);
            File.WriteAllBytes(outputFile, encryptedData);
            Debug.WriteLine("XML file encrypted successfully!");
        }

        public T DecryptXmlInMemory<T>(string inputFile)
        {
            byte[] encryptedData = File.ReadAllBytes(inputFile);
            string decryptedXml = DecryptString(encryptedData);
            return XmlUtils.DeserializeFromXmlString<T>(decryptedXml);
        }

        /// <summary>
        /// Encrypts <paramref name="plainText"/> with AES using the shared key, under a fresh
        /// random IV that is prepended to the returned ciphertext.
        ///
        /// A previous version of this method reused one fixed IV (persisted alongside the key)
        /// for every message it ever encrypted. With AES-CBC, encrypting more than one message
        /// under the same key+IV pair is a real weakness: identical plaintext prefixes produce
        /// identical ciphertext prefixes, and it makes the whole scheme deterministic instead of
        /// semantically secure. The IV itself isn't secret - it only needs to be unpredictable
        /// and never reused with the same key - so generating one per message and shipping it
        /// alongside the ciphertext (rather than reusing a stored one) is the standard fix.
        /// </summary>
        public byte[] EncryptString(string plainText)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = encryptionKeys.Key;
                aes.GenerateIV();

                using (MemoryStream ms = new MemoryStream())
                {
                    // Prepend the IV so DecryptString can recover it later.
                    ms.Write(aes.IV, 0, aes.IV.Length);

                    using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        byte[] bytes = Encoding.UTF8.GetBytes(plainText);
                        cs.Write(bytes, 0, bytes.Length);
                        cs.FlushFinalBlock();
                    }

                    return ms.ToArray();
                }
            }
        }

        public string DecryptString(byte[] cipherText)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = encryptionKeys.Key;

                int ivLength = aes.BlockSize / 8;
                if (cipherText == null || cipherText.Length < ivLength)
                    throw new CryptographicException("Ciphertext is missing or too short to contain an IV.");

                byte[] iv = new byte[ivLength];
                Buffer.BlockCopy(cipherText, 0, iv, 0, ivLength);
                aes.IV = iv;

                using (MemoryStream ms = new MemoryStream(cipherText, ivLength, cipherText.Length - ivLength))
                using (CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read))
                using (StreamReader sr = new StreamReader(cs))
                {
                    return sr.ReadToEnd();
                }
            }
        }

        public string DecryptStringInMemory(string inputFile)
        {
            byte[] encryptedData = File.ReadAllBytes(inputFile);
            return DecryptString(encryptedData);
        }
    }
}
