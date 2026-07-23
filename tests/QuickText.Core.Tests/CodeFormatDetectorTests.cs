using System.Text.Json;
using QuickText.Core.Snippets;

namespace QuickText.Core.Tests;

public class CodeFormatDetectorTests
{
    // ---------- JSON ----------

    [Theory]
    [InlineData("{\"a\":1,\"b\":[1,2,3]}")]
    [InlineData("  \n  {\"a\":1}")]           // leading whitespace before the brace
    [InlineData("[1, 2, 3]")]                 // array, not ini, not object
    [InlineData("[]")]
    public void Detect_valid_json(string body) => Assert.Equal("json", CodeFormatDetector.Detect(body));

    // The headline false-positive case: a snippet placeholder starts with '{' but is not JSON.
    [Fact]
    public void Detect_placeholder_body_is_not_json() =>
        Assert.Equal("", CodeFormatDetector.Detect("{姓名}你好"));

    [Theory]
    [InlineData("{\"a\":1")]                  // truncated
    [InlineData("{not json at all}")]
    public void Detect_invalid_json_is_not_json(string body) => Assert.Equal("", CodeFormatDetector.Detect(body));

    // ---------- Shell ----------

    [Theory]
    [InlineData("#!/bin/bash\necho hi")]
    [InlineData("#!/usr/bin/env zsh")]
    [InlineData("#!/bin/sh\necho hi")]
    public void Detect_shell_shebang(string body) => Assert.Equal("shell", CodeFormatDetector.Detect(body));

    [Fact]
    public void Detect_shebang_without_known_shell_is_not_shell() =>
        Assert.Equal("", CodeFormatDetector.Detect("#!/usr/bin/env node\nconsole.log(1)"));

    // ---------- XML / HTML ----------

    [Fact]
    public void Detect_xml_declaration() =>
        Assert.Equal("xml", CodeFormatDetector.Detect("<?xml version=\"1.0\"?><a/>"));

    [Theory]
    [InlineData("<!DOCTYPE html><html>")]
    [InlineData("<!doctype html><html>")]
    [InlineData("<!Doctype Html><Html>")]
    public void Detect_html_doctype_any_case(string body) => Assert.Equal("html", CodeFormatDetector.Detect(body));

    [Fact]
    public void Detect_simple_xml_tag_shape() =>
        Assert.Equal("xml", CodeFormatDetector.Detect("<root><child>value</child></root>"));

    // ---------- INI ----------

    [Fact]
    public void Detect_ini_section_header() =>
        Assert.Equal("ini", CodeFormatDetector.Detect("[general]\nkey = 1"));

    [Fact]
    public void Detect_ini_section_header_after_comment() =>
        Assert.Equal("ini", CodeFormatDetector.Detect("; comment\n[general]\nkey = 1"));

    [Fact]
    public void Detect_ini_section_header_after_hash_comment() =>
        Assert.Equal("ini", CodeFormatDetector.Detect("# comment\n[general]\nkey = 1"));

    [Fact]
    public void Detect_bracketed_line_without_section_shape_is_not_ini() =>
        Assert.Equal("", CodeFormatDetector.Detect("[not a section header because trailing text]x\nkey = 1"));

    // ---------- Ordinary prose: the false-positive guard ----------

    [Theory]
    [InlineData("这是一段普通的中文文本，没有任何代码特征。")]
    [InlineData("Just an ordinary paragraph of English prose, nothing structural about it.")]
    [InlineData("Dear team, please review the attached document before Friday.")]
    public void Detect_prose_is_plain_text(string body) => Assert.Equal("", CodeFormatDetector.Detect(body));

    // ---------- Empty / null ----------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\t \n")]
    public void Detect_empty_or_whitespace_is_plain_text(string? body) => Assert.Equal("", CodeFormatDetector.Detect(body));

    // ---------- Size cap ----------

    // Guarded so a huge body can't cost a full JSON parse — even one that would otherwise parse.
    [Fact]
    public void Detect_body_over_max_scan_length_is_not_detected()
    {
        var huge = "{" + new string('a', PlaceholderScanner.MaxScanLength + 1) + "}";
        Assert.Equal("", CodeFormatDetector.Detect(huge));
    }

    [Fact]
    public void Detect_valid_json_over_max_scan_length_is_still_skipped()
    {
        var huge = "[" + string.Join(",", Enumerable.Repeat("1", PlaceholderScanner.MaxScanLength)) + "]";
        Assert.True(huge.Length > PlaceholderScanner.MaxScanLength);
        Assert.Equal("", CodeFormatDetector.Detect(huge));
    }

    // ---------- Pathologically long line (minified content) ----------

    private static string BuildMinifiedJsonObject(int minLength)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append('{');
        int i = 0;
        while (sb.Length < minLength)
        {
            if (i > 0) sb.Append(',');
            sb.Append('"').Append('k').Append(i).Append("\":").Append(i);
            i++;
        }
        sb.Append('}');
        return sb.ToString();
    }

    // Valid JSON, but on one enormous line — exactly the minified shape that hangs the code
    // editor's word-wrap layout. The guard must key on LINE length, not total size: the same
    // content pretty-printed across many short lines detects normally (see the next test).
    [Fact]
    public void Detect_json_over_max_line_length_on_one_line_is_not_detected()
    {
        var minified = BuildMinifiedJsonObject(CodeFormatDetector.MaxLineLengthForCode + 1);
        Assert.True(minified.Length > CodeFormatDetector.MaxLineLengthForCode);
        Assert.DoesNotContain('\n', minified);
        Assert.Equal("", CodeFormatDetector.Detect(minified));
    }

    // Same bytes as above, pretty-printed one member per line — proves the guard is about line
    // length, not total body size.
    [Fact]
    public void Detect_same_json_pretty_printed_across_lines_still_detects_as_json()
    {
        var minified = BuildMinifiedJsonObject(CodeFormatDetector.MaxLineLengthForCode + 1);
        using var doc = JsonDocument.Parse(minified);
        var prettyOptions = new JsonSerializerOptions { WriteIndented = true };
        var pretty = JsonSerializer.Serialize(doc.RootElement, prettyOptions);
        Assert.Equal("json", CodeFormatDetector.Detect(pretty));
    }

    // Just under the threshold: detection still applies normally.
    [Fact]
    public void Detect_json_just_under_max_line_length_still_detects()
    {
        var body = "{\"a\":\"" + new string('x', CodeFormatDetector.MaxLineLengthForCode - 20) + "\"}";
        Assert.True(body.Length <= CodeFormatDetector.MaxLineLengthForCode);
        Assert.Equal("json", CodeFormatDetector.Detect(body));
    }

    // A body made of many short lines, whose TOTAL size is large, still detects normally — total
    // size is not the criterion, only the longest single line is.
    [Fact]
    public void Detect_many_short_lines_total_size_is_not_the_criterion()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("[\n");
        int i = 0;
        while (sb.Length < CodeFormatDetector.MaxLineLengthForCode * 5)
        {
            sb.Append('"').Append('x').Append(i).Append("\",\n");
            i++;
        }
        sb.Append("\"end\"\n]");
        var body = sb.ToString();
        Assert.True(body.Length > CodeFormatDetector.MaxLineLengthForCode);
        Assert.True(CodeFormatDetector.LongestLineLength(body) < CodeFormatDetector.MaxLineLengthForCode);
        Assert.Equal("json", CodeFormatDetector.Detect(body));
    }

    // ---------- LongestLineLength helper ----------

    [Theory]
    [InlineData(null, 0)]
    [InlineData("", 0)]
    [InlineData("abc", 3)]
    [InlineData("abc\ndef", 3)]
    [InlineData("ab\ndefgh\nij", 5)]
    [InlineData("ab\r\ndefgh\r\nij", 5)]     // \r\n counted as one break
    [InlineData("\n\n\n", 0)]
    public void LongestLineLength_computes_correctly(string? body, int expected) =>
        Assert.Equal(expected, CodeFormatDetector.LongestLineLength(body));
}
