using System.Security.Cryptography;
using System.Text;

namespace Tool_check.Crypto;

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

    public static string Hash(IReadOnlyList<string> orderedTickets) =>
        Convert.ToHexString(SHA256.HashData(Serialize(orderedTickets))).ToLowerInvariant();
}
