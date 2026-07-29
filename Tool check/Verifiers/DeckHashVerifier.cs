using Tool_check.Models;

namespace Tool_check.Verifiers;

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
