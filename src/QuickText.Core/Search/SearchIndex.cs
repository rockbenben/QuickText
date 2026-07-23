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

    public void Build(IEnumerable<Category> categories)
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
