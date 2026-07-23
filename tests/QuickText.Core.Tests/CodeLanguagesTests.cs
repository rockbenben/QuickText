using QuickText.Core.Snippets;

namespace QuickText.Core.Tests;

public class CodeLanguagesTests
{
    [Fact]
    public void Has_exactly_one_plain_text_entry_and_it_is_first()
    {
        var plain = CodeLanguages.All.Where(l => l.IsPlainText).ToList();
        Assert.Single(plain);
        Assert.Same(plain[0], CodeLanguages.All[0]);   // the dropdown's first row
        Assert.Equal("", plain[0].Id);
    }

    [Fact]
    public void Ids_are_unique()
    {
        var ids = CodeLanguages.All.Select(l => l.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    // Every real language must be selectable (needs a display name) and renderable
    // (needs a highlighting definition name for the App layer to look up).
    [Fact]
    public void Real_languages_have_a_display_name_and_a_highlighting_name()
    {
        foreach (var lang in CodeLanguages.All.Where(l => !l.IsPlainText))
        {
            Assert.False(string.IsNullOrWhiteSpace(lang.Id), $"blank id");
            Assert.False(string.IsNullOrWhiteSpace(lang.DisplayName), $"{lang.Id}: no display name");
            Assert.False(string.IsNullOrWhiteSpace(lang.HighlightingName), $"{lang.Id}: no highlighting name");
        }
    }

    // Plain text is the fallback for everything unrecognised, so a body whose Language was
    // hand-edited to garbage (or written by a newer version) still opens instead of throwing.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-such-language")]
    public void ById_falls_back_to_plain_text(string? id) =>
        Assert.True(CodeLanguages.ById(id).IsPlainText);

    [Fact]
    public void ById_finds_a_real_language_case_insensitively()
    {
        Assert.Equal("json", CodeLanguages.ById("json").Id);
        Assert.Equal("json", CodeLanguages.ById("JSON").Id);
    }

    // The dropdown ships these; losing one silently would be a regression nobody notices.
    [Fact]
    public void Ships_the_agreed_language_set()
    {
        Assert.Equal(
            new[] { "", "json", "yaml", "xml", "html", "markdown", "sql", "python",
                    "javascript", "csharp", "java", "powershell", "shell", "ini" },
            CodeLanguages.All.Select(l => l.Id));
    }
}
