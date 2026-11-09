using System.Text.Json;
using QuickText.Core.Models;

namespace QuickText.Core.Persistence;

public sealed class LibraryChangedException(string message) : IOException(message);

public sealed class Store
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, object> WriteGates =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly object _writeGate;
    private readonly Dictionary<string, string> _observed = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _unreadableCategories = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _root;
    public Store(string root)
    {
        _root = root;
        _writeGate = WriteGates.GetOrAdd(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)), _ => new object());
    }

    private static string Fingerprint(string path) => File.Exists(path)
        ? Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))) : "missing";

    private void EnsureUnchanged(string path)
    {
        if (_observed.TryGetValue(path, out var expected) && expected != Fingerprint(path))
            throw new LibraryChangedException("The library file changed externally. Reload before saving: " + path);
    }

    private string ReadObserved(string path)
    {
        var bytes = File.ReadAllBytes(path);
        _observed[path] = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
        using var reader = new StreamReader(new MemoryStream(bytes), System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private void WriteObserved(string path, string content)
    {
        EnsureUnchanged(path);
        AtomicWrite(path, content);
        _observed[path] = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content)));
    }

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

    private void EnsureFreshLibrary()
    {
        if (Directory.Exists(_root) && Directory.EnumerateFiles(_root, "*.json").Any(f =>
            !string.Equals(Path.GetFileName(f), "trash.json", StringComparison.OrdinalIgnoreCase) &&
            !(IsMachineStateRoot() && string.Equals(Path.GetFileName(f), "settings.json", StringComparison.OrdinalIgnoreCase))))
            throw new InvalidDataException("Library files exist but index.json is missing.");
    }

    private static LibraryIndex ParseIndex(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.EnumerateObject()
            .Any(p => string.Equals(p.Name, "Categories", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Library index has no category list.");
        var index = JsonSerializer.Deserialize<LibraryIndex>(json, JsonConfig.Read);
        if (index?.Categories == null || index.Categories.Any(c => c == null || c.Name == null || c.File == null))
            throw new InvalidDataException("Invalid library index.");
        if (index.Categories.Select(c => c.Name).Distinct(StringComparer.Ordinal).Count() != index.Categories.Count ||
            index.Categories.Select(c => c.File).Distinct(StringComparer.OrdinalIgnoreCase).Count() != index.Categories.Count)
            throw new InvalidDataException("Duplicate category names or files.");
        return index;
    }

    /// <summary>The conflict scan runs off the UI thread, so it must not mutate
    /// <see cref="LoadWarnings"/>; null marks an unreadable index instead of an empty one.</summary>
    private LibraryIndex? LoadIndexCore(bool reportWarnings)
    {
        try
        {
            if (!File.Exists(IndexPath))
            {
                if (reportWarnings) _observed[IndexPath] = "missing";
                if (reportWarnings) EnsureFreshLibrary();
                return new LibraryIndex();
            }
            var json = reportWarnings ? ReadObserved(IndexPath) : File.ReadAllText(IndexPath);
            return ParseIndex(json);
        }
        catch (Exception ex)
        {
            if (reportWarnings) LoadWarnings.Add($"index.json: {ex.Message}");
            return null;
        }
    }

    public void SaveIndex(LibraryIndex idx)
    {
        lock (_writeGate)
        {
            WriteObserved(IndexPath, JsonSerializer.Serialize(idx, JsonConfig.Write));
        }
    }

    private LibraryIndex LoadIndexForWrite()
    {
        EnsureUnchanged(IndexPath);
        if (!File.Exists(IndexPath))
        {
            EnsureFreshLibrary();
            return new LibraryIndex();
        }
        return ParseIndex(File.ReadAllText(IndexPath));
    }

    public List<Category> LoadAll(bool strict = false)
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
                var snippets = JsonSerializer.Deserialize<List<Snippet>>(ReadObserved(path), JsonConfig.Read)
                               ?? throw new InvalidDataException("Invalid category file.");
                if (snippets.Any(s => s == null || s.Id == null || s.Name == null || s.Body == null || s.Abbr == null))
                    throw new InvalidDataException("Invalid snippet fields.");
                _unreadableCategories.Remove(path);
                result.Add(new Category { Name = refi.Name, Color = refi.Color, Snippets = snippets });
            }
            catch (Exception ex)
            {
                _unreadableCategories.Add(path);
                LoadWarnings.Add($"unreadable category file: {refi.File}: {ex.Message}");
            }
        }
        if (strict && LoadWarnings.Count > 0) throw new InvalidDataException(string.Join(Environment.NewLine, LoadWarnings));
        return result;
    }

    public void SaveCategory(Category c)
    {
        lock (_writeGate) SaveCategoryCore(c);
    }

    private void SaveCategoryCore(Category c)
    {
        var idx = LoadIndexForWrite();
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
        var categoryPath = Path.Combine(_root, file);
        if (_unreadableCategories.Contains(categoryPath)) throw new InvalidDataException("Cannot overwrite an unreadable category: " + file);
        WriteObserved(categoryPath,
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
        bool Taken(string f) => File.Exists(Path.Combine(_root, f)) ||
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
        lock (_writeGate) ReorderCategoriesCore(orderedNames);
    }

    private void ReorderCategoriesCore(List<string> orderedNames)
    {
        var idx = LoadIndexForWrite();
        idx.Categories = idx.Categories
            .OrderBy(r => { var i = orderedNames.IndexOf(r.Name); return i < 0 ? int.MaxValue : i; })
            .ToList();
        SaveIndex(idx);
    }

    public void DeleteCategory(string name)
    {
        lock (_writeGate) DeleteCategoryCore(name);
    }

    private void DeleteCategoryCore(string name)
    {
        var idx = LoadIndexForWrite();
        var refi = idx.Categories.FirstOrDefault(r => r.Name == name);
        if (refi != null)
        {
            if (TryResolveCategoryFile(refi.File, out var categoryPath)) EnsureUnchanged(categoryPath);
            idx.Categories.Remove(refi);
            SaveIndex(idx);
            if (TryResolveCategoryFile(refi.File, out var path) && File.Exists(path))
                File.Delete(path);
        }
    }

    // ---------- trash (soft delete) ----------
    public const int TrashRetentionDays = 30;
    private string TrashPath => Path.Combine(_root, "trash.json");

    /// <summary>Reference checks need unpurged entries so cleanup cannot delete an image
    /// still owned by the library or trash.</summary>
    private List<TrashEntry> ReadTrash()
    {
        List<TrashEntry> all;
        try
        {
            if (!File.Exists(TrashPath))
            {
                _observed[TrashPath] = "missing";
                return new List<TrashEntry>();
            }
            all = File.Exists(TrashPath)
                ? JsonSerializer.Deserialize<List<TrashEntry>>(ReadObserved(TrashPath), JsonConfig.Read)
                  ?? throw new InvalidDataException("Invalid trash.")
                : new List<TrashEntry>();
            if (all.Any(t => t == null || t.Snippet == null || t.Snippet.Id == null))
                throw new InvalidDataException("Invalid trash entry.");
        }
        catch (Exception ex) { throw new InvalidDataException("Cannot read trash.", ex); }
        return all;
    }

    /// <summary>Purge expired entries only after the shorter trash is safely written. Images
    /// referenced elsewhere in the library or trash remain intact.</summary>
    public List<TrashEntry> LoadTrash()
    {
        var all = ReadTrash();
        var cutoff = DateTimeOffset.UtcNow.AddDays(-TrashRetentionDays);
        var expired = all.Where(t => t.DeletedAt < cutoff).ToList();
        if (expired.Count > 0)
        {
            all = all.Where(t => t.DeletedAt >= cutoff).ToList();
            // Persist the shorter trash FIRST: if the write fails, the entries (and their images)
            // are still on disk for the next attempt instead of becoming image-less ghosts.
            SaveTrash(all);
            foreach (var t in expired)
                if (t.Snippet.IsImage) DeleteUnreferencedImage(t.Snippet.ImagePath);
        }
        return all;
    }

    public void SaveTrash(List<TrashEntry> entries)
    {
        lock (_writeGate)
        {
            WriteObserved(TrashPath, JsonSerializer.Serialize(entries, JsonConfig.Write));
        }
    }

    public void ValidateBackup()
    {
        if (!HasLibrary) throw new InvalidDataException("Backup has no index.json.");
        var cats = LoadAll(strict: true);
        if (cats.Select(c => c.Name).Distinct(StringComparer.Ordinal).Count() != cats.Count)
            throw new InvalidDataException("Duplicate category names.");
        var snippets = cats.SelectMany(c => c.Snippets).ToList();
        if (snippets.Select(s => s.Id).Distinct().Count() != snippets.Count)
            throw new InvalidDataException("Duplicate snippet identifiers.");
        foreach (var sn in snippets.Concat(ReadTrash().Select(t => t.Snippet)).Where(s => s.IsImage))
            if (ResolveImage(sn.ImagePath) is not { } image || !File.Exists(image))
                throw new InvalidDataException("Missing or unsafe image: " + sn.ImagePath);
    }

    public List<Category> ImportBackup(string source)
    {
        lock (_writeGate)
        {
            new Store(source).ValidateBackup();
            List<Category>? imported = null;
            // Keep the rollback point until the copied library is readable as a whole.
            FileTreeCopy.CopyWithRollback(source, _root, () =>
            {
                ValidateBackup();
                imported = LoadAll(strict: true);
            });
            return imported!;
        }
    }

    public void DeleteUnreferencedImage(string relativePath)
    {
        lock (_writeGate)
        {
            // A failed read leaves the file intact. Both the live library and trash can own an image.
            try
            {
                var reader = new Store(_root);
                var candidate = reader.ResolveImage(relativePath);
                if (candidate == null) return;
                var cats = reader.LoadAll(strict: true);
                var trash = reader.ReadTrash();
                if (!cats.SelectMany(c => c.Snippets).Concat(trash.Select(t => t.Snippet))
                    .Any(s => string.Equals(reader.ResolveImage(s.ImagePath), candidate, StringComparison.OrdinalIgnoreCase)))
                    DeleteImage(relativePath);
            }
            catch { /* retain the image until its references can be checked */ }
        }
    }

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
