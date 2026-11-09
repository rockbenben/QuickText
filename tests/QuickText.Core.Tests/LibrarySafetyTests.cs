using QuickText.Core.Models;
using QuickText.Core.Persistence;

namespace QuickText.Core.Tests;

public class LibrarySafetyTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "QuickText-safety-" + Guid.NewGuid().ToString("N"));
    public LibrarySafetyTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{\"Categories\":null}")]
    [InlineData("{}")]
    [InlineData("{\"Other\":[]}")]
    public void Unreadable_index_cannot_be_overwritten_by_a_save(string broken)
    {
        var store = new Store(_root);
        store.SaveCategory(new Category { Name = "Existing" });
        var index = Path.Combine(_root, "index.json");
        File.WriteAllText(index, broken);
        Assert.ThrowsAny<Exception>(() => store.SaveCategory(new Category { Name = "New" }));
        Assert.ThrowsAny<Exception>(() => new Store(_root).SaveCategory(new Category { Name = "New" }));
        Assert.Equal(broken, File.ReadAllText(index));
        Assert.True(File.Exists(Path.Combine(_root, "Existing.json")));
        Assert.Throws<InvalidDataException>(() => store.LoadAll(strict: true));
    }

    [Fact]
    public void An_external_category_edit_cannot_be_overwritten_by_an_old_snapshot()
    {
        var reader = new Store(_root);
        var cat = new Category { Name = "A", Snippets = new() { new() { Name = "One", Body = "old" } } };
        reader.SaveCategory(cat);
        var snapshot = reader.LoadAll()[0];
        var external = new Store(_root);
        cat.Snippets[0].Body = "external";
        external.SaveCategory(cat);
        snapshot.Snippets[0].Body = "draft";
        Assert.Throws<LibraryChangedException>(() => reader.SaveCategory(snapshot));
        Assert.Equal("external", external.LoadAll()[0].Snippets[0].Body);
    }

    [Fact]
    public void Byte_order_marked_category_files_remain_readable()
    {
        var store = new Store(_root);
        store.SaveCategory(new Category { Name = "A" });
        File.WriteAllText(Path.Combine(_root, "A.json"), "[{\"Name\":\"你好\",\"Body\":\"旧文本\"}]", System.Text.Encoding.Unicode);
        Assert.Equal("旧文本", store.LoadAll(strict: true)[0].Snippets[0].Body);
    }

    [Fact]
    public void An_unreadable_category_is_not_replaced_by_a_new_category_with_the_same_name()
    {
        var store = new Store(_root);
        store.SaveCategory(new Category { Name = "A" });
        var path = Path.Combine(_root, "A.json");
        File.WriteAllText(path, "{");
        Assert.Empty(store.LoadAll());
        Assert.Throws<InvalidDataException>(() => store.SaveCategory(new Category { Name = "A" }));
        Assert.Equal("{", File.ReadAllText(path));
    }

    [Fact]
    public void A_backup_must_explicitly_contain_a_category_list()
    {
        File.WriteAllText(Path.Combine(_root, "index.json"), "{}");
        Assert.Throws<InvalidDataException>(() => new Store(_root).ValidateBackup());
    }

    [Fact]
    public void Missing_index_with_category_files_is_not_a_fresh_library()
    {
        File.WriteAllText(Path.Combine(_root, "Existing.json"), "[]");
        Assert.Throws<InvalidDataException>(() => new Store(_root).SaveCategory(new Category { Name = "New" }));
        Assert.False(File.Exists(Path.Combine(_root, "index.json")));
        Assert.Throws<InvalidDataException>(() => new Store(_root).LoadAll(strict: true));
    }

    [Theory]
    [InlineData("A")]
    [InlineData("settings")]
    public void Image_cleanup_keeps_images_when_the_index_disappears(string category)
    {
        var store = new Store(_root);
        string image = store.SaveImage(new byte[] { 1, 2, 3 }, ".png");
        store.SaveCategory(new Category { Name = category, Snippets = new() { new() { ImagePath = image } } });
        File.Delete(Path.Combine(_root, "index.json"));
        store.DeleteUnreferencedImage(image);
        Assert.True(File.Exists(store.ResolveImage(image)));
    }

    [Fact]
    public void Backup_rejects_categories_that_share_one_file_even_when_empty()
    {
        File.WriteAllText(Path.Combine(_root, "index.json"),
            "{\"Categories\":[{\"Name\":\"A\",\"File\":\"A.json\"},{\"Name\":\"B\",\"File\":\"a.json\"}]}");
        File.WriteAllText(Path.Combine(_root, "A.json"), "[]");
        Assert.Throws<InvalidDataException>(() => new Store(_root).ValidateBackup());
    }

    [Fact]
    public void Failed_usage_flush_retains_pending_counts_and_favorites_for_retry()
    {
        var usage = new UsageStore(_root);
        usage.Record("kept");
        usage.Flush();
        usage.Record("kept");
        usage.ToggleFavorite("kept");
        using (var locked = new FileStream(Path.Combine(_root, "usage.stats"), FileMode.Open, FileAccess.Read, FileShare.None))
            usage.Flush();
        usage.Flush();
        var saved = new UsageStore(_root);
        Assert.Equal(2, saved.CountOf("kept"));
        Assert.True(saved.IsFavorite("kept"));
    }

    [Fact]
    public void Backup_validates_referenced_categories_and_images()
    {
        var store = new Store(_root);
        var sn = new Snippet { Name = "Picture", ImagePath = "images/missing.png" };
        store.SaveCategory(new Category { Name = "A", Snippets = new() { sn } });
        Assert.Throws<InvalidDataException>(store.ValidateBackup);
        sn.ImagePath = store.SaveImage(new byte[] { 1, 2, 3 }, ".png");
        store.SaveCategory(new Category { Name = "A", Snippets = new() { sn } });
        store.ValidateBackup();
        File.Delete(Path.Combine(_root, "A.json"));
        Assert.Throws<InvalidDataException>(store.ValidateBackup);
    }

    [Fact]
    public void Export_failure_keeps_the_previous_zip_and_rejects_in_library_targets()
    {
        var library = Path.Combine(_root, "library");
        var store = new Store(library);
        store.SaveCategory(new Category { Name = "A", Snippets = new() { new() { Body = "original" } } });
        var zip = Path.Combine(_root, "backup.zip");
        LibraryBackup.Export(library, zip);
        var original = File.ReadAllBytes(zip);
        using (var locked = new FileStream(Path.Combine(library, "A.json"), FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.ThrowsAny<IOException>(() => LibraryBackup.Export(library, zip));
        Assert.Equal(original, File.ReadAllBytes(zip));
        var inside = Path.Combine(library, "backup.zip");
        File.WriteAllBytes(inside, original);
        Assert.Throws<IOException>(() => LibraryBackup.Export(library, inside));
        Assert.Equal(original, File.ReadAllBytes(inside));
    }

    [Fact]
    public void Image_cleanup_preserves_live_and_trash_references_and_unreadable_metadata()
    {
        var store = new Store(_root);
        var image = store.SaveImage(new byte[] { 1, 2, 3 }, ".png");
        var sn = new Snippet { ImagePath = image };
        store.SaveCategory(new Category { Name = "A", Snippets = new() { sn } });
        store.DeleteUnreferencedImage(image.Replace('/', '\\'));
        Assert.True(File.Exists(store.ResolveImage(image)));
        store.SaveTrash(new() { new() { Snippet = sn } });
        store.SaveCategory(new Category { Name = "A" });
        store.DeleteUnreferencedImage(image);
        Assert.True(File.Exists(store.ResolveImage(image)));
        File.WriteAllText(Path.Combine(_root, "trash.json"), "{");
        store.DeleteUnreferencedImage(image);
        Assert.True(File.Exists(store.ResolveImage(image)));
        new Store(_root).SaveTrash(new());
        store.DeleteUnreferencedImage(image);
        Assert.False(File.Exists(store.ResolveImage(image)));
    }

}
