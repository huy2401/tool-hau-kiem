using TransparencyVerifier.Models;

namespace TransparencyVerifier.Verifiers;

/// <summary>
/// Bài kiểm tra 3: DeckHash
/// Serialize lại danh sách phiếu theo canonical format và so với DeckHash công bố.
/// </summary>
public static class DeckHashVerifier
{
    public sealed record Result(
        string Round,
        bool Ok,
        string Expected,
        string Actual,
        string? ErrorMessage);

    public static List<Result> Verify(List<DeckInfo> decks)
    {
        var results = new List<Result>();

        foreach (var deck in decks)
        {
            try
            {
                var computed = Crypto.CanonicalDeckSerializer.Hash(deck.Tickets);
                var ok = string.Equals(computed, deck.DeckHash, StringComparison.OrdinalIgnoreCase);
                results.Add(new Result(deck.Round, ok, deck.DeckHash, computed, null));
            }
            catch (Exception ex)
            {
                results.Add(new Result(deck.Round, false, deck.DeckHash, "", $"Lỗi: {ex.Message}"));
            }
        }

        return results;
    }
}
