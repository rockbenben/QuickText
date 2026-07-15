using System.Text.Json;

namespace QuickText.Core.Persistence;

public sealed class UsageEntry
{
    public int Count { get; set; }
    public DateTimeOffset LastUsed { get; set; }
}

public sealed class UsageStats
{
    public Dictionary<string, UsageEntry> Usage { get; set; } = new();
    public List<string> Favorites { get; set; } = new();
}

/// <summary>
/// Per-snippet usage counts + favorites, persisted next to the library.
/// Filename is deliberately NOT *.json so the data-folder watcher ignores our own writes.
/// </summary>
public sealed class UsageStore
{
    private readonly string _path;
    private readonly UsageStats _stats;
    private readonly object _flushLock = new();
    private bool _dirty;
    private bool _flushScheduled;

    public UsageStore(string root)
    {
        _path = Path.Combine(root, AppPaths.UsageFileName);
        _stats = Load();
    }

    private UsageStats Load()
    {
        try
        {
            return File.Exists(_path)
                ? JsonSerializer.Deserialize<UsageStats>(File.ReadAllText(_path), JsonConfig.Read) ?? new UsageStats()
                : new UsageStats();
        }
        catch { return new UsageStats(); }
    }

    // Usage is bumped on every single send — debounce the disk write instead of
    // paying a synchronous WriteAllText on the hot send path.
    private void ScheduleSave()
    {
        lock (_flushLock)
        {
            _dirty = true;
            if (_flushScheduled) return;
            _flushScheduled = true;
        }
        Task.Delay(800).ContinueWith(_ => Flush());
    }

    /// <summary>Write pending changes now (no-op when clean). Call before replacing the store or on exit.</summary>
    public void Flush()
    {
        string json;
        lock (_flushLock)
        {
            _flushScheduled = false;
            if (!_dirty) return;
            _dirty = false;
            json = JsonSerializer.Serialize(_stats, JsonConfig.Write);
        }
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, json);
        }
        catch { }
    }

    public void Record(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        // Mutate under the same lock the background Flush serializes under: Record runs on
        // the UI thread while Flush's JsonSerializer.Serialize enumerates _stats on a
        // threadpool thread, and an unsynchronized dictionary write during that enumeration
        // throws "Collection was modified", dropping the flush (usage data loss).
        lock (_flushLock)
        {
            if (!_stats.Usage.TryGetValue(id, out var e)) { e = new UsageEntry(); _stats.Usage[id] = e; }
            e.Count++;
            e.LastUsed = DateTimeOffset.UtcNow;
        }
        ScheduleSave();
    }

    /// <summary>Snippet ids ordered by most-used then most-recent.</summary>
    public IReadOnlyList<string> TopIds(int n) =>
        _stats.Usage
            .OrderByDescending(kv => kv.Value.Count)
            .ThenByDescending(kv => kv.Value.LastUsed)
            .Take(n)
            .Select(kv => kv.Key)
            .ToList();

    public int CountOf(string id) => _stats.Usage.TryGetValue(id, out var e) ? e.Count : 0;

    public DateTimeOffset? LastUsedOf(string id) =>
        _stats.Usage.TryGetValue(id, out var e) ? e.LastUsed : null;

    public bool IsFavorite(string id) => _stats.Favorites.Contains(id);

    public void ToggleFavorite(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        // Same reason as Record(): the background Flush serializes _stats.Favorites on a
        // threadpool thread, so mutate it under _flushLock to avoid a concurrent-modification
        // throw that would silently drop the favorite change.
        lock (_flushLock)
        {
            if (!_stats.Favorites.Remove(id)) _stats.Favorites.Add(id);
        }
        ScheduleSave();
    }

    public IReadOnlyList<string> FavoriteIds => _stats.Favorites;
}
