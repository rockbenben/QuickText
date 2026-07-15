using System.Text.Json;
using QuickText.Core.Models;
using QuickText.Core.Persistence;

namespace QuickText.Core.Tests;

public class ModelsTests
{
    [Fact]
    public void Snippet_body_round_trips_verbatim()
    {
        var body = "第一行\n第二行\t制表\n😀 emoji 100% a|b \\ \"quote\"";
        var s = new Snippet { Id = "x1", Name = "问候", Abbr = ",hi", Body = body };

        var json = JsonSerializer.Serialize(s, JsonConfig.Write);
        var back = JsonSerializer.Deserialize<Snippet>(json, JsonConfig.Read)!;

        Assert.Equal(body, back.Body);
        Assert.Equal("x1", back.Id);
        Assert.Equal(",hi", back.Abbr);
    }

    [Fact]
    public void UseVariables_defaults_off_and_round_trips()
    {
        // Default off: legacy library files (no such property) must deserialize to false,
        // so code/script bodies with literal {...} are never processed unless opted in.
        Assert.False(new Snippet().UseVariables);
        var legacy = JsonSerializer.Deserialize<Snippet>("""{"name":"旧数据","body":"{ old }"}""", JsonConfig.Read)!;
        Assert.False(legacy.UseVariables);

        var s = new Snippet { Name = "带变量", Body = "你好 {昵称}", UseVariables = true };
        var back = JsonSerializer.Deserialize<Snippet>(JsonSerializer.Serialize(s, JsonConfig.Write), JsonConfig.Read)!;
        Assert.True(back.UseVariables);
    }

    [Fact]
    public void OutputMode_defaults_to_follow_global_and_round_trips()
    {
        Assert.Equal("", new Snippet().OutputMode);
        var legacy = JsonSerializer.Deserialize<Snippet>("""{"name":"旧"}""", JsonConfig.Read)!;
        Assert.Equal("", legacy.OutputMode ?? "");

        var s = new Snippet { OutputMode = "paste-enter" };
        var back = JsonSerializer.Deserialize<Snippet>(JsonSerializer.Serialize(s, JsonConfig.Write), JsonConfig.Read)!;
        Assert.Equal("paste-enter", back.OutputMode);
    }

    [Fact]
    public void Write_keeps_cjk_readable_and_round_trips_emoji()
    {
        var s = new Snippet { Name = "中文", Body = "😀 100% a|b" };
        var json = JsonSerializer.Serialize(s, JsonConfig.Write);

        // CJK stays human-readable in the synced file (not \uXXXX-escaped)
        Assert.Contains("中文", json);

        // Everything (incl. emoji, which the encoder may surrogate-escape) round-trips verbatim
        var back = JsonSerializer.Deserialize<Snippet>(json, JsonConfig.Read)!;
        Assert.Equal("中文", back.Name);
        Assert.Equal("😀 100% a|b", back.Body);
    }
}
