using System.Net.Http.Json;
using System.Text.Json;
using TransparencyVerifier;
using TransparencyVerifier.Models;
using TransparencyVerifier.Verifiers;

// ─── Cấu hình đầu vào ──────────────────────────────────────────────────────

string baseUrl;
string projectId;

if (args.Length >= 2)
{
    baseUrl = args[0].TrimEnd('/');
    projectId = args[1];
}
else
{
    Console.Write("Nhập Base URL server (vd: https://10.43.30.87:5002/api): ");
    baseUrl = (Console.ReadLine() ?? "").TrimEnd('/');

    Console.Write("Nhập Project ID (Guid): ");
    projectId = Console.ReadLine() ?? "";
}

if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(projectId))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("Thiếu Base URL hoặc Project ID. Thoát.");
    Console.ResetColor();
    return 1;
}

// ─── Gọi API /transparency ────────────────────────────────────────────────────

PrintBanner();
Console.WriteLine($"  Đang tải dữ liệu minh bạch từ server...");
Console.WriteLine($"  URL: {baseUrl}/projects/{projectId}/transparency");
Console.WriteLine();

TransparencyData data;
try
{
    using var handler = new HttpClientHandler
    {
        // Bỏ qua kiểm tra SSL cert tự ký (môi trường dev/internal)
        ServerCertificateCustomValidationCallback = (_, _, _, _) => true
    };
    using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };

    var url = $"{baseUrl}/projects/{projectId}/transparency";
    var response = await http.GetAsync(url);

    if (!response.IsSuccessStatusCode)
    {
        var body = await response.Content.ReadAsStringAsync();
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"  ❌ Lỗi từ server: HTTP {(int)response.StatusCode} {response.StatusCode}");
        Console.WriteLine($"     {body}");
        Console.ResetColor();
        return 1;
    }

    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    data = await response.Content.ReadFromJsonAsync<TransparencyData>(options)
           ?? throw new Exception("Server trả về JSON rỗng.");
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"  ❌ Không thể kết nối hoặc parse dữ liệu: {ex.Message}");
    Console.ResetColor();
    return 1;
}

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine($"  ✅ Đã tải thành công — Dự án: {data.ProjectName}");
Console.WriteLine($"     Hoàn tất lúc: {data.CompletedAt:dd/MM/yyyy HH:mm} UTC");
Console.WriteLine($"     Số vòng: {data.Decks.Count}  |  Tổng lượt bấm: {data.NhatKyBoc.Count}");
Console.ResetColor();
Console.WriteLine();

// ─── Chạy 5 bài kiểm tra ─────────────────────────────────────────────────────

Console.WriteLine(new string('═', 80));
Console.WriteLine("  BÀI KIỂM TRA TÍNH HỢP LỆ");
Console.WriteLine(new string('═', 80));

bool allOk = true;

// ── Bài 1 & 2: R_server commit + MASTER_SEED ─────────────────────────────────
Console.WriteLine();
Console.ForegroundColor = ConsoleColor.DarkGray;
Console.WriteLine("  [1/5] Kiểm tra R_server commit  (SHA-256(R_server) == R_server commit?)");
Console.WriteLine("  [2/5] Kiểm tra MASTER_SEED       (SHA-256(R_server ‖ R_supervisor ‖ BlockHash) == MASTER_SEED?)");
Console.ResetColor();
Console.WriteLine();

var commitResults = CommitVerifier.Verify(data.NguonNgauNhien);
foreach (var cr in commitResults)
{
    Console.WriteLine($"  Vòng {cr.Round}:");

    if (cr.ErrorMessage is not null)
    {
        PrintResult(false, $"Lỗi: {cr.ErrorMessage}");
        allOk = false;
        continue;
    }

    PrintResult(cr.CommitOk, $"R_server commit: " +
        (cr.CommitOk ? "Khớp" : $"KHÔNG KHỚP\n       Công bố : {cr.CommitExpected}\n       Tính lại: {cr.CommitActual}"));
    PrintResult(cr.MasterSeedOk, $"MASTER_SEED    : " +
        (cr.MasterSeedOk ? "Khớp" : $"KHÔNG KHỚP\n       Công bố : {cr.MasterSeedExpected}\n       Tính lại: {cr.MasterSeedActual}"));

    if (!cr.CommitOk || !cr.MasterSeedOk) allOk = false;
    Console.WriteLine();
}

// ── Bài 3: DeckHash ───────────────────────────────────────────────────────────
Console.ForegroundColor = ConsoleColor.DarkGray;
Console.WriteLine("  [3/5] Kiểm tra DeckHash (SHA-256 canonical serialize danh sách phiếu == DeckHash?)");
Console.ResetColor();
Console.WriteLine();

var deckHashResults = DeckHashVerifier.Verify(data.Decks);
foreach (var dr in deckHashResults)
{
    PrintResult(dr.Ok,
        $"Chồng phiếu vòng {dr.Round}: " +
        (dr.Ok ? $"Khớp  (hash: {dr.Actual[..16]}...)"
               : $"KHÔNG KHỚP\n       Công bố : {dr.Expected}\n       Tính lại: {dr.Actual}"));
    if (!dr.Ok) allOk = false;
}
Console.WriteLine();

// ── Bài 4: Hash-Chain ─────────────────────────────────────────────────────────
Console.ForegroundColor = ConsoleColor.DarkGray;
Console.WriteLine("  [4/5] Kiểm tra Hash-Chain nhật ký bấm (chuỗi prevHash → entryHash liên tục?)");
Console.ResetColor();
Console.WriteLine();

var chainResult = HashChainVerifier.Verify(data.NhatKyBoc);
if (chainResult.Ok)
{
    PrintResult(true, $"Chuỗi băm hợp lệ  ({chainResult.TotalSteps} mắt xích, không bị đứt gãy)");
}
else
{
    PrintResult(false,
        $"Chuỗi băm BỊ ĐỨT tại bước #{chainResult.BrokenAtIndex} " +
        $"(vòng {chainResult.BrokenRound}, phiếu số {chainResult.BrokenPosition})\n" +
        $"       {chainResult.ErrorDetail}");
    allOk = false;
}
Console.WriteLine();

// ── Bài 5: Payload khớp deck ─────────────────────────────────────────────────
Console.ForegroundColor = ConsoleColor.DarkGray;
Console.WriteLine("  [5/5] Kiểm tra payload nhật ký bấm khớp chồng phiếu (deck[round][position] == log.payload?)");
Console.ResetColor();
Console.WriteLine();

var payloadResult = PayloadVerifier.Verify(data.Decks, data.NhatKyBoc);
if (payloadResult.Ok)
{
    PrintResult(true, $"Payload khớp toàn bộ  ({payloadResult.TotalChecked} lượt bấm kiểm tra)");
}
else
{
    PrintResult(false, $"Phát hiện {payloadResult.Mismatches.Count} lượt bấm CÓ PAYLOAD KHÔNG KHỚP (trên {payloadResult.TotalChecked} lượt):");
    foreach (var m in payloadResult.Mismatches.Take(10))
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"       Vòng {m.Round} | Phiếu {m.Position}:");
        Console.WriteLine($"         Trong bộ bài : {m.ExpectedFromDeck}");
        Console.WriteLine($"         Trong nhật ký: {m.ActualFromLog}");
        Console.ResetColor();
    }
    allOk = false;
}

if (payloadResult.Warnings.Count > 0)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    foreach (var w in payloadResult.Warnings)
        Console.WriteLine($"  ⚠  {w}");
    Console.ResetColor();
}
Console.WriteLine();

// ─── Kết luận tổng hợp ───────────────────────────────────────────────────────

Console.WriteLine(new string('═', 80));
if (allOk)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("  ✅  KẾT LUẬN: DỮ LIỆU HỢP LỆ HOÀN TOÀN");
    Console.WriteLine("      Kết quả bốc thăm TRUNG THỰC, không phát hiện bất kỳ sự can thiệp nào.");
}
else
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("  ❌  KẾT LUẬN: PHÁT HIỆN BẤT THƯỜNG");
    Console.WriteLine("      Một hoặc nhiều bài kiểm tra thất bại. Cần điều tra thêm.");
}
Console.ResetColor();
Console.WriteLine(new string('═', 80));
Console.WriteLine();

// ─── Tái hiện bộ bài ─────────────────────────────────────────────────────────

Console.Write("  Hiển thị bộ bài chi tiết từng vòng? (y/N): ");
var answer = Console.ReadLine()?.Trim().ToLowerInvariant();
if (answer == "y" || answer == "yes")
{
    DeckDisplayer.Display(data.Decks, data.NhatKyBoc);
}

return allOk ? 0 : 2;

// ─── Helper functions ─────────────────────────────────────────────────────────

static void PrintBanner()
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine();
    Console.WriteLine("  ╔══════════════════════════════════════════════════════════════════╗");
    Console.WriteLine("  ║       CÔNG CỤ KIỂM TRA MINH BẠCH BỐC THĂM NHÀ Ở XÃ HỘI        ║");
    Console.WriteLine("  ║        Transparency Verifier — Dành cho Tổ Giám Sát Độc Lập     ║");
    Console.WriteLine("  ╚══════════════════════════════════════════════════════════════════╝");
    Console.ResetColor();
    Console.WriteLine();
}

static void PrintResult(bool ok, string message)
{
    if (ok)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("  ✅  ");
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write("  ❌  ");
    }
    Console.ResetColor();
    Console.WriteLine(message);
}
