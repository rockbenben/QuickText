namespace QuickText.Core;

/// <summary>
/// Works out which way a piece of user-supplied text reads, so the UI can render it in its own
/// direction instead of the layout's.
/// <para>Snippet names and bodies are arbitrary user data: a Chinese name in an Arabic UI, an
/// Arabic name in a Chinese one. The bidirectional algorithm resolves each paragraph in a single
/// base direction, so an LTR name shown in an RTL layout has its leading digits pulled to the
/// visual end — "365 README 备注" displays as "README 备注 365". The user's own data is not the
/// UI's to rearrange.</para>
/// <para>The textual fixes do not work here, both measured against a real render: Unicode 6.3
/// isolates (U+2068/U+2069) changed nothing, and neither did the older embedding controls
/// (U+202A/U+202B/U+202C) — WPF's text stack drops both. What it does honour is the
/// <c>FlowDirection</c> property, so callers feed this predicate into that.</para>
/// </summary>
public static class BidiText
{
    /// <summary>
    /// Does <paramref name="text"/> read right-to-left? Decided by its FIRST STRONG character, the
    /// same rule the Unicode algorithm uses to pick a paragraph direction. Digits, spaces and
    /// punctuation are weak and carry no direction, so they are skipped — which is exactly why
    /// "365 README" reads left-to-right despite starting with a number.
    /// </summary>
    public static bool IsRightToLeft(string? text)
    {
        foreach (var c in text ?? "")
        {
            if (IsStrongRtl(c)) return true;
            if (char.IsLetter(c)) return false;   // any other letter is a strong LTR character
        }
        return false;   // no strong character at all (digits/punctuation only) — treat as LTR
    }

    /// <summary>The right-to-left scripts, by block. Presentation forms are included because text
    /// pasted from older documents still arrives in them.</summary>
    private static bool IsStrongRtl(char c) =>
        (c >= '֐' && c <= 'ࣿ') ||     // Hebrew, Arabic, Syriac, Thaana, NKo, Samaritan
        (c >= 'יִ' && c <= '﷿') ||     // Hebrew + Arabic presentation forms A
        (c >= 'ﹰ' && c <= 'ﻼ');       // Arabic presentation forms B
}
