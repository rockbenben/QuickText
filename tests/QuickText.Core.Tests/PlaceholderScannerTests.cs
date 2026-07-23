using QuickText.Core.Snippets;

namespace QuickText.Core.Tests;

public class PlaceholderScannerTests
{
    // ---------- Classify ----------

    [Theory]
    [InlineData("姓名")]
    [InlineData("姓名:张三")]
    [InlineData("环境|dev|test|prod")]
    [InlineData(" 姓名 ")]          // trimmed before judging
    [InlineData("片段:")]           // no target name -> not a reference, it's a variable named 片段
    public void Classify_variable(string raw) =>
        Assert.Equal(TokenKind.Variable, Placeholders.Classify(raw));

    [Theory]
    [InlineData("日期")]
    [InlineData("date")]
    [InlineData("时间")]
    [InlineData("time")]
    [InlineData("日期时间")]
    [InlineData("datetime")]
    [InlineData("now")]
    [InlineData("uuid")]
    [InlineData("UUID")]
    [InlineData("随机数")]
    [InlineData("random")]
    [InlineData("剪贴板")]
    [InlineData("clipboard")]
    [InlineData("日期+7")]
    [InlineData("日期+7=M月d日")]
    public void Classify_auto(string raw) =>
        Assert.Equal(TokenKind.Auto, Placeholders.Classify(raw));

    // Classify judges by NAME only. Format validity is the scanner's job (it needs a clock),
    // so a bad format still classifies as Auto here.
    [Fact]
    public void Classify_ignores_date_format_validity() =>
        Assert.Equal(TokenKind.Auto, Placeholders.Classify("日期=Q"));

    [Theory]
    [InlineData("光标")]
    [InlineData("cursor")]
    public void Classify_cursor(string raw) =>
        Assert.Equal(TokenKind.Cursor, Placeholders.Classify(raw));

    [Theory]
    [InlineData("片段:签名")]
    [InlineData("snippet:sig")]
    public void Classify_nested(string raw) =>
        Assert.Equal(TokenKind.Nested, Placeholders.Classify(raw));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(":张三")]
    [InlineData("|a|b")]
    public void Classify_invalid(string raw) =>
        Assert.Equal(TokenKind.Invalid, Placeholders.Classify(raw));

    // ---------- the other exposed helpers ----------

    [Fact]
    public void TokenMatches_reports_index_length_and_inner_text()
    {
        var hits = Placeholders.TokenMatches("你好{姓名}，见{日期}").ToList();
        Assert.Equal(2, hits.Count);
        Assert.Equal((2, 4, "姓名"), hits[0]);     // "{姓名}" starts at 2, 4 chars incl. braces
        Assert.Equal((8, 4, "日期"), hits[1]);
    }

    [Fact]
    public void TokenMatches_ignores_tokens_spanning_a_newline() =>
        Assert.Empty(Placeholders.TokenMatches("{姓\n名}"));

    [Fact]
    public void NestedName_extracts_target_else_null()
    {
        Assert.Equal("签名", Placeholders.NestedName("片段:签名"));
        Assert.Equal("sig", Placeholders.NestedName("snippet: sig "));
        Assert.Null(Placeholders.NestedName("姓名"));
        Assert.Null(Placeholders.NestedName("片段:"));
    }

    [Fact]
    public void IsDateToken_covers_names_regardless_of_format()
    {
        Assert.True(Placeholders.IsDateToken("日期"));
        Assert.True(Placeholders.IsDateToken("时间+3=HH:mm"));
        Assert.True(Placeholders.IsDateToken("日期=Q"));      // bad format, still a date token
        Assert.False(Placeholders.IsDateToken("uuid"));
        Assert.False(Placeholders.IsDateToken("姓名"));
    }

    [Fact]
    public void ParseVariable_matches_the_prompt_time_parse()
    {
        var plain = Placeholders.ParseVariable("姓名");
        Assert.Equal("姓名", plain.Name);
        Assert.Equal("", plain.Default);
        Assert.Empty(plain.Options);

        var withDefault = Placeholders.ParseVariable("姓名:张三");
        Assert.Equal("姓名", withDefault.Name);
        Assert.Equal("张三", withDefault.Default);

        var withOptions = Placeholders.ParseVariable("环境|dev|test");
        Assert.Equal("环境", withOptions.Name);
        Assert.Equal(new[] { "dev", "test" }, withOptions.Options);
        Assert.Equal("dev", withOptions.Default);   // first option doubles as the default
    }

    // ---------- Scan ----------

    private static readonly DateTime Stamp = new(2026, 7, 23, 15, 4, 5);

    private static IReadOnlyList<TokenSpan> Scan(string body, params string[] existingSnippets) =>
        PlaceholderScanner.Scan(body, n => existingSnippets.Contains(n), Stamp,
            System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public void Scan_span_covers_the_whole_token_including_braces()
    {
        var spans = Scan("你好{姓名}！");
        var s = Assert.Single(spans);
        Assert.Equal(2, s.Start);
        Assert.Equal(4, s.Length);          // "{姓名}"
        Assert.Equal(TokenKind.Variable, s.Kind);
        Assert.Equal("姓名", s.Name);
        Assert.False(s.HasOptions);
        Assert.Equal(InvalidReason.None, s.Reason);
    }

    [Fact]
    public void Scan_flags_option_lists()
    {
        var s = Assert.Single(Scan("{环境|dev|prod}"));
        Assert.Equal(TokenKind.Variable, s.Kind);
        Assert.Equal("环境", s.Name);
        Assert.True(s.HasOptions);
    }

    [Fact]
    public void Scan_nested_reference_depends_on_existence()
    {
        var found = Assert.Single(Scan("{片段:签名}", "签名"));
        Assert.Equal(TokenKind.Nested, found.Kind);
        Assert.Equal("签名", found.Name);              // the TARGET name, not "片段:签名"
        Assert.Equal(InvalidReason.None, found.Reason);

        var missing = Assert.Single(Scan("{片段:签名}"));
        Assert.Equal(TokenKind.Invalid, missing.Kind);
        Assert.Equal(InvalidReason.UnknownSnippet, missing.Reason);
        Assert.Equal("签名", missing.Name);
    }

    // No lookup supplied (the caller can't resolve names) -> don't cry wolf.
    [Fact]
    public void Scan_without_lookup_keeps_nested_valid()
    {
        var s = Assert.Single(PlaceholderScanner.Scan("{片段:签名}"));
        Assert.Equal(TokenKind.Nested, s.Kind);
    }

    [Fact]
    public void Scan_previews_date_tokens()
    {
        var s = Assert.Single(Scan("{日期+7=yyyy-MM-dd}"));
        Assert.Equal(TokenKind.Auto, s.Kind);
        Assert.Equal("2026-07-30", s.Preview);
    }

    // .NET renders an unrecognized custom specifier like "Q" as a literal character rather than
    // throwing, so it does NOT reject that format. An unterminated quote in the format DOES make
    // .NET throw FormatException (confirmed empirically against Placeholders.ResolveDateTime),
    // which is what Fill()/ResolveDateTime actually treats as "bad" — so that's the case the
    // scanner must flag.
    [Fact]
    public void Scan_flags_a_bad_date_format()
    {
        var s = Assert.Single(Scan("{日期=yyyy'}"));
        Assert.Equal(TokenKind.Invalid, s.Kind);
        Assert.Equal(InvalidReason.BadDateFormat, s.Reason);
        Assert.Null(s.Preview);
    }

    // The defect this guards against: a hand-rolled character-allowlist validator (instead of
    // deferring to Placeholders.ResolveDateTime) flagged these as invalid, because CJK literal
    // characters and spaces weren't in its allowlist. All three are documented formats from the
    // README (the {日期=yyyy年M月d日} example is the project's flagship documented feature).
    [Theory]
    [InlineData("{日期=yyyy年M月d日}")]
    [InlineData("{日期时间=yyyy-MM-dd HH:mm}")]
    [InlineData("{时间=HH:mm:ss}")]
    public void Scan_accepts_formats_with_literal_text_and_spaces(string body)
    {
        var s = Assert.Single(Scan(body));
        Assert.Equal(TokenKind.Auto, s.Kind);
        Assert.NotNull(s.Preview);
        Assert.Equal(InvalidReason.None, s.Reason);
    }

    // uuid/random/clipboard have no preview text but must NOT be mistaken for bad dates.
    [Fact]
    public void Scan_non_date_auto_tokens_have_no_preview_and_stay_valid()
    {
        foreach (var body in new[] { "{uuid}", "{随机数}", "{剪贴板}" })
        {
            var s = Assert.Single(Scan(body));
            Assert.Equal(TokenKind.Auto, s.Kind);
            Assert.Null(s.Preview);
            Assert.Equal(InvalidReason.None, s.Reason);
        }
    }

    [Fact]
    public void Scan_marks_the_cursor_token()
    {
        var s = Assert.Single(Scan("报告{光标}"));
        Assert.Equal(TokenKind.Cursor, s.Kind);
    }

    [Theory]
    [InlineData("{:张三}")]
    [InlineData("{|a|b}")]
    public void Scan_flags_an_empty_variable_name(string body)
    {
        var s = Assert.Single(Scan(body));
        Assert.Equal(TokenKind.Invalid, s.Kind);
        Assert.Equal(InvalidReason.EmptyName, s.Reason);
    }

    [Fact]
    public void Scan_flags_an_unclosed_brace()
    {
        var s = Assert.Single(Scan("尊敬的{姓名"));
        Assert.Equal(3, s.Start);
        Assert.Equal(1, s.Length);          // just the brace itself
        Assert.Equal(TokenKind.Invalid, s.Kind);
        Assert.Equal(InvalidReason.Unclosed, s.Reason);
    }

    // A token can't span a newline, so the brace on line 1 is dangling even though line 2 has a '}'.
    [Fact]
    public void Scan_flags_a_brace_that_only_closes_on_the_next_line()
    {
        var s = Assert.Single(Scan("{姓\n名}"));
        Assert.Equal(InvalidReason.Unclosed, s.Reason);
        Assert.Equal(0, s.Start);
    }

    // A stray '}' is harmless literal text — only '{' signals intent to write a token.
    [Fact]
    public void Scan_ignores_a_stray_closing_brace() => Assert.Empty(Scan("100} 元"));

    // "{}" IS closed (unlike a dangling '{'); the real problem is there's no name inside to
    // fill, so it must report EmptyName rather than being swept up as Unclosed.
    [Fact]
    public void Scan_flags_an_empty_brace_pair_as_empty_name_not_unclosed()
    {
        var s = Assert.Single(Scan("{}"));
        Assert.Equal(TokenKind.Invalid, s.Kind);
        Assert.Equal(InvalidReason.EmptyName, s.Reason);
        Assert.Equal(0, s.Start);
        Assert.Equal(2, s.Length);
    }

    [Fact]
    public void Scan_returns_spans_in_document_order()
    {
        var spans = Scan("{日期} 你好 {姓名} {光标");
        Assert.Equal(new[] { TokenKind.Auto, TokenKind.Variable, TokenKind.Invalid },
            spans.Select(s => s.Kind));
        Assert.True(spans[0].Start < spans[1].Start && spans[1].Start < spans[2].Start);
    }

    // THE consistency nail: whatever the editor paints cyan must be exactly what the send-time
    // dialog will ask for. If these two ever disagree, highlighting is lying to the user.
    [Theory]
    [InlineData("您好 {昵称}，订单 {订单号} 给 {昵称} 已发货")]
    [InlineData("{剪贴板}{光标}{uuid}{日期+7}{片段:签名}{姓名:张三}{环境|a|b}")]
    [InlineData("{:空}{|x|y}{未闭合 {正常}")]
    [InlineData("没有任何记号的一段纯文本")]
    public void Scan_variables_match_the_send_time_prompt(string body)
    {
        var highlighted = PlaceholderScanner.Scan(body, _ => true, Stamp)
            .Where(s => s.Kind == TokenKind.Variable)
            .Select(s => s.Name)
            .Distinct()
            .ToArray();
        var prompted = Placeholders.VariableSpecs(body).Select(v => v.Name).ToArray();
        Assert.Equal(prompted, highlighted);
    }

    [Fact]
    public void Scan_gives_up_on_absurdly_long_bodies()
    {
        var huge = new string('x', PlaceholderScanner.MaxScanLength + 1) + "{姓名}";
        Assert.Empty(PlaceholderScanner.Scan(huge, _ => true, Stamp));
    }

    [Fact]
    public void Scan_handles_empty_and_null()
    {
        Assert.Empty(PlaceholderScanner.Scan(null));
        Assert.Empty(PlaceholderScanner.Scan(""));
    }
}
