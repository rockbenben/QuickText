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

    /// <summary>
    /// Roughly how much horizontal room <paramref name="text"/> needs, in "half-width units": CJK and
    /// other full-width characters count 2, everything else 1. Counting CHARACTERS instead makes any
    /// fits-on-one-line judgement wrong by 2× depending on the script — a 40-character Latin line and
    /// a 40-character Chinese one occupy very different widths. Deliberately a cheap approximation,
    /// not text measurement: it decides whether to show a preview, not where to break a line.
    /// </summary>
    public static int DisplayWidth(string? text)
    {
        int w = 0;
        foreach (var c in text ?? "")
        {
            // Surrogates: count the pair once (the low half adds nothing). Astral-plane characters
            // here are overwhelmingly emoji and CJK extensions, both full-width.
            if (char.IsLowSurrogate(c)) continue;
            w += char.IsHighSurrogate(c) || IsWide(c) ? 2 : 1;
        }
        return w;
    }

    /// <summary>Full-width ranges that matter in practice: CJK ideographs, kana, Hangul, and the
    /// full-width/CJK punctuation forms. Not the complete Unicode East_Asian_Width table — the extra
    /// ranges are rare enough that carrying them would cost more than the accuracy is worth.</summary>
    private static bool IsWide(char c) =>
        (c >= 'ᄀ' && c <= 'ᅟ') ||     // Hangul Jamo
        (c >= '⺀' && c <= '〾') ||     // CJK radicals, Kangxi, CJK symbols & punctuation
        (c >= 'ぁ' && c <= '㏿') ||     // kana, Hangul compat jamo, CJK compat
        (c >= '一' && c <= '鿿') ||     // CJK unified ideographs
        (c >= 'ꀀ' && c <= '꓏') ||     // Yi
        (c >= '가' && c <= '힣') ||     // Hangul syllables
        (c >= '豈' && c <= '﫿') ||     // CJK compatibility ideographs
        (c >= '︰' && c <= '﹏') ||     // CJK compatibility forms
        (c >= '＀' && c <= '｠') ||     // full-width forms
        (c >= '￠' && c <= '￦');

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
