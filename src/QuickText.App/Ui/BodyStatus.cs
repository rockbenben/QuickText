using System;
using System.Collections.Generic;
using System.Linq;
using QuickText.Core.Localization;
using QuickText.Core.Snippets;

namespace QuickText.App.Ui;

/// <summary>
/// The body editors' status line: counts and the localized text. Shared by BodyEditor (plain
/// text) and CodeEditor (AvalonEdit) so the two can't drift — these boundary rules (CRLF, caret
/// at end, over-cap degradation) were settled once and must stay settled once.
/// </summary>
internal static class BodyStatus
{
    /// <summary>Logical lines (what the gutter numbers), independent of soft wrapping.</summary>
    public static int LogicalLineCount(string s)
    {
        if (s.Length == 0) return 1;
        int n = 1;
        for (int i = 0; i < s.Length; i++) if (s[i] == '\n') n++;
        return n;
    }

    /// <summary>Characters excluding line breaks — "字数" as a user counts it.</summary>
    public static int CharCount(string s)
    {
        int n = 0;
        for (int i = 0; i < s.Length; i++) if (s[i] != '\r' && s[i] != '\n') n++;
        return n;
    }

    public static int CaretLine(string s, int caret)
    {
        int n = 0;
        for (int i = 0; i < caret && i < s.Length; i++) if (s[i] == '\n') n++;
        return n;
    }

    public static int CaretColumn(string s, int caret)
    {
        int c = Math.Min(caret, s.Length);
        int start = c;
        while (start > 0 && s[start - 1] != '\n') start--;
        return c - start;
    }

    /// <param name="varsOffTokenCount">-1 when the body is over the scan cap and was not counted.</param>
    public static string Compose(string text, int caret, int lineCount, int charCount,
        int varsOffTokenCount, bool useVariables, IReadOnlyList<TokenSpan> spans)
    {
        bool overCap = text.Length > PlaceholderScanner.MaxScanLength;
        var parts = new List<string>
        {
            string.Format(L("Manager.BodyStats"), lineCount, charCount),
            string.Format(L("Manager.BodyCaret"), CaretLine(text, caret) + 1, CaretColumn(text, caret) + 1),
        };

        // Over the cap `spans` is always empty, which would otherwise read as "no variables,
        // 0 invalid" — indistinguishable from a body that WAS scanned and found clean. Omit the
        // segments so the status line degrades to counts + caret position instead of lying.
        if (!overCap)
        {
            if (!useVariables)
            {
                if (varsOffTokenCount > 0) parts.Add(string.Format(L("Manager.BodyVarsOff"), varsOffTokenCount));
            }
            else
            {
                var vars = spans.Where(s => s.Kind == TokenKind.Variable).Select(s => s.Name).Distinct().ToList();
                if (vars.Count > 0) parts.Add(string.Format(L("Manager.BodyVars"), string.Join("、", vars)));
                int bad = spans.Count(s => s.Kind == TokenKind.Invalid);
                if (bad > 0) parts.Add(string.Format(L("Manager.BodyInvalid"), bad));
            }
        }

        return string.Join(" · ", parts);
    }

    /// <summary>Token count for the placeholders-off message; -1 above the scan cap (not counted).</summary>
    public static int CountTokensForVarsOff(string text) =>
        text.Length <= PlaceholderScanner.MaxScanLength ? Placeholders.TokenMatches(text).Count() : -1;

    /// <summary>Maps an invalid token's <see cref="InvalidReason"/> to its localized explanation.
    /// Only <see cref="InvalidReason.UnknownSnippet"/> takes an argument — the referenced snippet
    /// name; the other reasons' <c>Name</c> is raw source text and must not be interpolated in.
    /// Shared by BodyEditor and CodeEditor's hover tooltips so the two can't drift.</summary>
    internal static string? InvalidHint(TokenSpan span) => span.Reason switch
    {
        InvalidReason.UnknownSnippet => string.Format(L("Manager.Token.UnknownSnippet"), span.Name),
        InvalidReason.BadDateFormat => L("Manager.Token.BadDateFormat"),
        InvalidReason.EmptyName => L("Manager.Token.EmptyName"),
        InvalidReason.Unclosed => L("Manager.Token.Unclosed"),
        _ => null,
    };

    private static string L(string key) => LocalizationService.Instance[key];
}
