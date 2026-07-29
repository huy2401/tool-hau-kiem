using Tool_check.Models;

namespace Tool_check.Verifiers;

public static class HashChainVerifier
{
    public sealed record ChainResult(
        bool Ok,
        int TotalSteps,
        int? BrokenAtIndex,
        string? BrokenRound,
        int? BrokenPosition,
        string? ErrorDetail);

    public static ChainResult Verify(List<DrawStep> steps)
    {
        if (steps.Count == 0)
            return new ChainResult(true, 0, null, null, null, "Không có lượt bấm nào");

        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];

            if (step.EntryHash is null)
            {
                return new ChainResult(false, steps.Count, i, step.Round, step.Position,
                    $"Bước #{i} (vòng {step.Round}, phiếu {step.Position}) thiếu entryHash");
            }

            if (i == 0)
            {
                if (step.PrevHash is not null)
                {
                    return new ChainResult(false, steps.Count, i, step.Round, step.Position,
                        $"Bước đầu tiên phải có prevHash = null, thực tế = {step.PrevHash}");
                }
            }
            else
            {
                var expectedPrev = steps[i - 1].EntryHash;
                if (!string.Equals(step.PrevHash, expectedPrev, StringComparison.OrdinalIgnoreCase))
                {
                    return new ChainResult(false, steps.Count, i, step.Round, step.Position,
                        $"Chuỗi băm bị đứt tại bước #{i} (vòng {step.Round}, phiếu {step.Position}).\n" +
                        $"  prevHash: {step.PrevHash ?? "null"}\n" +
                        $"  entryHash trước: {expectedPrev ?? "null"}");
                }
            }
        }

        return new ChainResult(true, steps.Count, null, null, null, null);
    }
}
