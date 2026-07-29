using Tool_check.Models;

namespace Tool_check.Verifiers;

public static class PayloadVerifier
{
    public sealed record MismatchInfo(string Round, int Position, string ExpectedFromDeck, string ActualFromLog);

    public sealed record Result(
        bool Ok,
        int TotalChecked,
        List<MismatchInfo> Mismatches,
        List<string> Warnings);

    public static Result Verify(List<DeckInfo> decks, List<DrawStep> steps)
    {
        var deckByRound = decks.ToDictionary(d => d.Round, StringComparer.OrdinalIgnoreCase);

        var mismatches = new List<MismatchInfo>();
        var warnings = new List<string>();
        int totalChecked = 0;

        var stepsByRound = steps.GroupBy(s => s.Round, StringComparer.OrdinalIgnoreCase);

        foreach (var group in stepsByRound)
        {
            var round = group.Key;

            if (!deckByRound.TryGetValue(round, out var deck))
            {
                warnings.Add($"Vòng '{round}' có trong nhật ký bấm nhưng không có chồng phiếu tương ứng.");
                continue;
            }

            foreach (var step in group)
            {
                totalChecked++;
                if (step.Position < 0 || step.Position >= deck.Tickets.Count)
                {
                    mismatches.Add(new MismatchInfo(round, step.Position,
                        $"[Vị trí {step.Position} ngoài phạm vi 0..{deck.Tickets.Count - 1}]",
                        step.Payload));
                    continue;
                }

                var expectedFromDeck = deck.Tickets[step.Position];
                if (!string.Equals(expectedFromDeck, step.Payload, StringComparison.Ordinal))
                {
                    mismatches.Add(new MismatchInfo(round, step.Position, expectedFromDeck, step.Payload));
                }
            }
        }

        return new Result(mismatches.Count == 0, totalChecked, mismatches, warnings);
    }
}
