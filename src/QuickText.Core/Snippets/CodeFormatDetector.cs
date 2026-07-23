using System.Text.Json;
using System.Text.RegularExpressions;

namespace QuickText.Core.Snippets;

/// <summary>
/// Best-effort guess at a body's code format, from structural signals only. Returns a
/// <see cref="CodeLanguages"/> id, or "" when nothing is confident enough — "" is the correct
/// answer for ordinary prose and must be the default, because a wrong guess colours a user's
/// plain text as if it were code.
///
/// Deliberately narrow: only JSON, shell (<c>#!</c>), XML/HTML and INI have a structural signal
/// strong enough to beat ordinary prose without false positives. Python, SQL, Markdown, C#, Java,
/// PowerShell, YAML and JavaScript are NOT detected — none of them has a shape that can't equally
/// well be a paragraph of prose or a snippet placeholder, and a false positive (painting the
/// user's plain text as code) is exactly the failure this detector exists to avoid.
///
/// Callers must treat the result as a RENDERING hint only — see <c>BodyEditorWindow</c>, the one
/// caller, for the "guess but never persist" rule this exists to satisfy.
/// </summary>
public static class CodeFormatDetector
{
    /// <summary>Longest single line, in characters, that the code editor can render responsively.
    /// Beyond this its word-wrap layout degrades super-linearly (measured: one 46 KB line took 14 s
    /// to open, one 96 KB line took 59 s). Minified content is exactly this shape — and it gains
    /// nothing from syntax colouring anyway, so detection declines it rather than routing the user
    /// into an editor that will hang.</summary>
    public const int MaxLineLengthForCode = 4000;

    // A [section] header line: brackets with something inside, nothing but whitespace outside them.
    private static readonly Regex IniSectionHeader = new(@"^\s*\[[^\]\r\n]+\]\s*$", RegexOptions.Compiled);

    // A minimal "<tag ...>" or "<tag/>" opening shape, case-insensitive by construction (tag names
    // are matched as-is; callers already special-case the doctype/html signals separately).
    private static readonly Regex XmlTagShape = new(@"^<\s*[A-Za-z][\w:.-]*(\s[^<>]*)?/?>", RegexOptions.Compiled);

    /// <summary>Length of the longest line in <paramref name="body"/>, treating <c>\r\n</c> as a
    /// single break. Scans without allocating (no <c>Split</c>) so it's cheap to call even on
    /// bodies at the size cap. Shared by <see cref="Detect"/> (A1: decline detection past
    /// <see cref="MaxLineLengthForCode"/>) and the App layer's <c>BodyEditorWindow</c> (A2: fall
    /// back to plain text even for an explicitly chosen format), so the two never drift apart.</summary>
    public static int LongestLineLength(string? body)
    {
        if (string.IsNullOrEmpty(body)) return 0;
        int longest = 0, current = 0;
        foreach (var c in body)
        {
            if (c == '\n')
            {
                if (current > longest) longest = current;
                current = 0;
            }
            else if (c != '\r')
            {
                current++;
            }
        }
        if (current > longest) longest = current;
        return longest;
    }

    /// <summary>Guess a format from body structure alone. Never throws.</summary>
    public static string Detect(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return "";
        // A huge body can't be worth a full JSON parse (or is even worth scanning at all) — mirrors
        // PlaceholderScanner's own size cap for the same reason: past this size the editor is
        // already degrading to plain text, so there is nothing left to light up anyway.
        if (body.Length > PlaceholderScanner.MaxScanLength) return "";
        // A pathologically long single line is minified content — it parses fine as JSON but hangs
        // the code editor's word-wrap layout (see MaxLineLengthForCode). Decline BEFORE the JSON
        // parse below, so a huge minified body doesn't even pay for parsing.
        if (LongestLineLength(body) > MaxLineLengthForCode) return "";

        var trimmed = body.TrimStart();
        if (trimmed.Length == 0) return "";

        // 1. JSON — a leading '{' or '[' is necessary but NOT sufficient: snippet bodies very often
        // start with a placeholder like "{姓名}你好", which also starts with '{' but is plain text.
        // The parse is mandatory; it is what actually decides.
        if (trimmed[0] == '{' || trimmed[0] == '[')
        {
            try
            {
                JsonDocument.Parse(body);
                return "json";
            }
            catch (JsonException)
            {
                // Looked like JSON, wasn't — fall through to the other signals.
            }
        }

        // 2. Shell — a shebang naming a POSIX-ish shell.
        if (body.StartsWith("#!", StringComparison.Ordinal))
        {
            var firstLine = FirstLine(body);
            if (firstLine.Contains("sh", StringComparison.OrdinalIgnoreCase)
                || firstLine.Contains("bash", StringComparison.OrdinalIgnoreCase)
                || firstLine.Contains("zsh", StringComparison.OrdinalIgnoreCase))
                return "shell";
        }

        // 3. XML / HTML
        if (trimmed[0] == '<')
        {
            var head = trimmed.Length > 200 ? trimmed[..200] : trimmed;
            if (head.Contains("<!doctype html", StringComparison.OrdinalIgnoreCase)
                || head.Contains("<html", StringComparison.OrdinalIgnoreCase))
                return "html";
            if (trimmed.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase)
                || XmlTagShape.IsMatch(trimmed))
                return "xml";
        }

        // 4. INI — the first non-blank, non-comment line is a [section] header.
        foreach (var line in SplitLines(body))
        {
            var t = line.Trim();
            if (t.Length == 0) continue;
            if (t[0] == '#' || t[0] == ';') continue;
            return IniSectionHeader.IsMatch(line) ? "ini" : "";
        }

        return "";
    }

    private static string FirstLine(string s)
    {
        int nl = s.IndexOf('\n');
        var line = nl >= 0 ? s[..nl] : s;
        return line.TrimEnd('\r');
    }

    private static IEnumerable<string> SplitLines(string s)
    {
        int start = 0;
        for (int i = 0; i <= s.Length; i++)
        {
            if (i == s.Length || s[i] == '\n')
            {
                var len = i - start;
                if (len > 0 && s[i - 1] == '\r') len--;
                yield return s.Substring(start, len);
                start = i + 1;
            }
        }
    }
}
