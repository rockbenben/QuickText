using System.Globalization;
using QuickText.Core.Snippets;

namespace QuickText.Core.Tests;

public class PlaceholdersTests
{
    [Fact]
    public void Extracts_distinct_user_variables_in_order()
    {
        var vars = Placeholders.Variables("您好 {昵称}，订单 {订单号} 给 {昵称} 已发货");
        Assert.Equal(new[] { "昵称", "订单号" }, vars);
    }

    [Fact]
    public void Special_tokens_are_not_user_variables()
    {
        var vars = Placeholders.Variables("{剪贴板} {昵称} {光标} {clipboard}");
        Assert.Equal(new[] { "昵称" }, vars);
        Assert.False(Placeholders.HasVariables("仅 {剪贴板} 和 {光标}"));
    }

    // A CRLF line break is a single caret stop in the target app; the Left-walk count must
    // treat it as one, not two, or a multi-line snippet lands the caret a line too far left.
    [Fact]
    public void FillWithCaret_counts_crlf_as_one_caret_stop()
    {
        var (_, caret, hasCursor) = Placeholders.FillWithCaret("报告{光标}\r\n签名", null);
        Assert.True(hasCursor);
        Assert.Equal(3, caret);   // "\r\n签名" = one newline stop + 签 + 名, not 4 (\r,\n,签,名)
    }

    [Fact]
    public void FillWithCaret_plain_text_after_marker_counts_each_char()
    {
        var (_, caret, _) = Placeholders.FillWithCaret("你好{光标}abc", null);
        Assert.Equal(3, caret);
    }

    // Nested {片段} fan-out is bounded so a few small snippets can't expand to gigabytes
    // (OutOfMemory). Depth-3 × 100-wide would be 100^3 leaves without the budget.
    [Fact]
    public void ExpandSnippets_bounds_nested_fanout()
    {
        var bodies = new Dictionary<string, (string, bool)>
        {
            ["z"] = (new string('x', 1000), true),
            ["c"] = (string.Concat(Enumerable.Repeat("{片段:z}", 100)), true),
            ["b"] = (string.Concat(Enumerable.Repeat("{片段:c}", 100)), true),
        };
        var top = string.Concat(Enumerable.Repeat("{片段:b}", 100));
        var result = Placeholders.ExpandSnippets(top,
            n => bodies.TryGetValue(n, out var v) ? v : ((string, bool)?)null);
        // Without the cap this allocates ~100^3 * 1KB and OOMs; the budget keeps it bounded.
        Assert.True(result.Length < 5_000_000, $"expansion not bounded: {result.Length}");
    }

    // When the fan-out budget is spent, over-budget refs must stay VISIBLE as literal tokens
    // (like the depth-limit / unknown-name paths) — never silently deleted.
    [Fact]
    public void ExpandSnippets_budget_exhaustion_leaves_refs_literal_not_deleted()
    {
        var body = string.Concat(Enumerable.Repeat("{片段:x}", 2100));   // > MaxExpansions (2000)
        var result = Placeholders.ExpandSnippets(body,
            n => n == "x" ? ("A", true) : ((string, bool)?)null);
        // First 2000 expand to "A"; the rest remain the literal token — nothing vanishes.
        Assert.Equal(2000, System.Text.RegularExpressions.Regex.Matches(result, "A").Count);
        Assert.Contains("{片段:x}", result);
    }

    [Fact]
    public void ExpandSnippets_still_inlines_normal_nesting()
    {
        var bodies = new Dictionary<string, (string, bool)>
        {
            ["sig"] = ("此致\n敬礼", true),
        };
        var result = Placeholders.ExpandSnippets("落款：{片段:sig}",
            n => bodies.TryGetValue(n, out var v) ? v : ((string, bool)?)null);
        Assert.Equal("落款：此致\n敬礼", result);
    }

    // A pathological long token must not send the day-offset regex into O(n^2) backtracking.
    // The length guard returns immediately; a Stopwatch bound catches a regression to the regex.
    [Fact]
    public void ResolveDateTime_does_not_hang_on_long_internal_whitespace()
    {
        var name = "a" + new string(' ', 200_000) + "b";
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = Placeholders.ResolveDateTime(name, System.DateTime.Now);
        sw.Stop();
        Assert.Null(result);
        Assert.True(sw.ElapsedMilliseconds < 500, $"took {sw.ElapsedMilliseconds}ms — regex backtracking?");
    }

    [Fact]
    public void ResolveDateTime_still_parses_short_offsets()
    {
        var d = new System.DateTime(2026, 1, 1);
        Assert.Equal("2026-01-08", Placeholders.ResolveDateTime("日期+7", d));
        Assert.Equal("2026-01-01", Placeholders.ResolveDateTime("date", d));
    }

    [Fact]
    public void UsesClipboard_gates_the_clipboard_read()
    {
        Assert.True(Placeholders.UsesClipboard("你好 {剪贴板}"));
        Assert.True(Placeholders.UsesClipboard("hi {clipboard} there"));
        Assert.False(Placeholders.UsesClipboard("你好 {昵称}，{日期}"));
        Assert.False(Placeholders.UsesClipboard(""));
    }

    [Fact]
    public void Fill_substitutes_variables_clipboard_and_removes_cursor()
    {
        var body = "您好 {昵称}，单号 {剪贴板}{光标} 已处理";
        var outp = Placeholders.Fill(body, new Dictionary<string, string> { ["昵称"] = "小明" }, "A123");
        Assert.Equal("您好 小明，单号 A123 已处理", outp);
    }

    [Fact]
    public void Fill_leaves_unknown_variables_untouched()
    {
        var outp = Placeholders.Fill("{昵称}-{未知}", new Dictionary<string, string> { ["昵称"] = "x" });
        Assert.Equal("x-{未知}", outp);
    }

    [Fact]
    public void FillWithCaret_returns_chars_after_cursor()
    {
        var (text, caret, hasCursor) = Placeholders.FillWithCaret(
            "你好{光标}，{昵称}", new Dictionary<string, string> { ["昵称"] = "小明" });
        Assert.Equal("你好，小明", text);
        Assert.Equal(3, caret);   // "，小明" trails the caret
        Assert.True(hasCursor);
    }

    [Fact]
    public void FillWithCaret_trailing_cursor_reports_HasCursor_despite_zero_offset()
    {
        // Callers use HasCursor to suppress auto-Enter; a trailing marker must not be
        // indistinguishable from "no marker" just because nothing follows it.
        var (text, caret, hasCursor) = Placeholders.FillWithCaret("正在处理，请稍等{光标}", null);
        Assert.Equal("正在处理，请稍等", text);
        Assert.Equal(0, caret);
        Assert.True(hasCursor);
    }

    [Fact]
    public void Spec_parses_default_and_options()
    {
        var specs = Placeholders.VariableSpecs("{姓名:张三} {环境|dev|test|prod} {备注}");
        Assert.Equal(3, specs.Count);
        Assert.Equal(("姓名", "张三", 0), (specs[0].Name, specs[0].Default, specs[0].Options.Count));
        Assert.Equal("环境", specs[1].Name);
        Assert.Equal("dev", specs[1].Default);   // first option doubles as the default
        Assert.Equal(new[] { "dev", "test", "prod" }, specs[1].Options);
        Assert.Equal(("备注", "", 0), (specs[2].Name, specs[2].Default, specs[2].Options.Count));
    }

    [Fact]
    public void Fill_uses_value_by_parsed_name_and_falls_back_to_default()
    {
        // The values dict is keyed by the NAME, not the raw token text.
        Assert.Equal("张三", Placeholders.Fill("{姓名:李四}", new Dictionary<string, string> { ["姓名"] = "张三" }));
        // No value supplied → declared default; no default → literal token stays.
        Assert.Equal("李四", Placeholders.Fill("{姓名:李四}", null));
        Assert.Equal("{姓名}", Placeholders.Fill("{姓名}", null));
    }

    [Fact]
    public void Uuid_and_random_resolve_automatically()
    {
        Assert.False(Placeholders.HasVariables("{uuid} {随机数} {random}"));   // not user-prompted
        var uuid = Placeholders.Fill("{uuid}", null);
        Assert.True(Guid.TryParse(uuid, out _));
        var rand = Placeholders.Fill("{随机数}", null);
        Assert.Equal(6, rand.Length);
        Assert.True(int.TryParse(rand, out _));
        // Two occurrences resolve independently (fresh value per occurrence).
        var two = Placeholders.Fill("{uuid} {uuid}", null).Split(' ');
        Assert.NotEqual(two[0], two[1]);
    }

    [Fact]
    public void Nested_snippet_references_inline_and_bottom_out_on_cycles()
    {
        var bodies = new Dictionary<string, (string Body, bool UseVariables)>
        {
            ["签名"] = ("此致\n{姓名}", true),
            ["A"] = ("a[{片段:B}]", true),
            ["B"] = ("b[{snippet:A}]", true),
        };
        (string, bool)? Lookup(string n) => bodies.TryGetValue(n, out var b) ? b : null;

        var expanded = Placeholders.ExpandSnippets("开头 {片段:签名} 结尾", Lookup);
        Assert.Equal("开头 此致\n{姓名} 结尾", expanded);
        // A nested body's variables prompt like the host's own.
        Assert.Equal(new[] { "姓名" }, Placeholders.Variables(expanded));

        // Cycles bottom out as literal text instead of recursing forever.
        var cyclic = Placeholders.ExpandSnippets("{片段:A}", Lookup);
        Assert.Contains("{", cyclic);

        // Unknown references stay literal everywhere: no expansion, no variable prompt,
        // and Fill must not misread "片段:x" as variable 片段 with default x.
        Assert.Equal("{片段:未知}", Placeholders.ExpandSnippets("{片段:未知}", Lookup));
        Assert.Empty(Placeholders.Variables("{片段:未知}"));
        Assert.Equal("{snippet:missing}", Placeholders.Fill("{snippet:missing}", null));
    }

    [Fact]
    public void Nested_optout_snippet_is_inlined_verbatim_through_the_pipeline()
    {
        // '代码' opted out of variables BECAUSE its braces are literal code — nesting it
        // must not resurrect prompting or date substitution.
        var bodies = new Dictionary<string, (string Body, bool UseVariables)>
        {
            ["代码"] = ("if (x) { return {value} + {date}; }", false),
        };
        (string, bool)? Lookup(string n) => bodies.TryGetValue(n, out var b) ? b : null;

        var expanded = Placeholders.ExpandSnippets("运行：{片段:代码} 完毕 {昵称}", Lookup);
        Assert.Equal(new[] { "昵称" }, Placeholders.Variables(expanded));   // nothing from the nested body prompts
        var filled = Placeholders.Fill(expanded, new Dictionary<string, string> { ["昵称"] = "小明" });
        var final = Placeholders.UnprotectBraces(filled);
        Assert.Equal("运行：if (x) { return {value} + {date}; } 完毕 小明", final);
    }

    [Fact]
    public void Empty_name_tokens_stay_literal()
    {
        // {:x} / {|a|b} parse to an empty variable name: they're never prompted, so they
        // must never be rewritten either.
        Assert.Equal("{:draft}", Placeholders.Fill("{:draft}", null));
        Assert.Equal("{|red|blue}", Placeholders.Fill("{|red|blue}", null));
        Assert.Empty(Placeholders.Variables("{:draft} {|red|blue}"));
    }

    [Fact]
    public void HasAnyToken_matches_any_braced_token()
    {
        Assert.True(Placeholders.HasAnyToken("你好 {昵称}"));
        Assert.True(Placeholders.HasAnyToken("今天 {日期}"));
        Assert.True(Placeholders.HasAnyToken("if (x) { return y; }"));   // legacy pipeline prompted on this too
        Assert.False(Placeholders.HasAnyToken("plain text"));
        Assert.False(Placeholders.HasAnyToken(""));
        Assert.False(Placeholders.HasAnyToken("empty braces {} don't count"));
    }

    [Fact]
    public void Fill_resolves_date_time_tokens()
    {
        var now = new DateTime(2026, 7, 10, 14, 30, 0);
        Assert.Equal("2026-07-10", Placeholders.Fill("{日期}", null, now: now));
        Assert.Equal("14:30", Placeholders.Fill("{time}", null, now: now));
        Assert.Equal("2026-07-10 14:30", Placeholders.Fill("{日期时间}", null, now: now));
        Assert.Equal("2026-07-17", Placeholders.Fill("{日期+7}", null, now: now));
        Assert.Equal("2026-07-08", Placeholders.Fill("{date-2}", null, now: now));
    }

    [Fact]
    public void Date_time_tokens_are_not_user_variables()
    {
        Assert.False(Placeholders.HasVariables("今天 {日期}，{time}，{日期+3}"));
        Assert.Equal(new[] { "昵称" }, Placeholders.Variables("{昵称} {日期} {datetime}"));
    }

    [Fact]
    public void FillWithCaret_zero_when_no_cursor()
    {
        var (text, caret, hasCursor) = Placeholders.FillWithCaret("你好 {昵称}", new Dictionary<string, string> { ["昵称"] = "小明" });
        Assert.Equal("你好 小明", text);
        Assert.Equal(0, caret);
        Assert.False(hasCursor);
    }

    // ---- {日期=格式} custom date formats ----
    // Format tests pass InvariantCulture explicitly: the default culture on a Buddhist/Hijri
    // calendar system (th-TH / ar-SA) yields a different year, and tests must not drift with
    // the host machine's regional settings.

    [Fact]
    public void Date_custom_format_applies()
    {
        var now = new DateTime(2026, 7, 16, 14, 3, 27);
        var inv = CultureInfo.InvariantCulture;
        Assert.Equal("2026年7月16日", Placeholders.ResolveDateTime("日期=yyyy年M月d日", now, inv));
        Assert.Equal("14:03:27", Placeholders.ResolveDateTime("时间=HH:mm:ss", now, inv));   // ':' inside the format survives
        Assert.Equal("2026/07/16", Placeholders.ResolveDateTime("date=yyyy/MM/dd", now, inv));   // english alias, '/' literal
    }

    // ':' and '/' in a .NET custom format are CULTURE SEPARATOR placeholders (fi-FI renders ':'
    // as '.'), which would make the README's own {time=HH:mm:ss} example wrong on such UIs.
    // The engine must treat them as WYSIWYG literals.
    [Fact]
    public void Date_format_separators_are_literal_regardless_of_culture()
    {
        var now = new DateTime(2026, 7, 16, 14, 3, 27);
        Assert.Equal("14:03:27", Placeholders.ResolveDateTime("时间=HH:mm:ss", now, CultureInfo.GetCultureInfo("fi-FI")));
        Assert.Equal("2026/07/16", Placeholders.ResolveDateTime("日期=yyyy/MM/dd", now, CultureInfo.GetCultureInfo("de-DE")));
    }

    // A ':' INSIDE a quoted literal is already literal — the separator escaping must not
    // double-wrap it and corrupt the quoted section.
    [Fact]
    public void Date_format_quoted_sections_survive_escaping()
    {
        var now = new DateTime(2026, 7, 16, 14, 3, 0);
        Assert.Equal("时刻: 14:03",
            Placeholders.ResolveDateTime("时间='时刻: 'HH:mm", now, CultureInfo.GetCultureInfo("fi-FI")));
    }

    // 'yyyy' follows the culture's CALENDAR: th-TH default is Buddhist (2569, not 2026), and a
    // mixed snippet would paste two different years. Both the formatted and the default path
    // must force Gregorian.
    [Fact]
    public void Date_format_always_uses_gregorian_calendar()
    {
        var now = new DateTime(2026, 7, 16);
        var th = CultureInfo.GetCultureInfo("th-TH");
        Assert.Equal("2026-07-16", Placeholders.ResolveDateTime("日期=yyyy-MM-dd", now, th));
        Assert.Equal("2026-07-16", Placeholders.ResolveDateTime("日期", now, th));   // default path too
    }

    // A lone char is a STANDARD format specifier to .NET ("m" = MonthDay pattern, "H" throws) —
    // the engine must prefix '%' so it reads as the custom specifier the user meant.
    [Fact]
    public void Date_single_char_format_is_custom_not_standard()
    {
        var now = new DateTime(2026, 7, 16, 14, 3, 0);
        var inv = CultureInfo.InvariantCulture;
        Assert.Equal("3", Placeholders.ResolveDateTime("时间=m", now, inv));
        Assert.Equal("14", Placeholders.ResolveDateTime("时间=H", now, inv));
    }

    // The length guard must measure the NAME side only: a literal-heavy format easily exceeds
    // 64 chars and must still resolve (not fall through to a bogus "日期" variable prompt).
    [Fact]
    public void Date_long_literal_heavy_format_still_resolves()
    {
        var literal = "截止日期为下述时间，请尽快处理并回复确认邮件" + new string('！', 50);
        var token = "日期='" + literal + "'yyyy-MM-dd";
        Assert.True(token.Length > 64);
        Assert.Equal(literal + "2026-07-16",
            Placeholders.ResolveDateTime(token, new DateTime(2026, 7, 16), CultureInfo.InvariantCulture));
        Assert.False(Placeholders.HasVariables("{" + token + "}"));
    }

    // AddDays past DateTime.MaxValue throws ArgumentOutOfRangeException (as can bounded
    // calendars) — it must degrade to a literal token, never crash the paste.
    [Fact]
    public void Date_out_of_range_offset_stays_literal_not_crash()
    {
        var nearMax = DateTime.MaxValue.AddDays(-10);
        Assert.Null(Placeholders.ResolveDateTime("日期+9999=yyyy-MM-dd", nearMax));
        Assert.Equal("{日期+9999=yyyy-MM-dd}", Placeholders.Fill("{日期+9999=yyyy-MM-dd}", null, now: nearMax));
    }

    // FillWithCaret is THE production path: it must forward now/culture and stamp ONE time for
    // both halves, or a paste straddling a minute boundary shows two different times.
    [Fact]
    public void FillWithCaret_uses_single_timestamp_and_forwards_culture()
    {
        var now = new DateTime(2026, 7, 16, 14, 3, 27);
        var (text, _, _) = Placeholders.FillWithCaret("{日期时间}{光标}{日期时间}", null, "", now, CultureInfo.InvariantCulture);
        Assert.Equal("2026-07-16 14:032026-07-16 14:03", text);
        var (weekday, _, _) = Placeholders.FillWithCaret("{日期=dddd}{光标}", null, "", now, CultureInfo.GetCultureInfo("zh-CN"));
        Assert.Equal("星期四", weekday);
    }

    [Fact]
    public void Date_offset_combines_with_format()
    {
        var now = new DateTime(2026, 7, 16);
        var inv = CultureInfo.InvariantCulture;
        Assert.Equal("7月23日", Placeholders.ResolveDateTime("日期+7=M月d日", now, inv));
        Assert.Equal("2026-07-15", Placeholders.ResolveDateTime("日期-1=yyyy-MM-dd", now, inv));
    }

    [Fact]
    public void Date_empty_format_falls_back_to_default()
        => Assert.Equal("2026-07-16", Placeholders.ResolveDateTime("日期=", new DateTime(2026, 7, 16)));

    [Fact]
    public void Date_invalid_format_stays_literal_in_fill()
    {
        // An unbalanced quote throws FormatException → the whole token stays literal; nothing
        // is eaten, nothing prompts. (Unknown LETTERS don't throw: custom-format semantics copy
        // them unchanged — {日期=Q} renders "Q", consistently with {date=TBD} → "TBD".)
        var filled = Placeholders.Fill("截止：{日期='未闭合}", null, now: new DateTime(2026, 7, 16));
        Assert.Equal("截止：{日期='未闭合}", filled);
        Assert.Equal("Q", Placeholders.ResolveDateTime("日期=Q", new DateTime(2026, 7, 16), CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Date_format_token_is_not_a_user_variable()
        => Assert.False(Placeholders.HasVariables("{日期=yyyy年M月d日} {日期=Q} {date+3=MM-dd}"));

    [Fact]
    public void Date_default_output_unchanged_without_format()
    {
        var now = new DateTime(2026, 7, 16, 14, 3, 0);
        Assert.Equal("2026-07-16", Placeholders.ResolveDateTime("日期", now));
        Assert.Equal("14:03", Placeholders.ResolveDateTime("时间", now));
        Assert.Equal("2026-07-16 14:03", Placeholders.ResolveDateTime("日期时间+0", now));
    }

    [Fact]
    public void Date_format_weekday_follows_culture()
    {
        var now = new DateTime(2026, 7, 16);   // a Thursday
        Assert.Equal("星期四", Placeholders.ResolveDateTime("日期=dddd", now, CultureInfo.GetCultureInfo("zh-CN")));
        Assert.Equal("Thursday", Placeholders.ResolveDateTime("日期=dddd", now, CultureInfo.GetCultureInfo("en-US")));
    }

    [Fact]
    public void Fill_applies_date_format()
        => Assert.Equal("今天是7月16日",
            Placeholders.Fill("今天是{日期=M月d日}", null, now: new DateTime(2026, 7, 16),
                culture: CultureInfo.InvariantCulture));

    // THE reason the custom format uses '=' and not ':'. A colon-form date alias is a plain user
    // variable with a default — {日期:2026-01-01} is a prefilled date FIELD, one of this app's most
    // common uses. Parsing it as a format instead would silently paste garbage ("Po16a26") with no
    // prompt, breaking snippets that predate the format feature.
    [Fact]
    public void Date_alias_with_colon_is_still_a_user_variable()
    {
        var stamp = new DateTime(2026, 7, 16);
        foreach (var (token, name, def) in new[]
                 {
                     ("{日期:2026-01-01}", "日期", "2026-01-01"),
                     ("{date:today}", "date", "today"),
                     ("{时间:待定}", "时间", "待定"),
                     ("{now:later}", "now", "later"),
                 })
        {
            var spec = Assert.Single(Placeholders.VariableSpecs(token));
            Assert.Equal(name, spec.Name);
            Assert.Equal(def, spec.Default);
            Assert.Equal(def, Placeholders.Fill(token, null, now: stamp));        // unfilled → its default
            Assert.Equal("填的值", Placeholders.Fill(token, new Dictionary<string, string> { [name] = "填的值" }, now: stamp));
        }
    }

    // Rejecting an over-long token would drop it into the VARIABLE pipeline, which prompts for a
    // bogus field named 日期 and pastes the raw format string as its "default". A date token stays a
    // date token however absurd the format; an unusable one just stays literal.
    [Fact]
    public void Date_absurdly_long_format_stays_a_date_token_not_a_variable()
    {
        var token = "{日期='" + new string('长', 400) + "'yyyy}";
        Assert.False(Placeholders.HasVariables(token));
        Assert.Equal(new string('长', 400) + "2026",
            Placeholders.Fill(token, null, now: new DateTime(2026, 7, 16), culture: CultureInfo.InvariantCulture));
    }

    // Spaces around '=' are for readability; only a QUOTED space is content. Without trimming the
    // format side, {日期 = yyyy} pastes a leading space into every signature and table cell.
    [Fact]
    public void Date_format_side_is_trimmed()
    {
        var now = new DateTime(2026, 7, 16);
        var inv = CultureInfo.InvariantCulture;
        Assert.Equal("2026", Placeholders.ResolveDateTime("日期 = yyyy", now, inv));
        Assert.Equal("7月23日", Placeholders.ResolveDateTime("日期+7 = M月d日", now, inv));
        Assert.Equal(" 2026", Placeholders.ResolveDateTime("日期=' 'yyyy", now, inv));   // quoted: kept
    }
}
