using System.Security.Cryptography;
using System.Text;

namespace HanBridge.Core.Security;

public static class DpapiProtector
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("HanBridge/v1");

    public static byte[] Protect(ReadOnlySpan<byte> plaintext)
    {
        return ProtectedData.Protect(plaintext.ToArray(), Entropy, DataProtectionScope.CurrentUser);
    }

    public static byte[] Unprotect(ReadOnlySpan<byte> ciphertext)
    {
        return ProtectedData.Unprotect(ciphertext.ToArray(), Entropy, DataProtectionScope.CurrentUser);
    }

    public static string ProtectString(string plaintext)
    {
        return Convert.ToBase64String(Protect(Encoding.UTF8.GetBytes(plaintext)));
    }

    public static string UnprotectString(string ciphertext)
    {
        return Encoding.UTF8.GetString(Unprotect(Convert.FromBase64String(ciphertext)));
    }
}