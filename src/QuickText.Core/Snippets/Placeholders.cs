using System.Globalization;
using System.Text.RegularExpressions;

namespace QuickText.Core.Snippets;

/// <summary>
/// Snippet body placeholders: <c>{变量}</c> user fields — with an optional default
/// (<c>{姓名:张三}</c>) or an option list (<c>{环境|dev|test|prod}</c>, shown as a dropdown) —
/// plus special tokens <c>{剪贴板}</c>/<c>{clipboard}</c> (current clipboard),
/// <c>{光标}</c>/<c>{cursor}</c>, <c>{uuid}</c>, <c>{随机数}</c>/<c>{random}</c> (6 digits),
/// and date/time: <c>{日期}</c>/<c>{date}</c>, <c>{时间}</c>/<c>{time}</c>,
/// <c>{日期时间}</c>/<c>{datetime}</c>, with day offsets like <c>{日期+7}</c> and custom
/// formats like <c>{日期=yyyy年M月d日}</c> (combinable: <c>{日期+7=M月d日}</c>).
/// <para>The custom format is introduced by '=', NOT ':': ':' already means "variable default"
/// (<c>{姓名:张三}</c>), so a colon here would silently steal every existing <c>{日期:2026-01-01}</c>
/// — a prefilled date FIELD, one of this app's most common uses — and paste a garbled format of
/// it instead of prompting. '=' appears in neither the default nor the option syntax, so the two
/// forms can't collide.</para>
/// </summary>
public static class Placeholders
{
    /// <summary>A user-fillable variable: prompt label, prefill default, dropdown options (may be empty).</summary>
    public sealed record VariableSpec(string Name, string Default, IReadOnlyList<string> Options);

    private static readonly Regex Token = new(@"\{([^{}\r\n]+)\}", RegexOptions.Compiled);
    private static readonly Regex DayOffset = new(@"^(.*?)\s*([+-]\d{1,4})$", RegexOptions.Compiled);

    public static bool IsClipboard(string name) =>
        string.Equals(name, "剪贴板", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "clipboard", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Cheap check: could this body reference the clipboard token at all? Lets callers skip an
    /// unnecessary clipboard read when it can't be used. A false positive (the word appears
    /// outside braces) only costs the read they'd have done anyway; a true negative is safe.
    /// </summary>
    public static bool UsesClipboard(string body) =>
        !string.IsNullOrEmpty(body) &&
        (body.Contains("剪贴板", StringComparison.Ordinal) ||
         body.Contains("clipboard", StringComparison.OrdinalIgnoreCase));

    public static bool IsCursor(string name) =>
        string.Equals(name, "光标", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "cursor", StringComparison.OrdinalIgnoreCase);

    /// <summary>Resolve a date/time token — optionally with a ±N day offset and/or a custom
    /// .NET format (<c>{日期+7=M月d日}</c>) — or null if it isn't one / the format is invalid.
    /// <paramref name="culture"/> defaults to <see cref="CultureInfo.CurrentUICulture"/>; the
    /// UI layer passes the interface language so <c>{日期=dddd}</c> yields the weekday name
    /// in the UI's language.</summary>
    public static string? ResolveDateTime(string name, DateTime now, CultureInfo? culture = null)
        => TryResolveDateToken(name, now, culture, out var text) ? text : null;

    /// <summary>The one place a date token is turned into text — shared by <see cref="ResolveDateTime"/>
    /// and <see cref="Fill"/> so the tested path IS the pasting path. Returns whether the token is a
    /// date token AT ALL (that's what separates "not mine, try the variable pipeline" from "mine, but
    /// the format is bad"); <paramref name="text"/> is null in the latter case, so <see cref="Fill"/>
    /// can keep the token literal instead of pasting a wrong date.</summary>
    private static bool TryResolveDateToken(string name, DateTime now, CultureInfo? culture, out string? text)
    {
        text = null;
        if (!TryParseDateToken(name, out var def, out var format, out var offsetDays)) return false;
        text = FormatDate(now, offsetDays, string.IsNullOrWhiteSpace(format) ? def : format!, culture);
        return true;
    }

    /// <summary>Is this a date/time token by NAME (offset/format stripped), regardless of format
    /// validity? Keeps a bad-format date token out of the variable prompt (a null from
    /// <see cref="ResolveDateTime"/> alone would fall through to the variable pipeline).</summary>
    private static bool IsDateTimeToken(string name) => TryParseDateToken(name, out _, out _, out _);

    /// <summary>Parse <c>名字[±N][=格式]</c>. Everything after the FIRST '=' is the format string
    /// (it may itself contain '=', ':' and '/', as in HH:mm:ss); the day offset sits left of it.</summary>
    private static bool TryParseDateToken(string name, out string defaultFormat, out string? customFormat, out int offsetDays)
    {
        defaultFormat = ""; customFormat = null; offsetDays = 0;
        var n = name.Trim();
        int sep = n.IndexOf('=');
        // Both sides are trimmed: the format side too, so `{日期 = yyyy}` written for readability
        // doesn't paste a leading space (a genuinely wanted space is quotable: `{日期=' 'yyyy}`).
        if (sep >= 0) { customFormat = n[(sep + 1)..].Trim(); n = n[..sep].Trim(); }
        // Guard the NAME side only (the format is excluded — literal-heavy formats easily pass
        // 64 chars) before the DayOffset regex: its two whitespace-matching quantifiers
        // backtrack O(n²) on a long internal whitespace run (a pathological token would hang
        // the UI). No cap on the format side: a rejection there would make the token fall
        // through to the VARIABLE pipeline, prompting for a bogus field whose "default" is the
        // raw format string. Formatting is linear, so an absurd format is merely slow to fail.
        if (n.Length > 64) return false;
        var m = DayOffset.Match(n);
        if (m.Success && int.TryParse(m.Groups[2].Value, out var off)) { n = m.Groups[1].Value.Trim(); offsetDays = off; }

        if (Eq(n, "日期") || Eq(n, "date")) defaultFormat = "yyyy-MM-dd";
        else if (Eq(n, "时间") || Eq(n, "time")) defaultFormat = "HH:mm";
        else if (Eq(n, "日期时间") || Eq(n, "datetime") || Eq(n, "now")) defaultFormat = "yyyy-MM-dd HH:mm";
        else return false;
        return true;

        static bool Eq(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Format with WYSIWYG semantics: ':' and '/' outside quotes are literal characters
    /// (not the culture's separator placeholders — fi-FI would render ':' as '.'), a single-char
    /// format is custom (to .NET a lone char is a STANDARD specifier: "m" = MonthDay pattern),
    /// and the calendar is always Gregorian so formatted and default outputs never disagree on
    /// the year (th-TH's default calendar is Buddhist: 2569 vs 2026). Weekday/month NAMES still
    /// follow <paramref name="culture"/>. Null on any bad input — callers keep the token literal.</summary>
    private static string? FormatDate(DateTime now, int offsetDays, string format, CultureInfo? culture)
    {
        var f = EscapeSeparators(format);
        if (f.Length == 1) f = "%" + f;
        try
        {
            // AddDays inside the try: a huge offset near DateTime.MaxValue throws too.
            return now.AddDays(offsetDays).ToString(f, WithGregorian(culture ?? CultureInfo.CurrentUICulture));
        }
        catch (FormatException) { return null; }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    /// <summary>Quote-aware: wrap ':' and '/' that sit OUTSIDE '…'/"…" quoted sections (and not
    /// behind a '\' escape) in quotes, so .NET emits them verbatim instead of substituting the
    /// culture's time/date separator.</summary>
    private static string EscapeSeparators(string format)
    {
        if (format.IndexOf(':') < 0 && format.IndexOf('/') < 0) return format;
        var sb = new System.Text.StringBuilder(format.Length + 8);
        char quote = '\0';
        for (int i = 0; i < format.Length; i++)
        {
            char c = format[i];
            if (quote != '\0') { sb.Append(c); if (c == quote) quote = '\0'; continue; }
            if (c == '\'' || c == '"') { quote = c; sb.Append(c); continue; }
            if (c == '\\' && i + 1 < format.Length) { sb.Append(c).Append(format[++i]); continue; }
            if (c == ':' || c == '/') { sb.Append('\'').Append(c).Append('\''); continue; }
            sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>The culture with its calendar forced to Gregorian (names/digits untouched).
    /// Falls back to invariant in the theoretical case of a culture with no Gregorian calendar.</summary>
    private static CultureInfo WithGregorian(CultureInfo culture)
    {
        if (culture.DateTimeFormat.Calendar is GregorianCalendar) return culture;
        var greg = culture.OptionalCalendars.OfType<GregorianCalendar>().FirstOrDefault();
        if (greg == null) return CultureInfo.InvariantCulture;
        var clone = (CultureInfo)culture.Clone();
        clone.DateTimeFormat.Calendar = greg;
        return clone;
    }

    private static bool IsUuid(string name) => string.Equals(name, "uuid", StringComparison.OrdinalIgnoreCase);

    private static bool IsRandom(string name) =>
        string.Equals(name, "随机数", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "random", StringComparison.OrdinalIgnoreCase);

    private static bool IsSpecial(string name) =>
        IsClipboard(name) || IsCursor(name) || IsDateTimeToken(name) || IsUuid(name) || IsRandom(name)
        || TryNestedName(name, out _);   // an unresolved {片段:x} must not prompt as a variable "片段"

    /// <summary>Is this token a {片段:名称}/{snippet:name} reference? Outputs the referenced name.</summary>
    private static bool TryNestedName(string raw, out string name)
    {
        name = "";
        foreach (var p in new[] { "片段:", "snippet:" })
            if (raw.StartsWith(p, StringComparison.OrdinalIgnoreCase))
            {
                name = raw[p.Length..].Trim();
                return name.Length > 0;
            }
        return false;
    }

    // Private-use sentinels: braces of a VERBATIM (UseVariables=false) nested body are
    // swapped for these during expansion so the host's variable pipeline can't rewrite the
    // nested literal {…}; UnprotectBraces restores them as the final output step. Same
    // char length, so {光标} caret arithmetic is unaffected.
    private const char BraceOpen = '\uE000';
    private const char BraceClose = '\uE001';

    private static string ProtectBraces(string s) => s.Replace('{', BraceOpen).Replace('}', BraceClose);

    /// <summary>Restore protected braces after <see cref="Fill"/> — call as the last step.</summary>
    public static string UnprotectBraces(string s) =>
        string.IsNullOrEmpty(s) ? s : s.Replace(BraceOpen, '{').Replace(BraceClose, '}');

    /// <summary>
    /// Inline <c>{片段:名称}</c>/<c>{snippet:name}</c> references so shared blocks (signatures,
    /// footers) live in one place. Runs BEFORE variable collection, so a nested body's
    /// {变量} tokens prompt like the host's own — except when the referenced snippet has
    /// variables OFF: then its body is inlined verbatim with braces protected, honoring
    /// its opt-out. Depth-limited (cycles bottom out as literal text); unknown names stay
    /// literal.
    /// </summary>
    // Total {片段} inlinings allowed per expansion. A depth limit alone bounds recursion depth
    // but NOT fan-out: a body may hold thousands of refs, so nested refs amplify as
    // refs_per_body ^ depth. Without this a few small snippets can expand to gigabytes
    // (OutOfMemory / hang). Far above any real nesting (signatures/footers use a handful).
    private const int MaxExpansions = 2000;

    public static string ExpandSnippets(
        string body, Func<string, (string Body, bool UseVariables)?> lookup, int maxDepth = 3)
        => ExpandSnippets(body, lookup, maxDepth, new[] { MaxExpansions });

    private static string ExpandSnippets(
        string body, Func<string, (string Body, bool UseVariables)?> lookup, int maxDepth, int[] budget)
    {
        if (string.IsNullOrEmpty(body) || maxDepth <= 0) return body ?? "";
        return Token.Replace(body, m =>
        {
            var raw = m.Groups[1].Value.Trim();
            if (!TryNestedName(raw, out var name)) return m.Value;
            // Budget spent: leave the ref as literal text (like the depth-limit / unknown-name
            // paths) rather than deleting it — a resource guard must degrade visibly, not eat
            // authored content silently.
            if (budget[0] <= 0) return m.Value;
            var nested = lookup(name);
            if (nested == null) return m.Value;
            budget[0]--;
            var (nb, useVars) = nested.Value;
            return useVars ? ExpandSnippets(nb, lookup, maxDepth - 1, budget) : ProtectBraces(nb);
        });
    }

    /// <summary>
    /// Parse a user-variable token body: <c>name</c>, <c>name:default</c>, or
    /// <c>name|opt1|opt2</c> (options; the first option doubles as the default).
    /// </summary>
    private static VariableSpec ParseSpec(string raw)
    {
        if (raw.Contains('|'))
        {
            var parts = raw.Split('|', StringSplitOptions.TrimEntries);
            var options = parts.Skip(1).Where(p => p.Length > 0).ToList();
            return new VariableSpec(parts[0], options.Count > 0 ? options[0] : "", options);
        }
        int colon = raw.IndexOf(':');
        return colon < 0
            ? new VariableSpec(raw, "", Array.Empty<string>())
            : new VariableSpec(raw[..colon].Trim(), raw[(colon + 1)..].Trim(), Array.Empty<string>());
    }

    /// <summary>Distinct user-fillable variables (excludes special tokens), in first-seen order.</summary>
    public static IReadOnlyList<VariableSpec> VariableSpecs(string body)
    {
        var list = new List<VariableSpec>();
        var seen = new HashSet<string>();
        foreach (Match m in Token.Matches(body ?? ""))
        {
            var raw = m.Groups[1].Value.Trim();
            if (raw.Length == 0 || IsSpecial(raw)) continue;
            var spec = ParseSpec(raw);
            if (spec.Name.Length == 0) continue;
            if (seen.Add(spec.Name)) list.Add(spec);   // first occurrence's default/options win
        }
        return list;
    }

    /// <summary>Distinct user-fillable variable names, in first-seen order.</summary>
    public static IReadOnlyList<string> Variables(string body) =>
        VariableSpecs(body).Select(s => s.Name).ToList();

    public static bool HasVariables(string body) => Variables(body).Count > 0;

    /// <summary>Any {…} token at all — what the legacy always-on pipeline would have acted on.</summary>
    public static bool HasAnyToken(string body) => !string.IsNullOrEmpty(body) && Token.IsMatch(body);

    /// <summary>
    /// Like <see cref="Fill"/>, but also returns how many characters lie after the
    /// <c>{光标}</c> marker in the result — i.e. how many times to press Left after
    /// pasting so the caret lands where the marker was — plus whether a cursor token
    /// was present at all (a TRAILING marker yields CaretFromEnd 0 yet still means
    /// "the user wants to keep typing here", e.g. to suppress auto-send).
    /// </summary>
    public static (string Text, int CaretFromEnd, bool HasCursor) FillWithCaret(
        string body, IReadOnlyDictionary<string, string>? values, string clipboard = "",
        DateTime? now = null, CultureInfo? culture = null)
    {
        if (string.IsNullOrEmpty(body)) return (body ?? "", 0, false);
        // ONE stamp shared by both halves — two DateTime.Now reads straddling a minute or
        // midnight boundary would paste two different times in a single snippet.
        var stamp = now ?? DateTime.Now;

        Match? cursor = null;
        foreach (Match m in Token.Matches(body))
            if (IsCursor(m.Groups[1].Value.Trim())) { cursor = m; break; }

        if (cursor == null) return (Fill(body, values, clipboard, stamp, culture), 0, false);

        var before = Fill(body.Substring(0, cursor.Index), values, clipboard, stamp, culture);
        var after = Fill(body.Substring(cursor.Index + cursor.Length), values, clipboard, stamp, culture);
        // A CRLF line break is a SINGLE caret stop in the target app even though it's two
        // chars — count it once so the Left-key walk doesn't overshoot the marker by one per
        // line on multi-line snippets (the paste itself keeps the original text unchanged).
        int caretFromEnd = after.Replace("\r\n", "\n").Length;
        return (before + after, caretFromEnd, true);
    }

    /// <summary>Substitute all tokens. Clipboard/cursor/date-time resolve automatically; unknown variables are left as-is.</summary>
    public static string Fill(string body, IReadOnlyDictionary<string, string>? values, string clipboard = "",
        DateTime? now = null, CultureInfo? culture = null)
    {
        if (string.IsNullOrEmpty(body)) return body ?? "";
        var stamp = now ?? DateTime.Now;
        return Token.Replace(body, m =>
        {
            var raw = m.Groups[1].Value.Trim();
            if (IsCursor(raw)) return "";
            if (IsClipboard(raw)) return clipboard ?? "";
            if (IsUuid(raw)) return Guid.NewGuid().ToString();          // fresh per occurrence
            if (IsRandom(raw)) return Random.Shared.Next(100000, 1000000).ToString();
            if (TryResolveDateToken(raw, stamp, culture, out var date))
                return date ?? m.Value;                  // bad format / out of range: stay literal
            if (TryNestedName(raw, out _)) return m.Value;   // unresolved reference stays literal
            var spec = ParseSpec(raw);
            if (spec.Name.Length == 0) return m.Value;       // {:x}/{|a|b}: never prompted, so stay literal
            if (values != null && values.TryGetValue(spec.Name, out var v)) return v;
            return spec.Default.Length > 0 ? spec.Default : m.Value;    // unfilled: default, else literal
        });
    }
}
