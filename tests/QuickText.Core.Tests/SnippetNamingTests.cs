using QuickText.Core;

namespace QuickText.Core.Tests;

public class SnippetNamingTests
{
    [Theory]
    [InlineData("hello", "hello")]
    [InlineData("line1\nline2", "line1")]        // first line only
    [InlineData("line1\r\nline2", "line1")]
    [InlineData("   spaced   \nx", "spaced")]    // trimmed
    [InlineData("\nMeeting notes", "Meeting notes")]      // leading blank line skipped
    [InlineData("\n\n   Real line", "Real line")]         // several blank lines skipped
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("   \n  ", "")]                   // no non-blank line at all
    public void FromFirstLine_takes_the_first_non_blank_line(string? input, string expected)
        => Assert.Equal(expected, SnippetNaming.FromFirstLine(input));

    [Theory]
    [InlineData("short", 20, "short")]                       // fits -> unchanged
    [InlineData("exactly-five!", 13, "exactly-five!")]       // == max -> unchanged
    [InlineData("abcdefghij", 5, "abcde…")]                  // truncated + ellipsis
    public void Ellipsize_caps_at_max(string text, int max, string expected)
        => Assert.Equal(expected, SnippetNaming.Ellipsize(text, max));

    [Theory]
    [InlineData("abc", 0, "…")]     // truncate to nothing → ellipsis only, never IndexOutOfRange
    [InlineData("abc", -5, "…")]    // negative max clamped, no crash
    [InlineData("", 0, "")]         // empty text fits max 0 → unchanged
    public void Ellipsize_handles_nonpositive_max_without_crashing(string text, int max, string expected)
        => Assert.Equal(expected, SnippetNaming.Ellipsize(text, max));

    [Fact]
    public void Ellipsize_takes_a_custom_suffix_and_is_surrogate_safe()
    {
        Assert.Equal("ab …", SnippetNaming.Ellipsize("abcdef", 2, " …"));
        Assert.Equal(new string('a', 4) + "…", SnippetNaming.Ellipsize(new string('a', 4) + "😀z", 5));  // 5th char is a high surrogate → cut to 4
    }

    [Fact]
    public void Long_text_is_capped_with_an_ellipsis()
    {
        var name = SnippetNaming.FromFirstLine(new string('a', 40));
        Assert.Equal(new string('a', 20) + "…", name);
    }

    [Fact]
    public void Truncation_never_splits_a_surrogate_pair()
    {
        // 19 ASCII + a 😀 (surrogate pair at indices 19,20) → cutting at 20 would split it.
        var name = SnippetNaming.FromFirstLine(new string('a', 19) + "😀" + "tail");
        Assert.Equal(new string('a', 19) + "…", name);           // dropped the emoji rather than half of it
        Assert.False(char.IsLowSurrogate(name[^2]));             // char before "…" is not a stranded low surrogate
        Assert.False(char.IsHighSurrogate(name[^2]));            // ...nor a stranded high surrogate
    }

    [Fact]
    public void Emoji_that_fit_within_the_cap_are_kept_whole()
    {
        var name = SnippetNaming.FromFirstLine(string.Concat(System.Linq.Enumerable.Repeat("😀", 15)));  // 30 chars
        Assert.Equal(string.Concat(System.Linq.Enumerable.Repeat("😀", 10)) + "…", name);   // 10 whole emoji + …
    }
}
