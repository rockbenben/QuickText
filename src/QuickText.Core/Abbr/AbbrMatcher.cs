using System.Text;
using QuickText.Core.Models;

namespace QuickText.Core.Abbr;

public sealed record AbbrMatch(int BackspaceCount, string Abbr, Snippet Snippet);

public sealed class AbbrMatcher
{
    private readonly int _maxBuffer;
    private readonly StringBuilder _buffer = new();
    // Case-insensitive: ";QM" typed with CapsLock on must still trigger the "qm" snippet.
    private Dictionary<string, Snippet> _abbrs = new(StringComparer.OrdinalIgnoreCase);
    private int _maxAbbrLen;

    public AbbrMatcher(int maxBuffer = 32) => _maxBuffer = maxBuffer;

    /// <summary>
    /// Rebuild the trigger table. <paramref name="prefix"/> is prepended to every stored
    /// abbreviation, so a snippet whose Abbr is "qm" triggers on "&lt;prefix&gt;qm" — the user
    /// sets the prefix once and types only the bare abbreviation. Duplicate abbreviations
    /// collide silently (last one wins); the Manager warns about them at edit time.
    /// </summary>
    public void Rebuild(IEnumerable<Snippet> snippets, string prefix = "")
    {
        _abbrs = new Dictionary<string, Snippet>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in snippets)
            if (!string.IsNullOrEmpty(s.Abbr))
                _abbrs[prefix + s.Abbr] = s;
        _maxAbbrLen = _abbrs.Keys.Count == 0 ? 0 : _abbrs.Keys.Max(k => k.Length);
        _buffer.Clear();
    }

    /// <summary>No triggers registered — callers can skip per-keystroke work entirely.</summary>
    public bool IsEmpty => _abbrs.Count == 0;

    public void FeedChar(char c)
    {
        _buffer.Append(c);
        if (_buffer.Length > _maxBuffer)
            _buffer.Remove(0, _buffer.Length - _maxBuffer);
    }

    /// <summary>Drop the last buffered char, so a typo corrected with Backspace can still expand.</summary>
    public void Backspace()
    {
        if (_buffer.Length > 0) _buffer.Remove(_buffer.Length - 1, 1);
    }

    public void Reset() => _buffer.Clear();

    public AbbrMatch? OnTerminator()
    {
        var tail = _buffer.ToString();
        AbbrMatch? best = null;
        for (int len = Math.Min(_maxAbbrLen, tail.Length); len >= 1; len--)
        {
            var candidate = tail[^len..];
            if (!_abbrs.TryGetValue(candidate, out var snippet)) continue;
            // Require a word boundary before the abbr — but only when the abbr itself starts
            // with a word char, so bare "af" won't fire inside "graf". A punctuation-prefixed
            // abbr (";qm") is self-delimiting and may follow a letter (e.g. "Hi;qm"), which is
            // exactly what the prefix convention is for.
            int pos = tail.Length - len;
            if (pos > 0 && char.IsLetterOrDigit(tail[pos - 1]) && char.IsLetterOrDigit(candidate[0])) continue;
            best = new AbbrMatch(len, candidate, snippet);   // Abbr keeps the TYPED casing for undo
            break;
        }
        Reset();
        return best;
    }
}
