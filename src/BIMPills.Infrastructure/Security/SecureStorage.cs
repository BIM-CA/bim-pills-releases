using System;
using System.Security.Cryptography;
using System.Text;

namespace BIMPills.Infrastructure.Security
{
    /// <summary>
    /// Wraps Windows DPAPI (Data Protection API) to protect sensitive strings at rest.
    /// Encrypted data is bound to the current Windows user — only the same user on the
    /// same machine can decrypt it, so it cannot be read by other users or on other machines.
    /// </summary>
    public static class SecureStorage
    {
        // Entropy adds an extra application-specific layer: data protected by BIMPills
        // cannot be decrypted by other apps even for the same Windows user.
        private static readonly byte[] _entropy = Encoding.UTF8.GetBytes("BIMPills-2026-DPAPI");

        /// <summary>
        /// Encrypts <paramref name="plainText"/> with DPAPI and returns a Base64 string
        /// safe to store in JSON or text files.
        /// </summary>
        public static string Protect(string plainText)
        {
            var bytes     = Encoding.UTF8.GetBytes(plainText);
            var encrypted = ProtectedData.Protect(bytes, _entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encrypted);
        }

        /// <summary>
        /// Decrypts a Base64 string previously produced by <see cref="Protect"/>.
        /// Throws <see cref="CryptographicException"/> if the data is invalid or was
        /// encrypted by a different user or machine.
        /// </summary>
        public static string Unprotect(string cipherBase64)
        {
            var bytes     = Convert.FromBase64String(cipherBase64);
            var decrypted = ProtectedData.Unprotect(bytes, _entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }

        /// <summary>
        /// Tries to decrypt a value. Returns <c>true</c> and the decrypted text on success.
        /// Returns <c>false</c> and the original value unchanged on failure — used to
        /// migrate legacy plaintext values: on failure the caller should re-save encrypted.
        /// </summary>
        public static bool TryUnprotect(string value, out string result)
        {
            try
            {
                result = Unprotect(value);
                return true;
            }
            catch
            {
                // Value is either plaintext (legacy) or encrypted by a different user/machine.
                result = value;
                return false;
            }
        }
    }
}
