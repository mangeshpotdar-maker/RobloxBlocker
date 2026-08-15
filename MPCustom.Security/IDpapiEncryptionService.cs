namespace MPCustom.Security
{
    public interface IDpapiEncryptionService
    {
        byte[] Protect(byte[] userData, byte[]? optionalEntropy = null);
        byte[] Unprotect(byte[] encryptedData, byte[]? optionalEntropy = null);
        string ProtectString(string plainText);
        string UnprotectString(string cipherText);
    }
}
