using System.Text.Json;
using QuickText.Core.Models;

namespace QuickText.Core.Persistence;

public sealed class Store
{
    private readonly string _root;
    public Store(string root) => _root = root;

    /// <summary>Files skipped or unreadable during library/index reads. Index/category JSON is
    /// user-editable and arrives through backups and sync folders, so a bad file must not turn the
    /// whole library into a silent empty one.</summary>
    public List<string> LoadWarnings { get; } = new();

    /// <summary>The library folder this store reads and writes.</summary>
    public string Root => _root;

    private string IndexPath => Path.Combine(_root, "index.json");

    /// <summary>True once a library exists here (used to seed a starter library only on first run).</summary>
    public bool HasLibrary => File.Exists(IndexPath);

    private string ImagesDir => Path.Combine(_root, "images");

    /// <summary>The absolute image path, or null when <paramref name="relativePath"/> is rooted or
    /// escapes the library. ImagePath comes from persisted JSON, so a crafted/imported library must
    /// never make the app read or delete outside its own folder.</summary>
    public string? ResolveImage(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath)) return null;
        try
        {
            // Images live under images/ — never let an ImagePath point at index.json or another
            // library file; removing that snippet would otherwise delete the file.
            var full = Path.GetFullPath(Path.Combine(_root, relativePath));
            return IsUnder(full, ImagesDir) ? full : null;
        }
        catch { return null; }
    }

    private static bool IsUnder(string fullPath, string root)
    {
        try
        {
            var fullRoot = Path.GetFullPath(root);
            var prefix = fullRoot.EndsWith(Path.DirectorySeparatorChar)
                ? fullRoot : fullRoot + Path.DirectorySeparatorChar;
            return Path.GetFullPath(fullPath).StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private bool IsUnderRoot(string fullPath) => IsUnder(fullPath, _root);

    // Index/trash always live in the library root. settings.json only collides when the library
    // folder IS the machine-state folder (a user can point DataFolder at %APPDATA%\QuickText).
    private static readonly HashSet<string> ReservedCategoryFiles =
        new(StringComparer.OrdinalIgnoreCase) { "index.json", "trash.json" };

    private bool IsMachineStateRoot()
    {
        try
        {
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(_root));
            var state = Path.TrimEndingDirectorySeparator(Path.GetFullPath(AppPaths.MachineStateDir));
            return string.Equals(root, state, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    /// <summary>Category files are plain file names in the library root; anything else is ignored.</summary>
    private bool TryResolveCategoryFile(string file, out string fullPath)
    {
        fullPath = "";
        if (string.IsNullOrWhiteSpace(file) || Path.IsPathRooted(file)) return false;
        if (!string.Equals(Path.GetFileName(file), file, StringComparison.Ordinal)) return false;
        if (!file.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) return false;
        if (ReservedCategoryFiles.Contains(file)) return false;
        if (IsMachineStateRoot() && string.Equals(file, "settings.json", StringComparison.OrdinalIgnoreCase))
            return false;
        try
        {
            fullPath = Path.GetFullPath(Path.Combine(_root, file));
            return IsUnderRoot(fullPath);
        }
        catch { return false; }
    }

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
        try
        {
            if (ResolveImage(relativePath) is { } p && File.Exists(p)) File.Delete(p);
        }
        catch { }
    }

    public static string SanitizeFile(string categoryName)
    {
        var name = categoryName;
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        var file = name + ".json";
        // Never let a category name collide with index.json/trash.json. settings.json is only
        // reserved when the library folder is the machine-state folder; that check needs the
        // store root and lives in UniqueFileFor.
        return ReservedCategoryFiles.Contains(file) ? name + "_category.json" : file;
    }

    private static void AtomicWrite(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content);
        if (File.Exists(path)) File.Replace(tmp, path, null);
        else File.Move(tmp, path);
    }

    public LibraryIndex LoadIndex() => LoadIndexCore(reportWarnings: true) ?? new LibraryIndex();

    /// <summary>The conflict scan runs off the UI thread, so it must not mutate
    /// <see cref="LoadWarnings"/>; null marks an unreadable index instead of an empty one.</summary>
    private LibraryIndex? LoadIndexCore(bool reportWarnings)
    {
        try
        {
            if (!File.Exists(IndexPath)) return new LibraryIndex();
            return JsonSerializer.Deserialize<LibraryIndex>(File.ReadAllText(IndexPath), JsonConfig.Read)
                   ?? new LibraryIndex();
        }
        catch (Exception ex)
        {
            if (reportWarnings) LoadWarnings.Add($"index.json: {ex.Message}");
            return null;
        }
    }

    public void SaveIndex(LibraryIndex idx) =>
        AtomicWrite(IndexPath, JsonSerializer.Serialize(idx, JsonConfig.Write));

    public List<Category> LoadAll()
    {
        Directory.CreateDirectory(_root);
        LoadWarnings.Clear();
        var idx = LoadIndex();
        var result = new List<Category>();
        foreach (var refi in idx.Categories)
        {
            if (!TryResolveCategoryFile(refi.File, out var path))
            {
                LoadWarnings.Add($"unsafe category file: {refi.File}");
                continue;
            }
            try
            {
                if (!File.Exists(path))
                {
                    LoadWarnings.Add($"missing category file: {refi.File}");
                    continue;
                }
                var snippets = JsonSerializer.Deserialize<List<Snippet>>(File.ReadAllText(path), JsonConfig.Read)
                               ?? new List<Snippet>();
                result.Add(new Category { Name = refi.Name, Color = refi.Color, Snippets = snippets });
            }
            catch (Exception ex)
            {
                LoadWarnings.Add($"unreadable category file: {refi.File}: {ex.Message}");
            }
        }
        return result;
    }

    public void SaveCategory(Category c)
    {
        var idx = LoadIndex();
        var existing = idx.Categories.FirstOrDefault(r => r.Name == c.Name);
        string file;
        bool repaired = false;
        if (existing != null && TryResolveCategoryFile(existing.File, out _))
        {
            file = existing.File;
        }
        else
        {
            file = UniqueFileFor(c.Name, idx);
            if (existing != null)
            {
                // A hand-edited/imported index can point anywhere; keep the category, move its
                // file back into the library instead of writing through the old path.
                existing.File = file;
                repaired = true;
            }
        }
        AtomicWrite(Path.Combine(_root, file),
            JsonSerializer.Serialize(c.Snippets, JsonConfig.Write));
        if (existing == null)
        {
            idx.Categories.Add(new CategoryRef { Name = c.Name, File = file, Color = c.Color });
            SaveIndex(idx);
        }
        else if (existing.Color != c.Color || repaired)
        {
            existing.Color = c.Color;
            SaveIndex(idx);
        }
    }

    private string UniqueFileFor(string categoryName, LibraryIndex idx)
    {
        var baseFile = SanitizeFile(categoryName);
        if (IsMachineStateRoot() && string.Equals(baseFile, "settings.json", StringComparison.OrdinalIgnoreCase))
            baseFile = "settings_category.json";
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
            if (TryResolveCategoryFile(refi.File, out var path) && File.Exists(path))
                File.Delete(path);
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
            all = all.Where(t => t.DeletedAt >= cutoff).ToList();
            // Persist the shorter trash FIRST: if the write fails, the entries (and their images)
            // are still on disk for the next attempt instead of becoming image-less ghosts.
            SaveTrash(all);
            foreach (var t in expired)
                if (t.Snippet.IsImage) DeleteImage(t.Snippet.ImagePath);
        }
        return all;
    }

    public void SaveTrash(List<TrashEntry> entries) =>
        AtomicWrite(TrashPath, JsonSerializer.Serialize(entries, JsonConfig.Write));

    public IReadOnlyList<string> FindConflictFiles()
    {
        if (!Directory.Exists(_root)) return Array.Empty<string>();
        if (LoadIndexCore(reportWarnings: false) is not { } index) return Array.Empty<string>();
        var indexed = new HashSet<string>(
            index.Categories.Select(r => r.File), StringComparer.OrdinalIgnoreCase);
        return Directory.GetFiles(_root, "*.json")
            .Select(Path.GetFileName)
            .Where(f => f != null && !indexed.Contains(f)
                        && (f.Contains("conflict", StringComparison.OrdinalIgnoreCase)
                            || f.Contains("冲突")
                            || f.Contains(" (1)")))
            .ToList()!;
    }
}
