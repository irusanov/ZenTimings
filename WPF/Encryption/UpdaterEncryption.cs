using System;
using System.Security.Cryptography;

namespace ZenTimings.Encryption
{
    /// <summary>
    /// RSA signature verification for the update metadata file.
    /// </summary>
    internal static class UpdaterSignature
    {
        // RSA-2048 public key in XML format.
        private const string PublicKeyXml = "<RSAKeyValue><Modulus>xd+oBWB2K0hnfCOb8K3jT2IMAI90APR4mM7TnXht2EqBH/3OKHDyJNO/SWVhD5IRkl888swW12/kSymnuj/+U9eolOvqVJFzAee45kFfZab/L0h71shvqDOIdncasO+pBL23GF7KKUXXN5D4eDs7V9vDOnOra6vVtkdaOSoGtMDvnDG6auadsOHn/nojXRIpjn1lEB1f74ecLqy+SRHA++Di2eut/SOETsWwsS1J/9rjEgwliIHQN7pV7kd7ktGQqfndIjJYx/8F3+HU2pB0RFfjrYQAYjIejNIncz3LJOdVNlJa2H4+TzTIiFRVTF/1pNtY/OaL1nNFUH3uzlGUrQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>;

        /// <summary>
        /// Verifies that <paramref name="data"/> was signed with the matching private key.
        /// </summary>
        /// <param name="data">The raw bytes of the update XML.</param>
        /// <param name="signature">The RSA-SHA256 signature bytes.</param>
        /// <returns>true if the signature is valid; otherwise false.</returns>
        public static bool Verify(byte[] data, byte[] signature)
        {
            // ProviderType 24 = PROV_RSA_AES — required on .NET 4.5 for SHA-256 signatures
            using (var rsa = new RSACryptoServiceProvider(new CspParameters { ProviderType = 24 }))
            {
                try
                {
                    rsa.FromXmlString(PublicKeyXml);
                    return rsa.VerifyData(data, CryptoConfig.MapNameToOID("SHA256"), signature);
                }
                finally
                {
                    rsa.PersistKeyInCsp = false;
                }
            }
        }
    }
}
