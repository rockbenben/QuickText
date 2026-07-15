using System.Text.Json;
using QuickText.Core.Models;

namespace QuickText.Core.Persistence;

public sealed class Store
{
    private readonly string _root;
    public Store(string root) => _root = root;

    private string IndexPath => Path.Combine(_root, "index.json");

    /// <summary>True once a library exists here (used to seed a starter library only on first run).</summary>
    public bool HasLibrary => File.Exists(IndexPath);

    private string ImagesDir => Path.Combine(_root, "images");

    public string ResolveImage(string relativePath) => Path.Combine(_root, relativePath);

    /// <summary>Store image bytes under images/ and return the relative path.</summary>
    public string SaveImage(byte[] data, string extension)
    {
        Directory.CreateDirectory(ImagesDir);
        var name = Guid.NewGuid().ToString("N") + extension;
        File.WriteAllBytes(Path.Combine(ImagesDir, name), data);
        return "images/" + name;
    }

    /// <summary>Delete an image file (best effort) — used when a snippet's image is removed/replaced/deleted.</summary>
    public void DeleteImage(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return;
        try { var p = ResolveImage(relativePath); if (File.Exists(p)) File.Delete(p); } catch { }
    }

    public static string SanitizeFile(string categoryName)
    {
        var name = categoryName;
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name + ".json";
    }

    private static void AtomicWrite(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content);
        if (File.Exists(path)) File.Replace(tmp, path, null);
        else File.Move(tmp, path);
    }

    public LibraryIndex LoadIndex()
    {
        try
        {
            if (!File.Exists(IndexPath)) return new LibraryIndex();
            return JsonSerializer.Deserialize<LibraryIndex>(File.ReadAllText(IndexPath), JsonConfig.Read)
                   ?? new LibraryIndex();
        }
        catch { return new LibraryIndex(); }
    }

    public void SaveIndex(LibraryIndex idx) =>
        AtomicWrite(IndexPath, JsonSerializer.Serialize(idx, JsonConfig.Write));

    public List<Category> LoadAll()
    {
        Directory.CreateDirectory(_root);
        var idx = LoadIndex();
        var result = new List<Category>();
        foreach (var refi in idx.Categories)
        {
            var path = Path.Combine(_root, refi.File);
            try
            {
                if (!File.Exists(path)) continue;
                var snippets = JsonSerializer.Deserialize<List<Snippet>>(File.ReadAllText(path), JsonConfig.Read)
                               ?? new List<Snippet>();
                result.Add(new Category { Name = refi.Name, Color = refi.Color, Snippets = snippets });
            }
            catch { /* skip corrupt file, keep app usable */ }
        }
        return result;
    }

    public void SaveCategory(Category c)
    {
        var idx = LoadIndex();
        var existing = idx.Categories.FirstOrDefault(r => r.Name == c.Name);
        string file = existing?.File ?? UniqueFileFor(c.Name, idx);
        AtomicWrite(Path.Combine(_root, file),
            JsonSerializer.Serialize(c.Snippets, JsonConfig.Write));
        if (existing == null)
        {
            idx.Categories.Add(new CategoryRef { Name = c.Name, File = file, Color = c.Color });
            SaveIndex(idx);
        }
        else if (existing.Color != c.Color)
        {
            existing.Color = c.Color;
            SaveIndex(idx);
        }
    }

    private static string UniqueFileFor(string categoryName, LibraryIndex idx)
    {
        var baseFile = SanitizeFile(categoryName);
        bool Taken(string f) =>
            idx.Categories.Any(r => string.Equals(r.File, f, StringComparison.OrdinalIgnoreCase));
        if (!Taken(baseFile)) return baseFile;
        var stem = Path.GetFileNameWithoutExtension(baseFile);
        for (int i = 2; ; i++)
        {
            var candidate = $"{stem}_{i}.json";
            if (!Taken(candidate)) return candidate;
        }
    }

    /// <summary>Persist a new category display order (index.json order == display order).</summary>
    public void ReorderCategories(List<string> orderedNames)
    {
        var idx = LoadIndex();
        idx.Categories = idx.Categories
            .OrderBy(r => { var i = orderedNames.IndexOf(r.Name); return i < 0 ? int.MaxValue : i; })
            .ToList();
        SaveIndex(idx);
    }

    public void DeleteCategory(string name)
    {
        var idx = LoadIndex();
        var refi = idx.Categories.FirstOrDefault(r => r.Name == name);
        if (refi != null)
        {
            var path = Path.Combine(_root, refi.File);
            if (File.Exists(path)) File.Delete(path);
            idx.Categories.Remove(refi);
            SaveIndex(idx);
        }
    }

    // ---------- trash (soft delete) ----------
    public const int TrashRetentionDays = 30;
    private string TrashPath => Path.Combine(_root, "trash.json");

    /// <summary>
    /// Load the trash, silently purging entries older than <see cref="TrashRetentionDays"/>
    /// (purged image snippets take their image file with them). Callers that persist the
    /// returned list back should MarkSelfWrite around SaveTrash.
    /// </summary>
    public List<TrashEntry> LoadTrash()
    {
        List<TrashEntry> all;
        try
        {
            all = File.Exists(TrashPath)
                ? JsonSerializer.Deserialize<List<TrashEntry>>(File.ReadAllText(TrashPath), JsonConfig.Read) ?? new()
                : new List<TrashEntry>();
        }
        catch { return new List<TrashEntry>(); }

        var cutoff = DateTimeOffset.UtcNow.AddDays(-TrashRetentionDays);
        var expired = all.Where(t => t.DeletedAt < cutoff).ToList();
        if (expired.Count > 0)
        {
            foreach (var t in expired)
                if (t.Snippet.IsImage) DeleteImage(t.Snippet.ImagePath);
            all = all.Where(t => t.DeletedAt >= cutoff).ToList();
            SaveTrash(all);
        }
        return all;
    }

    public void SaveTrash(List<TrashEntry> entries) =>
        AtomicWrite(TrashPath, JsonSerializer.Serialize(entries, JsonConfig.Write));

    public IReadOnlyList<string> FindConflictFiles()
    {
        if (!Directory.Exists(_root)) return Array.Empty<string>();
        return Directory.GetFiles(_root, "*.json")
            .Select(Path.GetFileName)
            .Where(f => f!.Contains("conflict", StringComparison.OrdinalIgnoreCase)
                        || f!.Contains("冲突")
                        || f!.Contains(" (1)"))
            .ToList()!;
    }
}
