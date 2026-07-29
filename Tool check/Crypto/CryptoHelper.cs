using System.Security.Cryptography;
using System.Text;

namespace Tool_check.Crypto;

public static class CryptoHelper
{
    public static byte[] Sha256Bytes(byte[] data) => SHA256.HashData(data);

    public static string Sha256Hex(byte[] data) =>
        Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    public static string Sha256Hex(string text) =>
        Sha256Hex(Encoding.UTF8.GetBytes(text));

    public static byte[] RoundSeed(byte[] masterSeed, string label)
    {
        var labelBytes = Encoding.UTF8.GetBytes(label);
        var combined = new byte[masterSeed.Length + labelBytes.Length];
        Buffer.BlockCopy(masterSeed, 0, combined, 0, masterSeed.Length);
        Buffer.BlockCopy(labelBytes, 0, combined, masterSeed.Length, labelBytes.Length);
        return SHA256.HashData(combined);
    }
}
