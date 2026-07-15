namespace QuickText.Core;

/// <summary>
/// Compares a GitHub release tag against the running version. Pure (no network) so the version
/// logic is unit-tested; the App does the actual HTTP fetch and passes the tag in here.
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
