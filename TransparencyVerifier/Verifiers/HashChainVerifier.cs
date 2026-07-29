using System.Buffers.Binary;
using System.Text;
using TransparencyVerifier.Models;

namespace TransparencyVerifier.Verifiers;

/// <summary>
/// Bài kiểm tra 4: Hash-Chain nhật ký bấm.
/// Tính lại chuỗi EntryHash từ đầu đến cuối và so với giá trị đã lưu.
///
/// EntryHash = SHA-256(PrevHash ‖ ApplicantId(16 bytes) ‖ DeckId(16 bytes) ‖ Position(int32 BE) ‖ UTF-8(Payload))
///
/// LƯU Ý QUAN TRỌNG: API /transparency KHÔNG trả ApplicantId và DeckId (vì lý do PII/bảo mật).
/// Do đó, bài kiểm tra này hoạt động theo 2 chế độ:
///   - Nếu API trả entryHash: kiểm tra tính LIÊN TỤC của chuỗi (prevHash khớp entryHash trước).
///   - Phát hiện bất kỳ mắt xích nào bị đứt gãy (entryHash bị null hoặc prevHash không khớp).
/// </summary>
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

        // Kiểm tra tính liên tục của chuỗi prevHash → entryHash
        // Step[i].prevHash phải == Step[i-1].entryHash
        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];

            if (step.EntryHash is null)
            {
                return new ChainResult(false, steps.Count, i, step.Round, step.Position,
                    $"Bước #{i} (vòng {step.Round}, phiếu {step.Position}) thiếu entryHash — chưa materialize hoặc bị xóa");
            }

            if (i == 0)
            {
                // Bước đầu tiên: prevHash phải là null (chuỗi bắt đầu bằng empty)
                if (step.PrevHash is not null)
                {
                    return new ChainResult(false, steps.Count, i, step.Round, step.Position,
                        $"Bước đầu tiên phải có prevHash = null, thực tế = {step.PrevHash}");
                }
            }
            else
            {
                // Bước i: prevHash phải bằng entryHash của bước i-1
                var expectedPrev = steps[i - 1].EntryHash;
                if (!string.Equals(step.PrevHash, expectedPrev, StringComparison.OrdinalIgnoreCase))
                {
                    return new ChainResult(false, steps.Count, i, step.Round, step.Position,
                        $"Chuỗi băm bị đứt tại bước #{i} (vòng {step.Round}, phiếu {step.Position}).\n" +
                        $"  prevHash ghi nhận : {step.PrevHash ?? "null"}\n" +
                        $"  entryHash bước trước: {expectedPrev ?? "null"}");
                }
            }
        }

        return new ChainResult(true, steps.Count, null, null, null, null);
    }
}
