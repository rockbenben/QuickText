using QuickText.Core.Models;
using QuickText.Core.Pinyin;
using QuickText.Core.Search;

namespace QuickText.Core.Tests;

public class SearchIndexTests
{
    private static SearchIndex Build(params Snippet[] s)
    {
        var idx = new SearchIndex(new ToolGoodPinyinProvider());
        idx.Build(new[] { new Category { Name = "默认", Snippets = s.ToList() } });
        return idx;
    }

    [Fact]
    public void Initials_match_finds_snippet()
    {
        var idx = Build(new Snippet { Id = "1", Name = "你好世界", Body = "..." });
        var hits = idx.Search("nhsj");
        Assert.Contains(hits, h => h.Snippet.Id == "1");
    }

    [Fact]
    public void Abbr_match_finds_snippet()
    {
        var idx = Build(new Snippet { Id = "1", Name = "问候", Abbr = ",hi", Body = "x" });
        Assert.Contains(idx.Search(",hi"), h => h.Snippet.Id == "1");
    }

    [Fact]
    public void Category_filter_narrows_the_scan()
    {
        var idx = new SearchIndex(new ToolGoodPinyinProvider());
        idx.Build(new[]
        {
            new Category { Name = "模板", Snippets = { new Snippet { Id = "t", Name = "会议提醒", Body = "x" } } },
            new Category { Name = "常用", Snippets = { new Snippet { Id = "c", Name = "会议纪要", Body = "x" } } },
        });
        // Prefix match on the category name, case-insensitive; keyword searched within it.
        var hits = idx.Search("会议", category: "模");
        Assert.Single(hits);
        Assert.Equal("t", hits[0].Snippet.Id);
        // Empty keywords + category → browse that category (newest first).
        Assert.Single(idx.Search("", category: "常用"));
        // Unknown category → no hits rather than a silent full-library search.
        Assert.Empty(idx.Search("会议", category: "不存在"));
        // HasCategory lets callers detect that and fall back to a literal '@…' search.
        Assert.True(idx.HasCategory("模"));
        Assert.False(idx.HasCategory("example.com"));
    }

    [Fact]
    public void Name_match_outranks_body_only_match()
    {
        var idx = Build(
            new Snippet { Id = "name", Name = "退款政策", Body = "无关" },
            new Snippet { Id = "body", Name = "其它", Body = "关于退款政策的说明" });
        var hits = idx.Search("退款政策");
        Assert.Equal("name", hits[0].Snippet.Id);
    }

    [Fact]
    public void Body_full_text_is_searched()
    {
        var idx = Build(new Snippet { Id = "1", Name = "标题", Body = "这里包含唯一词汇 XYZZY" });
        Assert.Contains(idx.Search("xyzzy"), h => h.Snippet.Id == "1");
    }

    [Fact]
    public void Usage_reorders_equal_score_hits_but_not_better_matches()
    {
        var old = DateTimeOffset.UtcNow.AddDays(-1);
        var idx = Build(
            new Snippet { Id = "rare", Name = "回复模板甲", Body = "x", UpdatedAt = DateTimeOffset.UtcNow },
            new Snippet { Id = "often", Name = "回复模板乙", Body = "x", UpdatedAt = old },
            new Snippet { Id = "exact", Name = "回复", Body = "x", UpdatedAt = old });
        idx.UsageOf = id => id == "often" ? 50 : 0;

        var hits = idx.Search("回复");
        // exact name match (score 1000) still beats the heavily-used prefix match (600)…
        Assert.Equal("exact", hits[0].Snippet.Id);
        // …but within the equal-score tier, usage outranks the newer UpdatedAt.
        Assert.Equal("often", hits[1].Snippet.Id);
        Assert.Equal("rare", hits[2].Snippet.Id);
    }

    [Fact]
    public void Empty_query_returns_all()
    {
        var idx = Build(
            new Snippet { Id = "1", Name = "a" },
            new Snippet { Id = "2", Name = "b" });
        Assert.Equal(2, idx.Search("").Count);
    }
}
