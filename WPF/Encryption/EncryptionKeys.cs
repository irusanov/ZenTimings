using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;

namespace ZenTimings.Encryption
{
    /// <summary>
    /// Generates (or loads) the AES-256 key used by <see cref="AesEncryption"/> and protects it
    /// at rest using the Windows Data Protection API (DPAPI), scoped to the current user account.
    ///
    /// The previous implementation wrote the raw AES key <em>and</em> a fixed IV as plain bytes
    /// to "key.bin"/"iv.bin" using a working-directory-relative path. That defeated the purpose
    /// of encrypting anything: the key needed to decrypt a file was sitting in plain text right
    /// next to it, so anyone who could read the encrypted file could also read the key. Using
    /// DPAPI means the stored key blob is only unprotectable by the same Windows user account
    /// (and, with <see cref="DataProtectionScope.CurrentUser"/>, only on the same machine) that
    /// created it - copying both files elsewhere is no longer enough to read the plaintext.
    ///
    /// This still does not protect data from the same Windows user ZenTimings itself runs as -
    /// DPAPI-unprotect only requires being logged in as that user, so this is meant to guard
    /// against another local account or a copied-off file, not against the machine's own user.
    /// If that stronger guarantee is ever needed, this needs a user-supplied passphrase (e.g.
    /// via PBKDF2/Rfc2898DeriveBytes) instead of a machine/account-derived key.
    /// </summary>
    internal class EncryptionKeys
    {
        private const int KeySizeBits = 256;

        private static readonly string KeyFilePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "key.protected.bin");

        public byte[] Key { get; }

        public EncryptionKeys()
        {
            Key = LoadOrGenerateKey();
        }

        private static byte[] LoadOrGenerateKey()
        {
            if (File.Exists(KeyFilePath))
            {
                try
                {
                    byte[] protectedKey = File.ReadAllBytes(KeyFilePath);
                    return ProtectedData.Unprotect(protectedKey, null, DataProtectionScope.CurrentUser);
                }
                catch (CryptographicException ex)
                {
                    // The key file exists but can't be unprotected - most likely it was created
                    // by a different Windows user/machine, or the file is corrupt. There is no
                    // way to recover the original key in that case, so fall through and generate
                    // a fresh one; anything encrypted with the old key becomes unreadable, which
                    // is the correct outcome given the key itself is unrecoverable.
                    Debug.WriteLine($"Could not unprotect stored encryption key, generating a new one: {ex.Message}");
                }
            }

            return GenerateAndStoreKey();
        }

        private static byte[] GenerateAndStoreKey()
        {
            using (Aes aes = Aes.Create())
            {
                aes.KeySize = KeySizeBits;
                aes.GenerateKey();

                byte[] key = aes.Key;
                byte[] protectedKey = ProtectedData.Protect(key, null, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(KeyFilePath, protectedKey);

                Debug.WriteLine("Encryption key generated and stored (DPAPI-protected).");
                return key;
            }
        }
    }
}
