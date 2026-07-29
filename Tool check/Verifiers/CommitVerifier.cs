using Tool_check.Models;

namespace Tool_check.Verifiers;

public static class CommitVerifier
{
    public sealed record Result(
        string Round,
        bool CommitOk,
        string? CommitExpected,
        string? CommitActual,
        bool MasterSeedOk,
        string? MasterSeedExpected,
        string? MasterSeedActual,
        string? ErrorMessage);

    public static List<Result> Verify(List<RandomnessSource> sources)
    {
        var results = new List<Result>();

        foreach (var s in sources)
        {
            if (s.RServer is null || s.MasterSeed is null)
            {
                results.Add(new Result(s.Round,
                    false, null, null,
                    false, null, null,
                    "Thiếu dữ liệu RServer hoặc MasterSeed"));
                continue;
            }

            bool commitOk = false;
            string? commitExpected = s.RServerCommit?.ToLowerInvariant();
            string? commitActual = null;

            try
            {
                var rServerBytes = Convert.FromHexString(s.RServer);
                commitActual = Crypto.CryptoHelper.Sha256Hex(rServerBytes);
                commitOk = string.Equals(commitActual, commitExpected, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                results.Add(new Result(s.Round, false, commitExpected, null, false, null, null,
                    $"Lỗi parse RServer hex: {ex.Message}"));
                continue;
            }

            bool masterSeedOk = false;
            string? masterSeedExpected = s.MasterSeed?.ToLowerInvariant();
            string? masterSeedActual = null;

            try
            {
                var rServerBytes = Convert.FromHexString(s.RServer);
                var rSupBytes = s.RSupervisor is not null
                    ? Convert.FromHexString(s.RSupervisor)
                    : Array.Empty<byte>();
                var blockHashBytes = s.BlockHash is not null
                    ? Convert.FromHexString(s.BlockHash)
                    : Array.Empty<byte>();

                var combined = new byte[rServerBytes.Length + rSupBytes.Length + blockHashBytes.Length];
                Buffer.BlockCopy(rServerBytes, 0, combined, 0, rServerBytes.Length);
                Buffer.BlockCopy(rSupBytes, 0, combined, rServerBytes.Length, rSupBytes.Length);
                Buffer.BlockCopy(blockHashBytes, 0, combined, rServerBytes.Length + rSupBytes.Length, blockHashBytes.Length);

                masterSeedActual = Crypto.CryptoHelper.Sha256Hex(combined);
                masterSeedOk = string.Equals(masterSeedActual, masterSeedExpected, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                results.Add(new Result(s.Round, commitOk, commitExpected, commitActual, false, masterSeedExpected, null,
                    $"Lỗi tính MASTER_SEED: {ex.Message}"));
                continue;
            }

            results.Add(new Result(s.Round,
                commitOk, commitExpected, commitActual,
                masterSeedOk, masterSeedExpected, masterSeedActual,
                null));
        }

        return results;
    }
}
