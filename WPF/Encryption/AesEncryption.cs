using System;
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
            Console.WriteLine("XML file encrypted successfully!");
        }

        public T DecryptXmlInMemory<T>(string inputFile)
        {
            byte[] encryptedData = File.ReadAllBytes(inputFile);
            string decryptedXml = DecryptString(encryptedData);
            return XmlUtils.DeserializeFromXml<T>(decryptedXml);
        }

        public byte[] EncryptString(string plainText)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = encryptionKeys.Key;
                aes.IV = encryptionKeys.IV;

                using (MemoryStream ms = new MemoryStream())
                using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(plainText);
                    cs.Write(bytes, 0, bytes.Length);
                    cs.FlushFinalBlock();
                    return ms.ToArray();
                }
            }
        }

        public string DecryptString(byte[] cipherText)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = encryptionKeys.Key;
                aes.IV = encryptionKeys.IV;

                using (MemoryStream ms = new MemoryStream(cipherText))
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
