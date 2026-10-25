using QuickText.Core.Persistence;

namespace QuickText.Core.Tests;

public class FileTreeCopyTests : IDisposable
{
    private readonly string _work =
        Path.Combine(Path.GetTempPath(), "khcopy_" + Guid.NewGuid().ToString("N"));

    public FileTreeCopyTests() => Directory.CreateDirectory(_work);
    public void Dispose() { try { Directory.Delete(_work, true); } catch { } }

    [Fact]
    public void CopyWithRollback_copies_nested_files_overwriting_destination()
    {
        var from = Path.Combine(_work, "from");
        var to = Path.Combine(_work, "to");
        Directory.CreateDirectory(Path.Combine(from, "images"));
        Directory.CreateDirectory(to);
        File.WriteAllText(Path.Combine(from, "index.json"), "new-index");
        File.WriteAllText(Path.Combine(from, "images", "a.png"), "new-image");
        File.WriteAllText(Path.Combine(to, "index.json"), "old-index");

        FileTreeCopy.CopyWithRollback(from, to);

        Assert.Equal("new-index", File.ReadAllText(Path.Combine(to, "index.json")));
        Assert.Equal("new-image", File.ReadAllText(Path.Combine(to, "images", "a.png")));
    }

    [Fact]
    public void A_mid_copy_failure_restores_overwritten_files_and_removes_new_ones()
    {
        var from = Path.Combine(_work, "from");
        var to = Path.Combine(_work, "to");
        Directory.CreateDirectory(from);
        Directory.CreateDirectory(to);
        File.WriteAllText(Path.Combine(from, "a.txt"), "new-a");
        File.WriteAllText(Path.Combine(from, "b.txt"), "new-b");
        File.WriteAllText(Path.Combine(from, "zzz-blocked"), "cannot-write");
        File.WriteAllText(Path.Combine(to, "a.txt"), "old-a");

        // A directory where the last source file wants to become a file makes File.Copy throw.
        Directory.CreateDirectory(Path.Combine(to, "zzz-blocked"));

        Assert.ThrowsAny<Exception>(() => FileTreeCopy.CopyWithRollback(from, to));

        Assert.Equal("old-a", File.ReadAllText(Path.Combine(to, "a.txt")));
        Assert.False(File.Exists(Path.Combine(to, "b.txt")));
        Assert.True(Directory.Exists(Path.Combine(to, "zzz-blocked")));
    }
}
