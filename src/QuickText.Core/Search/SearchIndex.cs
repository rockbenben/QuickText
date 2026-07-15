using QuickText.Core.Models;
using QuickText.Core.Pinyin;

namespace QuickText.Core.Search;

public sealed record SearchHit(Snippet Snippet, string Category, int Score);

public sealed class SearchIndex
{
    private readonly IPinyinProvider _pinyin;
    private readonly List<Entry> _entries = new();

    public SearchIndex(IPinyinProvider pinyin) => _pinyin = pinyin;

    /// <summary>
    /// Optional usage-count source. Match quality still dominates ranking; this only
    /// orders hits WITHIN the same score tier, so the snippets the user actually
    /// sends float to the top of equally-good matches (frequency learning).
    /// </summary>
    public Func<string, int>? UsageOf { get; set; }

    private sealed record Entry(
        Snippet Snippet, string Category,
        string NameLower, string PinyinFull, string Initials, string AbbrLower, string BodyLower);

    public void Build(IEnumerable<Category> categories)
    {
        _entries.Clear();
        foreach (var cat in categories)
        foreach (var s in cat.Snippets)
        {
            _entries.Add(new Entry(
                s, cat.Name,
                s.Name.ToLowerInvariant(),
                _pinyin.GetFullPinyin(s.Name),
                _pinyin.GetInitials(s.Name),
                s.Abbr.ToLowerInvariant(),
                s.Body.ToLowerInvariant()));
        }
    }

    /// <summary>
    /// Search; <paramref name="category"/> (from an <c>@分类</c> query prefix) narrows the scan
    /// to categories whose name starts with it (falls back to contains), case-insensitive.
    /// </summary>
    public IReadOnlyList<SearchHit> Search(string query, int limit = 200, string? category = null)
    {
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
            int score = ScoreOf(e, q);
            if (score > 0) hits.Add(new SearchHit(e.Snippet, e.Category, score));
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
    public bool HasCategory(string category) => FilterByCategory(category).Count > 0;

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

    private static int ScoreOf(Entry e, string q)
    {
        if (e.NameLower == q) return 1000;
        if (!string.IsNullOrEmpty(e.AbbrLower) && e.AbbrLower.Contains(q)) return 700;
        if (e.NameLower.StartsWith(q)) return 600;
        if (e.Initials.Contains(q)) return 500;
        if (e.NameLower.Contains(q)) return 450;
        if (e.PinyinFull.Contains(q)) return 400;
        if (e.BodyLower.Contains(q)) return 100;
        return 0;
    }
}
