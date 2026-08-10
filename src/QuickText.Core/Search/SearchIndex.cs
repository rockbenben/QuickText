using QuickText.Core.Models;
using QuickText.Core.Pinyin;

namespace QuickText.Core.Search;

/// <summary>Which field earned a hit its score. The panel shows the reason — a result that came
/// back for a query appearing nowhere in its visible text is otherwise unexplained.</summary>
public enum MatchKind
{
    None = 0,
    /// <summary>Query appears literally in the name.</summary>
    Name,
    /// <summary>Query appears in the abbreviation.</summary>
    Abbr,
    /// <summary>Query matched the name's pinyin initials.</summary>
    Initials,
    /// <summary>Query matched the name's full pinyin.</summary>
    Pinyin,
    /// <summary>Query appears in the body only.</summary>
    Body,
}

/// <param name="NameStart">Start of the matched run within <c>Snippet.Name</c>, or -1 when the
/// match cannot be pinned to specific name characters (an abbr- or body-only hit, or a full-pinyin
/// hit the romanization map could not locate). Never guess a span: highlighting the wrong
/// characters is worse than highlighting none.</param>
public sealed record SearchHit(
    Snippet Snippet, string Category, int Score,
    MatchKind Kind = MatchKind.None, int NameStart = -1, int NameLength = 0);

public sealed class SearchIndex
{
    private readonly IPinyinProvider _pinyin;
    private readonly List<Entry> _entries = new();

    // The in-flight (or last) background build, and the lock that keeps builds serialized and
    // readers joined to the newest one. See Build for why indexing runs off the caller's thread.
    private Task? _building;
    private readonly object _buildLock = new();

    /// <summary>Has the pending build finished? A UI-thread caller must check this BEFORE reading:
    /// the readers below join the build unconditionally, and the app's UI thread is the one its two
    /// WH_KEYBOARD_LL hooks are dispatched on — block it past Windows' LowLevelHooksTimeout (300ms)
    /// and the OS drops those hooks for the rest of the session, killing abbreviation expansion and
    /// tap-summon silently. Skip the read and wait for <see cref="BuildCompleted"/> instead.
    /// <para>A bounded wait inside the readers was tried and is the wrong shape: it makes Search
    /// answer from a half-built index, i.e. return "no matches" for snippets that exist, which is a
    /// worse failure than being briefly unavailable and one the caller cannot even detect.</para></summary>
    public bool IsBuilt
    {
        get { lock (_buildLock) return _building is null or { IsCompleted: true }; }
    }

    /// <summary>Raised on a background thread when a build finishes — carrying the exception if it
    /// failed, null if it succeeded. Both cases need an owner: a failed build leaves an index that
    /// answers every query with nothing, which the user cannot tell apart from an empty library
    /// unless something says so; and a reader that hit <see cref="ReaderJoinTimeout"/> needs to know
    /// when there is finally something to re-query.</summary>
    public event Action<Exception?>? BuildCompleted;

    public SearchIndex(IPinyinProvider pinyin) => _pinyin = pinyin;

    /// <summary>
    /// Optional usage-count source. Match quality still dominates ranking; this only
    /// orders hits WITHIN the same score tier, so the snippets the user actually
    /// sends float to the top of equally-good matches (frequency learning).
    /// </summary>
    public Func<string, int>? UsageOf { get; set; }

    private sealed record Entry(
        Snippet Snippet, string Category,
        string NameLower, string PinyinFull, string Initials, string AbbrLower, string BodyLower,
        PinyinMap Map);

    /// <summary>
    /// Index the given library. The work happens on a BACKGROUND thread and every reader
    /// (<see cref="Search"/>, <see cref="HasCategory"/>) joins it first, so callers still observe
    /// a fully built index and need no change.
    /// <para>Why: the very first pinyin lookup pays for ToolGood.Words' dictionary load, measured
    /// at 766ms — over half of the app's whole startup — while indexing the snippets themselves
    /// costs 3ms after it. That cost is fixed, independent of library size, and nothing can be
    /// searched until the user summons the panel, which is seconds away at the earliest. Running
    /// it off the startup path overlaps it with the window and tray work instead of adding to it.
    /// </para>
    /// <para>Builds are chained rather than run in parallel: a save triggers another Build, and two
    /// concurrent writers to <c>_entries</c> (or two first-callers into the pinyin library) would
    /// race. Readers join under the same lock, so they always see the LATEST build.</para>
    /// </summary>
    public void Build(IEnumerable<Category> categories)
    {
        // Snapshot before handing to another thread: the Manager keeps editing its own list.
        var snapshot = categories.ToList();
        lock (_buildLock)
        {
            // Chained with ContinueWith, NOT `previous.Wait()` inside the task: waiting parks a whole
            // thread-pool worker per queued build doing nothing but blocking, and the Manager queues
            // one on every drag-reorder, batch move, save and close. A burst of those starves the
            // pool, so the newest build is not even scheduled while a reader is waiting on it.
            _building = _building == null
                ? Task.Run(() => BuildGuarded(snapshot))
                : _building.ContinueWith(_ => BuildGuarded(snapshot), TaskScheduler.Default);
        }
    }

    /// <summary>Run one build without ever faulting the task. A faulted task would rethrow inside
    /// whatever touched <see cref="Search"/> next — i.e. crash the panel on a keystroke — so the
    /// failure travels out through <see cref="BuildCompleted"/> instead.
    /// <para>Reported, not swallowed. While this ran synchronously the exception reached
    /// App.OnStartup, which came up empty AND warned the user. Dropping it on a thread-pool thread
    /// would leave someone whose pinyin dictionary failed to load with a Manager listing every
    /// snippet, working abbreviations, and a search panel that finds none of them — with nothing
    /// anywhere to explain it.</para></summary>
    private void BuildGuarded(List<Category> snapshot)
    {
        Exception? error = null;
        try { BuildCore(snapshot); }
        catch (Exception ex) { error = ex; _entries.Clear(); }
        BuildCompleted?.Invoke(error);
    }

    /// <summary>Block until the pending build has finished, so every reader sees a COMPLETE index —
    /// a partial answer here is indistinguishable from "that snippet does not exist". Free once the
    /// build is done: waiting on a completed task returns immediately. A caller that cannot afford
    /// to block (the UI thread — see <see cref="IsBuilt"/>) must check first rather than ask for a
    /// shorter wait.</summary>
    private void EnsureBuilt()
    {
        Task? pending;
        lock (_buildLock) pending = _building;
        pending?.Wait();
    }

    private void BuildCore(List<Category> categories)
    {
        _entries.Clear();
        foreach (var cat in categories)
        foreach (var s in cat.Snippets)
        {
            // One map lookup serves both the initials the scorer matches on and the character
            // positions the UI highlights — and on the cached provider it's the same memo entry.
            var map = _pinyin.GetMap(s.Name);
            _entries.Add(new Entry(
                s, cat.Name,
                s.Name.ToLowerInvariant(),
                _pinyin.GetFullPinyin(s.Name),
                map.Initials,
                s.Abbr.ToLowerInvariant(),
                s.Body.ToLowerInvariant(),
                map));
        }
    }

    /// <summary>
    /// Search; <paramref name="category"/> (from an <c>@分类</c> query prefix) narrows the scan
    /// to categories whose name starts with it (falls back to contains), case-insensitive.
    /// </summary>
    public IReadOnlyList<SearchHit> Search(string query, int limit = 200, string? category = null)
    {
        EnsureBuilt();
        var entries = FilterByCategory(category);

        if (string.IsNullOrWhiteSpace(query))
            return entries
                .OrderByDescending(e => e.Snippet.UpdatedAt)
                .Take(limit)
                .Select(e => new SearchHit(e.Snippet, e.Category, 0))
                .ToList();

        var q = query.Trim().ToLowerInvariant();
        var hits = new List<SearchHit>();
        foreach (var e in entries)
        {
            var m = Match(e, q);
            if (m.Score > 0)
                hits.Add(new SearchHit(e.Snippet, e.Category, m.Score, m.Kind, m.Start, m.Length));
        }
        var usage = UsageOf;
        return hits
            .OrderByDescending(h => h.Score)
            .ThenByDescending(h => usage?.Invoke(h.Snippet.Id) ?? 0)
            .ThenByDescending(h => h.Snippet.UpdatedAt)
            .Take(limit)
            .ToList();
    }

    /// <summary>Would an "@category" filter match anything? Callers fall back to a literal search when not.</summary>
    public bool HasCategory(string category)
    {
        EnsureBuilt();
        return FilterByCategory(category).Count > 0;
    }

    private IReadOnlyList<Entry> FilterByCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category)) return _entries;
        var byPrefix = _entries
            .Where(e => e.Category.StartsWith(category, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (byPrefix.Count > 0) return byPrefix;
        return _entries
            .Where(e => e.Category.Contains(category, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>Score a query against one entry AND report why it matched. The tier order is
    /// unchanged from when this only returned a score; each tier now also names the field and,
    /// where the matched characters are exactly derivable, their range within the name.</summary>
    private static (int Score, MatchKind Kind, int Start, int Length) Match(Entry e, string q)
    {
        if (e.NameLower == q) return (1000, MatchKind.Name, 0, e.Snippet.Name.Length);
        if (!string.IsNullOrEmpty(e.AbbrLower) && e.AbbrLower.Contains(q))
            return (700, MatchKind.Abbr, -1, 0);
        if (e.NameLower.StartsWith(q)) return (600, MatchKind.Name, 0, q.Length);

        int initialsAt = e.Initials.IndexOf(q, StringComparison.Ordinal);
        if (initialsAt >= 0)
        {
            // Initials are one character per map entry, so the query's first and last initials
            // index directly into the map — no scanning, no ambiguity.
            var (start, length) = e.Map.SourceSpan(initialsAt, initialsAt + q.Length - 1);
            return (500, MatchKind.Initials, start, Clamp(e.Snippet.Name, start, length));
        }

        int nameAt = e.NameLower.IndexOf(q, StringComparison.Ordinal);
        if (nameAt >= 0) return (450, MatchKind.Name, nameAt, q.Length);

        if (e.PinyinFull.Contains(q))
        {
            // The scorer matches against GetFullPinyin, but the character map is built from a
            // different romanization path, so the query may not appear in the map's own
            // concatenation. Locate it there when possible; otherwise report the kind with no
            // span rather than a fabricated one.
            int joinedAt = e.Map.Joined.IndexOf(q, StringComparison.Ordinal);
            int from = e.Map.SyllableAt(joinedAt);
            int to = e.Map.SyllableAt(joinedAt + q.Length - 1);
            if (from >= 0 && to >= from)
            {
                var (start, length) = e.Map.SourceSpan(from, to);
                return (400, MatchKind.Pinyin, start, Clamp(e.Snippet.Name, start, length));
            }
            return (400, MatchKind.Pinyin, -1, 0);
        }

        if (e.BodyLower.Contains(q)) return (100, MatchKind.Body, -1, 0);
        return (0, MatchKind.None, -1, 0);
    }

    /// <summary>Keep a span inside the name. The map is built from the name, so this should always
    /// be a no-op; it exists so a romanization change can never hand the UI an out-of-range span.</summary>
    private static int Clamp(string name, int start, int length) =>
        start < 0 || start >= name.Length ? 0 : Math.Min(length, name.Length - start);
}
