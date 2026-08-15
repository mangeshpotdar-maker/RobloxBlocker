using System;
using System.Security.Cryptography;
using System.Text;

namespace MPCustom.Security
{
    public class DpapiEncryptionService : IDpapiEncryptionService
    {
        public byte[] Protect(byte[] userData, byte[]? optionalEntropy = null)
        {
            if (userData == null || userData.Length == 0) return Array.Empty<byte>();

            if (OperatingSystem.IsWindows())
            {
                return ProtectedData.Protect(userData, optionalEntropy, DataProtectionScope.LocalMachine);
            }
            else
            {
                // Fallback for non-Windows environments (tests)
                var xor = new byte[userData.Length];
                for (int i = 0; i < userData.Length; i++)
                {
                    xor[i] = (byte)(userData[i] ^ 0x5A);
                }
                return xor;
            }
        }

        public byte[] Unprotect(byte[] encryptedData, byte[]? optionalEntropy = null)
        {
            if (encryptedData == null || encryptedData.Length == 0) return Array.Empty<byte>();

            if (OperatingSystem.IsWindows())
            {
                return ProtectedData.Unprotect(encryptedData, optionalEntropy, DataProtectionScope.LocalMachine);
            }
            else
            {
                // Fallback for non-Windows environments (tests)
                var xor = new byte[encryptedData.Length];
                for (int i = 0; i < encryptedData.Length; i++)
                {
                    xor[i] = (byte)(encryptedData[i] ^ 0x5A);
                }
                return xor;
            }
        }

        public string ProtectString(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return string.Empty;

            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] cipherBytes = Protect(plainBytes);
            return Convert.ToBase64String(cipherBytes);
        }

        public string UnprotectString(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return string.Empty;

            try
            {
                byte[] cipherBytes = Convert.FromBase64String(cipherText);
                byte[] plainBytes = Unprotect(cipherBytes);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
