using System.Security.Cryptography;
using System.Text;

namespace TransparencyVerifier.Crypto;

/// <summary>
/// Hàm serialize + hash deck — copy chính xác từ backend CanonicalDeckSerializer.cs.
/// Định dạng cố định: mỗi phiếu một dòng "{position}\t{payload}\n", UTF-8 không BOM.
/// DeckHash = SHA-256(bytes) hex thường.
/// </summary>
public static class CanonicalDeckSerializer
{
    public static byte[] Serialize(IReadOnlyList<string> orderedTickets)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < orderedTickets.Count; i++)
        {
            sb.Append(i.ToString(System.Globalization.CultureInfo.InvariantCulture));
            sb.Append('\t');
            sb.Append(orderedTickets[i]);
            sb.Append('\n');
        }
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(sb.ToString());
    }

    /// <summary>SHA-256 hex (thường) của serialize(deck) — chính là DeckHash công bố trước khi mở.</summary>
    public static string Hash(IReadOnlyList<string> orderedTickets) =>
        Convert.ToHexString(SHA256.HashData(Serialize(orderedTickets))).ToLowerInvariant();
}
