namespace QuickText.Core.Pinyin;

/// <summary>
/// Memoizing decorator over an <see cref="IPinyinProvider"/>. Snippet names rarely change,
/// but the search index is rebuilt on every save / hot-reload — caching per input string
/// makes rebuilds O(changed) instead of recomputing pinyin for the whole library.
/// Not thread-safe by design: Build runs on the UI thread only.
/// </summary>
public sealed class CachedPinyinProvider : IPinyinProvider
{
    private const int MaxEntries = 10_000;   // safety valve; a library rarely exceeds this

    private readonly IPinyinProvider _inner;
    private readonly Dictionary<string, (string Full, PinyinMap Map)> _cache = new();

    public CachedPinyinProvider(IPinyinProvider inner) => _inner = inner;

    public string GetFullPinyin(string text) => Get(text).Full;

    // Initials come off the cached map rather than a separately cached string: one fewer thing to
    // keep in sync, and the map is what the highlighter needs anyway on the very same lookup.
    public string GetInitials(string text) => Get(text).Map.Initials;

    public PinyinMap GetMap(string text) => Get(text).Map;

    private (string Full, PinyinMap Map) Get(string text)
    {
        text ??= "";
        if (_cache.TryGetValue(text, out var hit)) return hit;
        if (_cache.Count >= MaxEntries) _cache.Clear();
        var computed = (_inner.GetFullPinyin(text), _inner.GetMap(text));
        _cache[text] = computed;
        return computed;
    }
}
