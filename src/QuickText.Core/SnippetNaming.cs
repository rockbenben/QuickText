namespace QuickText.Core;

/// <summary>Derives a listable snippet name from free text, and the shared surrogate-safe truncation
/// used wherever text is capped for display. Shared so the search-panel "create", clipboard-capture,
/// and preview paths can't drift.</summary>
public static class SnippetNaming
{
    private const int MaxNameLength = 20;

    /// <summary>The first non-blank line of <paramref name="text"/>, trimmed and capped to a readable
    /// length with an ellipsis. Skipping leading blank lines means pasted text that starts with a
    /// newline still yields its real first line rather than an empty name. Returns "" only when there
    /// is no non-blank line, so callers can fall back to a default label.</summary>
    public static string FromFirstLine(string? text)
    {
        var firstLine = "";
        foreach (var line in (text ?? "").Split('\r', '\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length > 0) { firstLine = trimmed; break; }
        }
        return Ellipsize(firstLine, MaxNameLength);
    }

    /// <summary>Truncate <paramref name="text"/> to at most <paramref name="max"/> chars and append
    /// <paramref name="ellipsis"/> — never cutting between a UTF-16 surrogate pair (which would leave
    /// a lone surrogate rendered as �). Returns the text unchanged when it already fits.</summary>
    public static string Ellipsize(string text, int max, string ellipsis = "…")
    {
        if (text.Length <= max) return text;
        int cut = System.Math.Max(0, max);
        if (cut > 0 && char.IsHighSurrogate(text[cut - 1])) cut--;   // don't split a surrogate pair (guarded so max<=0 can't index text[-1])
        return text[..cut] + ellipsis;
    }
}
