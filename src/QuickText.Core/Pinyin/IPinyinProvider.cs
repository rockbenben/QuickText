namespace QuickText.Core.Pinyin;

/// <summary>
/// Per-character romanization of a string: <c>Syllables[i]</c> is the lowercase pinyin of the
/// character at <c>SourceIndex[i]</c> in the original text. A contiguous run of non-Chinese
/// characters contributes ONE entry — its leading character — mirroring
/// <see cref="IPinyinProvider.GetInitials"/>, so the two can never disagree.
/// <para>This exists so the UI can say WHICH characters a pinyin query matched rather than only
/// that one did: searching "q" and getting 请假条 back is unexplained without it.</para>
/// </summary>
public sealed record PinyinMap(IReadOnlyList<string> Syllables, IReadOnlyList<int> SourceIndex)
{
    public static readonly PinyinMap Empty = new(Array.Empty<string>(), Array.Empty<int>());

    private string? _initials;
    private string? _joined;

    /// <summary>The initials string this map produces — the first letter of every syllable.
    /// Memoized: the search index asks for it once per snippet on every rebuild.</summary>
    public string Initials => _initials ??= string.Concat(Syllables.Where(s => s.Length > 0).Select(s => s[0]));

    /// <summary>All syllables joined, for locating a full-pinyin query inside this map's own
    /// concatenation. Deliberately NOT assumed equal to <see cref="IPinyinProvider.GetFullPinyin"/>:
    /// that method has its own implementation, and a caller that cannot find its query here simply
    /// gets no character span rather than a wrong one.</summary>
    public string Joined => _joined ??= string.Concat(Syllables);

    /// <summary>Index of the syllable containing position <paramref name="joinedIndex"/> of
    /// <see cref="Joined"/>, or -1 when out of range.</summary>
    public int SyllableAt(int joinedIndex)
    {
        if (joinedIndex < 0) return -1;
        int at = 0;
        for (int i = 0; i < Syllables.Count; i++)
        {
            at += Syllables[i].Length;
            if (joinedIndex < at) return i;
        }
        return -1;
    }

    /// <summary>Source character range covered by syllables <paramref name="from"/>..<paramref name="to"/>
    /// inclusive, as (start, length) in the original text. Returns (-1, 0) when out of range.</summary>
    public (int Start, int Length) SourceSpan(int from, int to)
    {
        if (from < 0 || to < from || to >= SourceIndex.Count) return (-1, 0);
        int start = SourceIndex[from];
        // The run ends where the NEXT mapped character begins; the last entry extends one character
        // (the caller clamps to the text length). Using the next index keeps a multi-character
        // non-Chinese run — which collapses to a single entry — fully covered.
        int end = to + 1 < SourceIndex.Count ? SourceIndex[to + 1] : start + 1;
        return (start, Math.Max(1, end - start));
    }
}

public interface IPinyinProvider
{
    string GetFullPinyin(string text);
    string GetInitials(string text);

    /// <summary>Per-character romanization; see <see cref="PinyinMap"/>.</summary>
    PinyinMap GetMap(string text);
}
