using System.Security.Cryptography;
using System.Text;

namespace TransparencyVerifier.Crypto;

/// <summary>
/// Hàm SHA-256 helper — copy logic từ backend CryptoHelper.cs.
/// </summary>
public static class CryptoHelper
{
    public static byte[] Sha256Bytes(byte[] data) => SHA256.HashData(data);

    public static string Sha256Hex(byte[] data) =>
        Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    public static string Sha256Hex(string text) =>
        Sha256Hex(Encoding.UTF8.GetBytes(text));

    /// <summary>
    /// RoundSeed(label) = SHA-256(MASTER_SEED ‖ UTF-8(label)).
    /// </summary>
    public static byte[] RoundSeed(byte[] masterSeed, string label)
    {
        var labelBytes = Encoding.UTF8.GetBytes(label);
        var combined = new byte[masterSeed.Length + labelBytes.Length];
        Buffer.BlockCopy(masterSeed, 0, combined, 0, masterSeed.Length);
        Buffer.BlockCopy(labelBytes, 0, combined, masterSeed.Length, labelBytes.Length);
        return SHA256.HashData(combined);
    }

    /// <summary>
    /// Hex → byte[], bỏ qua hoa/thường.
    /// </summary>
    public static byte[] HexToBytes(string hex) => Convert.FromHexString(hex);
}
