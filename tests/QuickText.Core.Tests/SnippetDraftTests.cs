using QuickText.Core.Models;
using QuickText.Core.Persistence;

namespace QuickText.Core.Tests;

public class SnippetDraftTests : IDisposable
{
    private const string Def = "New snippet";   // the localized default the Manager seeds a fresh row with

    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "khdraft_" + Guid.NewGuid().ToString("N"));
    public SnippetDraftTests() => Directory.CreateDirectory(_root);
    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }

    [Fact]
    public void Blank_name_and_body_is_a_draft()
        => Assert.True(new Snippet { Name = "", Body = "" }.IsBlankDraft(Def));

    [Fact]
    public void Untouched_default_name_with_no_body_is_a_draft()
        => Assert.True(new Snippet { Name = Def, Body = "" }.IsBlankDraft(Def));

    [Fact]
    public void Whitespace_only_body_is_a_draft()
        => Assert.True(new Snippet { Name = Def, Body = "   \t\n" }.IsBlankDraft(Def));

    [Theory]
    [InlineData("Greeting", "")]          // real name, empty body — user named it, keep it
    [InlineData("", "hello")]             // real body — keep it
    [InlineData(Def, "hello")]            // default name but has content — keep it
    public void Anything_the_user_filled_in_is_kept(string name, string body)
        => Assert.False(new Snippet { Name = name, Body = body }.IsBlankDraft(Def));

    [Fact]
    public void An_abbreviation_alone_keeps_it()
        => Assert.False(new Snippet { Name = Def, Body = "", Abbr = ",sig" }.IsBlankDraft(Def));

    [Fact]
    public void An_image_alone_keeps_it()
        => Assert.False(new Snippet { Name = Def, Body = "", ImagePath = "images/x.png" }.IsBlankDraft(Def));

    // Mirrors what the Manager does on close: drop blank drafts, then persist. Proves an abandoned
    // empty row does NOT survive a save/reload while a real one (and a named-but-empty one) does.
    [Fact]
    public void Pruning_on_save_drops_only_abandoned_drafts()
    {
        var cat = new Category { Name = "常用" };
        cat.Snippets.Add(new Snippet { Id = "keep-real", Name = "问候", Body = "你好" });
        cat.Snippets.Add(new Snippet { Id = "keep-named", Name = "待填", Body = "" });   // named → not a draft
        cat.Snippets.Add(new Snippet { Id = "drop-1", Name = "", Body = "" });
        cat.Snippets.Add(new Snippet { Id = "drop-2", Name = Def, Body = "  " });

        cat.Snippets.RemoveAll(s => s.IsBlankDraft(Def));   // the close-time prune

        var store = new Store(_root);
        store.SaveCategory(cat);
        var reloaded = Assert.Single(store.LoadAll());

        var ids = reloaded.Snippets.Select(s => s.Id).ToList();
        Assert.Equal(new[] { "keep-real", "keep-named" }, ids);
    }
}
