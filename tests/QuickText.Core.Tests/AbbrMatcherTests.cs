using QuickText.Core.Abbr;
using QuickText.Core.Models;

namespace QuickText.Core.Tests;

public class AbbrMatcherTests
{
    private static AbbrMatcher WithAbbr(string abbr, string body)
    {
        var m = new AbbrMatcher();
        m.Rebuild(new[] { new Snippet { Abbr = abbr, Body = body } });
        return m;
    }

    private static void Type(AbbrMatcher m, string s) { foreach (var c in s) m.FeedChar(c); }

    [Fact]
    public void Matches_abbr_on_terminator_returns_backspace_and_body()
    {
        var m = WithAbbr(",hi", "你好，很高兴为您服务");
        Type(m, ",hi");
        var match = m.OnTerminator();
        Assert.NotNull(match);
        Assert.Equal(3, match!.BackspaceCount);
        Assert.Equal("你好，很高兴为您服务", match.Snippet.Body);
        Assert.Equal(",hi", match.Abbr);   // expansion-undo retypes exactly this
    }

    [Fact]
    public void No_match_when_buffer_does_not_end_with_abbr()
    {
        var m = WithAbbr(",hi", "x");
        Type(m, "hello");
        Assert.Null(m.OnTerminator());
    }

    [Fact]
    public void Reset_clears_buffer()
    {
        var m = WithAbbr(",hi", "x");
        Type(m, ",h");
        m.Reset();
        Type(m, "i");
        Assert.Null(m.OnTerminator());
    }

    [Fact]
    public void Longest_matching_abbr_wins()
    {
        var m = new AbbrMatcher();
        m.Rebuild(new[]
        {
            new Snippet { Abbr = ",h", Body = "short" },
            new Snippet { Abbr = "x,h", Body = "long" },
        });
        Type(m, "x,h");
        var match = m.OnTerminator();
        Assert.Equal("long", match!.Snippet.Body);
        Assert.Equal(3, match.BackspaceCount);
    }

    [Fact]
    public void Match_carries_the_snippet()
    {
        var m = new AbbrMatcher();
        var sn = new Snippet { Abbr = ",hi", Body = "x", UseVariables = true, OutputMode = "copy" };
        m.Rebuild(new[] { sn });
        Type(m, ",hi");
        Assert.Same(sn, m.OnTerminator()!.Snippet);   // hook reads Body/Id/UseVariables/OutputMode off it
    }

    [Fact]
    public void Matching_is_case_insensitive_but_undo_keeps_typed_casing()
    {
        var m = new AbbrMatcher();
        m.Rebuild(new[] { new Snippet { Abbr = "qm", Body = "sig" } }, ";");
        Type(m, ";QM");   // CapsLock on
        var match = m.OnTerminator();
        Assert.Equal("sig", match!.Snippet.Body);
        Assert.Equal(";QM", match.Abbr);   // undo must retype exactly what the user typed
    }

    [Fact]
    public void Fires_when_abbr_stands_alone()
    {
        var m = WithAbbr("hf", "回复");
        Type(m, "hf");
        Assert.Equal("回复", m.OnTerminator()!.Snippet.Body);
    }

    [Fact]
    public void Does_not_fire_as_tail_of_a_longer_word()
    {
        var m = WithAbbr("hf", "回复");
        Type(m, "ashf");   // bare "hf" is preceded by a letter → not a word boundary
        Assert.Null(m.OnTerminator());
    }

    [Fact]
    public void Prefixed_abbr_fires_even_glued_to_a_word()
    {
        var m = WithAbbr(";qm", "sig");
        Type(m, "hi;qm");   // ';' prefix is self-delimiting → fires even without a space before it
        Assert.Equal("sig", m.OnTerminator()!.Snippet.Body);
    }

    [Fact]
    public void Backspace_removes_one_char_so_a_corrected_typo_still_expands()
    {
        var m = WithAbbr(";qm", "sig");
        Type(m, ";qmm");     // typo: extra 'm'
        m.Backspace();       // user corrects it
        Assert.Equal("sig", m.OnTerminator()!.Snippet.Body);
    }

    [Fact]
    public void Backspace_on_empty_buffer_is_a_noop()
    {
        var m = WithAbbr(";qm", "sig");
        m.Backspace();
        Type(m, ";qm");
        Assert.Equal("sig", m.OnTerminator()!.Snippet.Body);
    }

    [Fact]
    public void IsEmpty_reflects_registered_triggers()
    {
        var m = new AbbrMatcher();
        Assert.True(m.IsEmpty);
        m.Rebuild(new[] { new Snippet { Abbr = "", Body = "no trigger" } });
        Assert.True(m.IsEmpty);    // snippets without an Abbr register nothing
        m.Rebuild(new[] { new Snippet { Abbr = "qm", Body = "sig" } });
        Assert.False(m.IsEmpty);
    }

    [Fact]
    public void Global_prefix_is_prepended_to_stored_abbr()
    {
        var m = new AbbrMatcher();
        m.Rebuild(new[] { new Snippet { Abbr = "qm", Body = "sig" } }, ";");
        Type(m, "hi;qm");                        // bare "qm" + global ";" → fires on ";qm"
        var match = m.OnTerminator();
        Assert.Equal("sig", match!.Snippet.Body);
        Assert.Equal(3, match.BackspaceCount);   // deletes ";qm"
        Assert.Equal(";qm", match.Abbr);         // undo retypes the full trigger

        m.Rebuild(new[] { new Snippet { Abbr = "qm", Body = "sig" } }, ";");
        Type(m, "qm");                           // bare, prefix not typed → must NOT fire
        Assert.Null(m.OnTerminator());
    }
}
