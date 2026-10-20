using System.Security.Cryptography;

namespace QuickText.Core;

/// <summary>
/// Compares a GitHub release tag against the running version, and picks the right file out of a
/// release. Pure (no network) so the version and asset logic is unit-tested; the App does the
/// actual HTTP fetch and passes the tag in here.
/// </summary>
public static class UpdateCheck
{
    /// <summary>
    /// True if the release tag <paramref name="latestTag"/> (e.g. "v1.2.0") is a strictly newer
    /// version than <paramref name="current"/> (e.g. "1.0.0"). Tolerates a leading 'v', differing
    /// component counts (missing parts are 0), and a pre-release/build suffix ("1.2.0-beta",
    /// "1.2.0+abc"). Anything unparsable returns false, so garbage never reports a phantom update.
    /// </summary>
    public static bool IsNewer(string? latestTag, string? current)
    {
        if (!TryParse(latestTag, out var latest) || !TryParse(current, out var cur)) return false;
        for (int i = 0; i < System.Math.Max(latest.Length, cur.Length); i++)
        {
            int a = i < latest.Length ? latest[i] : 0;
            int b = i < cur.Length ? cur[i] : 0;
            if (a != b) return a > b;
        }
        return false;   // equal
    }

    /// <summary>
    /// The release tag without its leading 'v' — the number the app shows as its own version, so
    /// the folder a download is unpacked into is named after the version the user can see.
    /// </summary>
    public static string NormalizeVersion(string? tag)
    {
        var s = (tag ?? "").Trim();
        return s.Length > 0 && (s[0] == 'v' || s[0] == 'V') ? s[1..] : s;
    }

    /// <summary>
    /// The self-contained build among a release's assets, or null when there is none.
    /// <para>A release carries two zips: the self-contained one (the .NET runtime is inside it, so
    /// it runs on a machine with no .NET installed) and a framework-dependent one (the runtime has
    /// to be installed separately). Only the first can be handed to a user as "just run this", so
    /// the framework-dependent one is never picked — a user who took it would get an exe that
    /// refuses to start with a message about a missing runtime.</para>
    /// </summary>
    public static string? SelectSelfContainedAsset(IEnumerable<string> assetNames)
    {
        foreach (var name in assetNames)
        {
            if (!name.EndsWith("-win-x64.zip", StringComparison.OrdinalIgnoreCase)) continue;
            if (name.Contains("-needs-dotnet10", StringComparison.OrdinalIgnoreCase)) continue;
            return name;
        }
        return null;
    }

    /// <summary>
    /// The published SHA-256 digests, keyed by file name. The format is the one
    /// <c>sha256sum</c> writes: the digest, whitespace, then the file name — the name may carry a
    /// leading '*' (binary mode). Lines that are not a digest plus a name are skipped rather than
    /// guessed at: a release also ships a README and a licence, and those lines are not ours.
    /// </summary>
    public static Dictionary<string, string> ParseChecksums(string? text)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in (text ?? "").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            int sep = line.IndexOfAny(new[] { ' ', '\t' });
            if (sep <= 0) continue;
            var digest = line[..sep].Trim();
            var name = line[(sep + 1)..].Trim();
            if (name.StartsWith('*')) name = name[1..];
            if (name.Length == 0 || digest.Length != 64) continue;
            if (!digest.All(Uri.IsHexDigit)) continue;
            map[name] = digest.ToLowerInvariant();
        }
        return map;
    }

    /// <summary>The file's SHA-256 as lowercase hex.</summary>
    public static string ComputeSha256Hex(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
    }

    /// <summary>
    /// True when the digest of the file we downloaded is the one the release published. Compared
    /// case-insensitively: hex digests are printed in both cases and the letter case carries no
    /// meaning.
    /// </summary>
    public static bool MatchesChecksum(string? expected, string? actual)
        => !string.IsNullOrWhiteSpace(expected)
           && !string.IsNullOrWhiteSpace(actual)
           && string.Equals(expected.Trim(), actual.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool TryParse(string? s, out int[] parts)
    {
        parts = System.Array.Empty<int>();
        if (string.IsNullOrWhiteSpace(s)) return false;
        s = s.Trim().TrimStart('v', 'V');
        int cut = s.IndexOfAny(new[] { '-', '+', ' ' });   // drop pre-release / build metadata
        if (cut >= 0) s = s[..cut];
        var segs = s.Split('.');
        var nums = new int[segs.Length];
        for (int i = 0; i < segs.Length; i++)
            if (!int.TryParse(segs[i], out nums[i]) || nums[i] < 0) return false;
        parts = nums;
        return nums.Length > 0;
    }
}
