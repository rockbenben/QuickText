using System.Globalization;

namespace QuickText.Core.Snippets;

/// <summary>What a <c>{…}</c> token is, for editor highlighting. Mirrors exactly what
/// <see cref="Placeholders.Fill"/> will do with it at send time.</summary>
public enum TokenKind
{
    /// <summary>A user-fillable field: prompts a dialog at send time.</summary>
    Variable,
    /// <summary>Resolved automatically: date/time, uuid, random, clipboard.</summary>
    Auto,
    /// <summary>A <c>{片段:名称}</c> reference to another snippet's body.</summary>
    Nested,
    /// <summary>The <c>{光标}</c> caret marker.</summary>
    Cursor,
    /// <summary>Written like a token but pasted VERBATIM at send time — the silent failure
    /// the editor exists to surface. See <see cref="InvalidReason"/>.</summary>
    Invalid,
}

/// <summary>Why a token is <see cref="TokenKind.Invalid"/>. An enum, not a message: Core holds
/// no UI copy — the App layer maps these to the localized <c>Manager.Token.*</c> strings.</summary>
public enum InvalidReason
{
    None,
    /// <summary><c>{片段:x}</c> where no snippet named x exists.</summary>
    UnknownSnippet,
    /// <summary><c>{日期=Q}</c> — a date token whose custom format .NET rejects.</summary>
    BadDateFormat,
    /// <summary><c>{:张三}</c> / <c>{|a|b}</c> — no name, so it can never be prompted.</summary>
    EmptyName,
    /// <summary>A <c>{</c> with no closing <c>}</c> on the same line.</summary>
    Unclosed,
}

/// <summary>One highlightable region of a snippet body.</summary>
/// <param name="Start">Index of the opening brace in the body.</param>
/// <param name="Length">Length INCLUDING both braces — this is what gets drawn.</param>
/// <param name="Name">Normalized name: the variable/token name, the referenced snippet name for
/// <see cref="TokenKind.Nested"/>, or "{" for an unclosed brace.</param>
/// <param name="HasOptions">Variable written with an option list (<c>{环境|a|b}</c>).</param>
/// <param name="Preview">A date token's resolved text (e.g. "7月30日"); null for every other kind.</param>
/// <param name="Reason">Always <see cref="InvalidReason.None"/> unless Kind is Invalid.</param>
public sealed record TokenSpan(
    int Start,
    int Length,
    TokenKind Kind,
    string Name,
    bool HasOptions,
    string? Preview,
    InvalidReason Reason);

/// <summary>
/// Turns a snippet body into the spans an editor highlights. Pure logic: the rules all come from
/// <see cref="Placeholders"/>, so what is highlighted and what is pasted can never disagree.
/// </summary>
public static class PlaceholderScanner
{
    /// <summary>Bodies longer than this are not scanned at all (empty result). A snippet is a
    /// snippet; past this size the per-keystroke rescan would cost more than the highlight is
    /// worth, and the editor degrades to plain text instead of stuttering.</summary>
    public const int MaxScanLength = 100_000;

    private static readonly TokenSpan[] None = Array.Empty<TokenSpan>();

    /// <param name="snippetExists">Resolves a <c>{片段:名称}</c> target. Null means "can't check" —
    /// references then stay <see cref="TokenKind.Nested"/> rather than being cried wolf on.</param>
    /// <param name="now">Clock for date previews. Null = <see cref="DateTime.Now"/>.</param>
    /// <param name="culture">Culture for weekday/month names in date previews.</param>
    public static IReadOnlyList<TokenSpan> Scan(
        string? body,
        Func<string, bool>? snippetExists = null,
        DateTime? now = null,
        CultureInfo? culture = null)
    {
        if (string.IsNullOrEmpty(body) || body!.Length > MaxScanLength) return None;

        var stamp = now ?? DateTime.Now;
        var spans = new List<TokenSpan>();
        // Which chars belong to a well-formed token — everything else is scanned for dangling '{'.
        var claimed = new bool[body.Length];

        foreach (var (index, length, rawInner) in Placeholders.TokenMatches(body))
        {
            for (int i = index; i < index + length; i++) claimed[i] = true;

            var raw = rawInner.Trim();
            var kind = Placeholders.Classify(raw);
            var name = raw;
            var reason = InvalidReason.None;
            bool hasOptions = false;
            string? preview = null;

            switch (kind)
            {
                case TokenKind.Invalid:
                    // Classify only returns Invalid for an unnameable token.
                    reason = InvalidReason.EmptyName;
                    break;

                case TokenKind.Nested:
                    name = Placeholders.NestedName(raw) ?? raw;
                    if (snippetExists != null && !snippetExists(name))
                    {
                        kind = TokenKind.Invalid;
                        reason = InvalidReason.UnknownSnippet;
                    }
                    break;

                case TokenKind.Auto:
                    if (Placeholders.IsDateToken(raw))
                    {
                        preview = Placeholders.ResolveDateTime(raw, stamp, culture);
                        if (preview == null)
                        {
                            // A date token by name whose format .NET rejects: Fill() keeps it
                            // literal, so the user would paste the token instead of a date.
                            kind = TokenKind.Invalid;
                            reason = InvalidReason.BadDateFormat;
                        }
                    }
                    break;

                case TokenKind.Variable:
                    var spec = Placeholders.ParseVariable(raw);
                    name = spec.Name;
                    hasOptions = spec.Options.Count > 0;
                    break;
            }

            spans.Add(new TokenSpan(index, length, kind, name, hasOptions, preview, reason));
        }

        // Dangling '{' — a token pattern excludes '{' from its inner text, so an unclaimed '{'
        // is always one the user opened and never closed on that line.
        for (int i = 0; i < body.Length; i++)
        {
            if (body[i] != '{' || claimed[i]) continue;
            // "{}" never matches the token regex (it requires at least one inner char), so both
            // braces fall through here unclaimed. It IS closed — the real problem is there's no
            // name inside to fill — so report EmptyName, not Unclosed, and cover both braces in
            // one 2-char span rather than flagging the '{' as dangling.
            if (i + 1 < body.Length && body[i + 1] == '}')
            {
                spans.Add(new TokenSpan(i, 2, TokenKind.Invalid, "{}", false, null, InvalidReason.EmptyName));
                i++;   // both braces consumed by this one span
                continue;
            }
            spans.Add(new TokenSpan(i, 1, TokenKind.Invalid, "{", false, null, InvalidReason.Unclosed));
        }

        spans.Sort(static (a, b) => a.Start.CompareTo(b.Start));
        return spans;
    }
}
