using TransparencyVerifier.Models;

namespace TransparencyVerifier;

/// <summary>
/// Hiển thị bộ bài từng vòng theo format bảng.
/// </summary>
public static class DeckDisplayer
{
    // Nhãn hiển thị tiếng Việt cho từng payload
    private static string KetQua(string payload)
    {
        if (payload == "TRUNG_QUYEN_MUA") return "TRÚNG QUYỀN MUA";
        if (payload == "KHONG_TRUNG_UU_TIEN") return "KHÔNG TRÚNG";
        if (payload == "CHO_PHAN_LOAI_DU") return "CHỜ PHÂN LOẠI";
        if (payload.StartsWith("TRUNG:", StringComparison.Ordinal))
            return $"TRÚNG  →  {payload["TRUNG:".Length..]}";
        if (payload == "KHONG_TRUNG") return "KHÔNG TRÚNG";
        if (payload.StartsWith("DU_KHUYET:", StringComparison.Ordinal))
            return $"DỰ KHUYẾT #{payload["DU_KHUYET:".Length..]}";
        return payload;
    }

    private static string RoundLabel(string round)
    {
        if (round == "A1") return "VÒNG A1 — Bốc quyền mua (U2)";
        if (round.StartsWith("A2:", StringComparison.Ordinal))
            return $"VÒNG A2 — Phân căn ưu tiên — Loại phòng: {round["A2:".Length..]}";
        if (round.StartsWith("B:", StringComparison.Ordinal))
            return $"VÒNG B  — Vòng thường — Loại phòng: {round["B:".Length..]}";
        if (round == "C") return "VÒNG C  — Căn dư (gộp 1 vòng + dự khuyết)";
        return round;
    }

    public static void Display(List<DeckInfo> decks, List<DrawStep> steps)
    {
        // Build a set of (round, position) that were auto-drawn
        var autoDrawnSet = new HashSet<(string Round, int Position)>(
            steps.Where(s => s.AutoDrawn).Select(s => (s.Round, s.Position)));


        Console.WriteLine();
        Console.WriteLine(new string('═', 80));
        Console.WriteLine("  TÁI HIỆN CHỒNG PHIẾU TỪNG VÒNG");
        Console.WriteLine(new string('═', 80));

        foreach (var deck in decks)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"  ╔══ {RoundLabel(deck.Round)} ══");
            Console.ResetColor();
            Console.WriteLine($"  ║   DeckHash : {deck.DeckHash}");
            Console.WriteLine($"  ║   Tổng số phiếu: {deck.Size}  |  Phiếu trúng: {deck.WonCount}  |  Phiếu không trúng/chờ: {deck.Size - deck.WonCount}");
            Console.WriteLine($"  ║   Niêm phong: {deck.SealedAt:dd/MM/yyyy HH:mm} UTC");
            Console.WriteLine($"  ╚{new string('═', 60)}");
            Console.WriteLine();

            // Header
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  {"Phiếu số",-12}{"Nội dung phiếu (raw)",-30}{"Kết quả hiển thị",-30}{"Ghi chú",-12}");
            Console.WriteLine($"  {new string('─', 12)}{new string('─', 30)}{new string('─', 30)}{new string('─', 12)}");
            Console.ResetColor();

            for (int i = 0; i < deck.Tickets.Count; i++)
            {
                var ticket = deck.Tickets[i];
                var ketQua = KetQua(ticket);
                var isAutoDrawn = autoDrawnSet.Contains((deck.Round, i));
                var ghiChu = isAutoDrawn ? "(Tự động)" : "";

                // Chọn màu theo loại vé
                if (ticket.StartsWith("TRUNG", StringComparison.Ordinal))
                    Console.ForegroundColor = ConsoleColor.Green;
                else if (ticket.StartsWith("DU_KHUYET", StringComparison.Ordinal))
                    Console.ForegroundColor = ConsoleColor.Yellow;
                else if (ticket == "CHO_PHAN_LOAI_DU")
                    Console.ForegroundColor = ConsoleColor.Magenta;
                else
                    Console.ForegroundColor = ConsoleColor.Red;

                Console.WriteLine($"  {i,-12}{ticket,-30}{ketQua,-30}{ghiChu,-12}");
                Console.ResetColor();
            }

            Console.WriteLine();
        }
    }
}
