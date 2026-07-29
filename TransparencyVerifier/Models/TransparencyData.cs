using System.Text.Json.Serialization;

namespace TransparencyVerifier.Models;

// ── Top-level response ────────────────────────────────────────────────────────

public sealed class TransparencyData
{
    [JsonPropertyName("projectId")]
    public Guid ProjectId { get; set; }

    [JsonPropertyName("projectName")]
    public string ProjectName { get; set; } = "";

    [JsonPropertyName("completedAt")]
    public DateTime? CompletedAt { get; set; }

    [JsonPropertyName("waitlistSize")]
    public int? WaitlistSize { get; set; }

    [JsonPropertyName("nguonNgauNhien")]
    public List<RandomnessSource> NguonNgauNhien { get; set; } = [];

    [JsonPropertyName("decks")]
    public List<DeckInfo> Decks { get; set; } = [];

    [JsonPropertyName("nhatKyBoc")]
    public List<DrawStep> NhatKyBoc { get; set; } = [];

    [JsonPropertyName("thuatToan")]
    public string? ThuatToan { get; set; }
}

// ── Nguồn ngẫu nhiên (per vòng) ─────────────────────────────────────────────

public sealed class RandomnessSource
{
    [JsonPropertyName("round")]
    public string Round { get; set; } = "";

    [JsonPropertyName("masterSeed")]
    public string? MasterSeed { get; set; }

    [JsonPropertyName("rServer")]
    public string? RServer { get; set; }

    [JsonPropertyName("rServerCommit")]
    public string? RServerCommit { get; set; }

    [JsonPropertyName("rSupervisor")]
    public string? RSupervisor { get; set; }

    [JsonPropertyName("blockHeight")]
    public long BlockHeight { get; set; }

    [JsonPropertyName("blockHash")]
    public string? BlockHash { get; set; }

    [JsonPropertyName("inputHash")]
    public string? InputHash { get; set; }

    [JsonPropertyName("anchorChain")]
    public string? AnchorChain { get; set; }
}

// ── Bộ bài (deck) ─────────────────────────────────────────────────────────────

public sealed class DeckInfo
{
    [JsonPropertyName("round")]
    public string Round { get; set; } = "";

    [JsonPropertyName("deckHash")]
    public string DeckHash { get; set; } = "";

    [JsonPropertyName("size")]
    public int Size { get; set; }

    [JsonPropertyName("wonCount")]
    public int WonCount { get; set; }

    [JsonPropertyName("sealedAt")]
    public DateTime? SealedAt { get; set; }

    [JsonPropertyName("revealedAt")]
    public DateTime? RevealedAt { get; set; }

    [JsonPropertyName("tickets")]
    public List<string> Tickets { get; set; } = [];
}

// ── Nhật ký bốc (draw step) ───────────────────────────────────────────────────

public sealed class DrawStep
{
    [JsonPropertyName("round")]
    public string Round { get; set; } = "";

    [JsonPropertyName("position")]
    public int Position { get; set; }

    [JsonPropertyName("payload")]
    public string Payload { get; set; } = "";

    [JsonPropertyName("autoDrawn")]
    public bool AutoDrawn { get; set; }

    [JsonPropertyName("prevHash")]
    public string? PrevHash { get; set; }

    [JsonPropertyName("entryHash")]
    public string? EntryHash { get; set; }
}
