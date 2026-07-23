using System.Text;
using ToolGood.Words;

namespace QuickText.Core.Pinyin;

/// <summary>
/// Wraps <see cref="WordsHelper"/> to produce lowercase, no-separator pinyin.
/// </summary>
/// <remarks>
/// API adaptation notes (ToolGood.Words 3.1.0.3, net10.0):
/// - <c>WordsHelper.GetPinyin(text, splitSpan, tone)</c> is the actual overload (the brief's
///   two-arg <c>GetPinyin(text, "")</c> without the <c>tone</c> bool does not exist in this
///   version). Calling it with <c>splitSpan: ""</c>, <c>tone: false</c> yields e.g. "NiHao" for
///   "你好" and "Hi" for "Hi" (ASCII passes through unchanged) — lowering the result gives the
///   exact test-required output.
/// - <c>WordsHelper.GetFirstPinyin(text)</c> does NOT reduce ASCII runs to their first letter —
///   it returns the original ASCII text unchanged (e.g. "Hi" -> "Hi", not "H"). Since the
///   contract requires initials of ASCII text to be a single leading letter (e.g. "Hi" -> "h"),
///   GetInitials is implemented character-by-character.
/// - Polyphone fix: a non-contextual per-character lookup (<c>GetAllPinyin(c, false)[0]</c>)
///   picks the wrong syllable for polyphone characters depending on context, e.g. "音乐"
///   (yinyue) needs 乐 = "yue" but the non-contextual first candidate is "le"; "重庆"
///   (chongqing) needs 重 = "chong" but the non-contextual first candidate is "zhong". The
///   word-aware <c>WordsHelper.GetPinyinList(text, false)</c> resolves these correctly (verified
///   empirically: "音乐" -> ["Yin","Yue"], "重庆" -> ["Chong","Qing"]) and, for every input
///   verified (including mixed CJK/ASCII like "AB你好" and "你好world"), returns exactly one
///   entry per <see cref="char"/> in <paramref name="text"/> (index-aligned, ASCII chars get
///   their own single-letter entry). GetInitials therefore prefers this contextual list for the
///   per-character syllable and only falls back to the non-contextual per-char lookup if the
///   list is ever not index-aligned with the input, so behavior degrades gracefully rather than
///   crashing or misaligning. Chinese-vs-not detection still uses
///   <c>WordsHelper.GetAllPinyin(char, tone)</c> (empty list = not Chinese), and a contiguous run
///   of non-Chinese characters still contributes only its leading character, lowercased.
/// </remarks>
public sealed class ToolGoodPinyinProvider : IPinyinProvider
{
    public string GetFullPinyin(string text) =>
        WordsHelper.GetPinyin(text ?? "", "", false).ToLowerInvariant();

    /// <summary>Initials are DERIVED from <see cref="GetMap"/> rather than walked separately, so
    /// the string the search index matches against and the character positions the UI highlights
    /// can never disagree about where a syllable started.</summary>
    public string GetInitials(string text) => GetMap(text).Initials;

    public PinyinMap GetMap(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return PinyinMap.Empty;
        }

        var contextual = WordsHelper.GetPinyinList(text, false).ToList();
        var isAligned = contextual.Count == text.Length;

        var syllablesOut = new List<string>();
        var sourceOut = new List<int>();
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            var syllables = WordsHelper.GetAllPinyin(c, false);
            if (syllables.Count > 0)
            {
                var syllable = isAligned ? contextual[i] : syllables[0];
                syllablesOut.Add(syllable.ToLowerInvariant());
                sourceOut.Add(i);
                i++;
            }
            else
            {
                // A contiguous non-Chinese run contributes only its leading character — the
                // long-standing GetInitials contract ("Hi" -> "h"). The run's remaining characters
                // are skipped, so the next entry's source index is where the run ends, which is
                // what lets PinyinMap.SourceSpan cover the whole run.
                syllablesOut.Add(char.ToLowerInvariant(c).ToString());
                sourceOut.Add(i);
                i++;
                while (i < text.Length && WordsHelper.GetAllPinyin(text[i], false).Count == 0)
                {
                    i++;
                }
            }
        }

        return new PinyinMap(syllablesOut, sourceOut);
    }
}
