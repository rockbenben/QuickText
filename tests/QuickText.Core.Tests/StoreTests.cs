using QuickText.Core.Models;
using QuickText.Core.Persistence;

namespace QuickText.Core.Tests;

public class StoreTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "khstore_" + Guid.NewGuid().ToString("N"));

    public StoreTests() => Directory.CreateDirectory(_root);
    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }

    [Fact]
    public void SaveCategory_then_LoadAll_round_trips_body_verbatim()
    {
        var store = new Store(_root);
        var body = "line1\nline2\t😀 100% a|b";
        var cat = new Category { Name = "项目A" };
        cat.Snippets.Add(new Snippet { Id = "1", Name = "问候", Abbr = ",hi", Body = body });
        store.SaveCategory(cat);

        var loaded = store.LoadAll();
        var one = Assert.Single(loaded);
        Assert.Equal("项目A", one.Name);
        Assert.Equal(body, one.Snippets[0].Body);
    }

    [Fact]
    public void LoadAll_skips_corrupt_category_file()
    {
        var store = new Store(_root);
        store.SaveCategory(new Category { Name = "好的" });
        store.SaveCategory(new Category { Name = "坏的" });
        // corrupt one file on disk
        File.WriteAllText(Path.Combine(_root, Store.SanitizeFile("坏的")), "{ broken");

        var loaded = store.LoadAll();
        Assert.Contains(loaded, c => c.Name == "好的");
        // corrupt one is skipped, not thrown
        Assert.DoesNotContain(loaded, c => c.Name == "坏的");
    }

    [Fact]
    public void Trash_round_trips_and_purges_expired_entries()
    {
        var store = new Store(_root);
        var fresh = new TrashEntry { Snippet = new Snippet { Id = "keep", Name = "新" }, Category = "常用" };
        var expired = new TrashEntry
        {
            Snippet = new Snippet { Id = "gone", Name = "旧" },
            Category = "常用",
            DeletedAt = DateTimeOffset.UtcNow.AddDays(-(Store.TrashRetentionDays + 1)),
        };
        store.SaveTrash(new List<TrashEntry> { fresh, expired });

        var loaded = store.LoadTrash();   // purges the expired entry and persists the rest
        var one = Assert.Single(loaded);
        Assert.Equal("keep", one.Snippet.Id);
        Assert.Equal("常用", one.Category);
        // The purge was written back, not just filtered in memory.
        Assert.Single(store.LoadTrash());
    }

    [Fact]
    public void LoadTrash_on_missing_or_corrupt_file_returns_empty()
    {
        var store = new Store(_root);
        Assert.Empty(store.LoadTrash());
        File.WriteAllText(Path.Combine(_root, "trash.json"), "{ broken");
        Assert.Empty(store.LoadTrash());
    }

    [Fact]
    public void FindConflictFiles_detects_sync_conflicts()
    {
        File.WriteAllText(Path.Combine(_root, "项目A (conflict).json"), "[]");
        File.WriteAllText(Path.Combine(_root, "项目B-冲突.json"), "[]");
        var found = new Store(_root).FindConflictFiles();
        Assert.Equal(2, found.Count);
    }

    [Fact]
    public void SanitizeFile_replaces_invalid_chars()
    {
        var f = Store.SanitizeFile("a/b:c");
        Assert.DoesNotContain('/', f);
        Assert.DoesNotContain(':', f);
        Assert.EndsWith(".json", f);
    }

    [Fact]
    public void FindConflictFiles_detects_cloud_duplicate_marker()
    {
        File.WriteAllText(Path.Combine(_root, "项目C (1).json"), "[]");
        var found = new Store(_root).FindConflictFiles();
        Assert.Contains(found, f => f.Contains(" (1)"));
    }

    [Fact]
    public void Categories_with_colliding_sanitized_names_do_not_lose_data()
    {
        var store = new Store(_root);
        var a = new Category { Name = "a/b" };
        a.Snippets.Add(new Snippet { Id = "a1", Name = "AA", Body = "bodyA" });
        var b = new Category { Name = "a:b" };
        b.Snippets.Add(new Snippet { Id = "b1", Name = "BB", Body = "bodyB" });
        store.SaveCategory(a);
        store.SaveCategory(b);

        var loaded = store.LoadAll();
        Assert.Equal(2, loaded.Count);
        var la = loaded.Single(c => c.Name == "a/b");
        var lb = loaded.Single(c => c.Name == "a:b");
        Assert.Equal("bodyA", la.Snippets.Single().Body);
        Assert.Equal("bodyB", lb.Snippets.Single().Body);
    }

    // Sync-drive users (坚果云/OneDrive/NAS) get a full-library diff if every existing snippet
    // sprouts a new field. A snippet that never picked a code format must serialize byte-identical
    // to before this feature existed.
    [Fact]
    public void Snippet_without_a_code_format_writes_no_code_format_field()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(
            new[] { new Snippet { Name = "n", Body = "b" } },
            QuickText.Core.Persistence.JsonConfig.Write);
        Assert.DoesNotContain("codeformat", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Snippet_with_a_code_format_writes_and_reads_it_back()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(
            new[] { new Snippet { Name = "n", Body = "b", CodeFormat = "json" } },
            QuickText.Core.Persistence.JsonConfig.Write);
        Assert.Contains("\"CodeFormat\": \"json\"", json);

        var back = System.Text.Json.JsonSerializer.Deserialize<List<Snippet>>(
            json, QuickText.Core.Persistence.JsonConfig.Read)!;
        Assert.Equal("json", back[0].CodeFormat);
    }

    // Pre-upgrade files have no such field at all.
    [Fact]
    public void Snippet_json_without_the_field_deserializes_as_plain_text()
    {
        var back = System.Text.Json.JsonSerializer.Deserialize<List<Snippet>>(
            """[{"Name":"n","Body":"b"}]""", QuickText.Core.Persistence.JsonConfig.Read)!;
        Assert.True(string.IsNullOrEmpty(back[0].CodeFormat));
    }
}
