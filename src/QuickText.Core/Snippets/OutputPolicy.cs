namespace QuickText.Core.Snippets;

public sealed record OutputPolicy(bool CopyOnly, bool AutoSend)
{
    public static OutputPolicy Resolve(string? mode, bool globalCopy, bool globalAutoSend,
        bool hasCursor, bool abbreviation = false)
    {
        bool copy = mode == "copy" || (string.IsNullOrEmpty(mode) && globalCopy);
        bool enter = mode == "paste-enter" || (string.IsNullOrEmpty(mode) && globalAutoSend && !abbreviation);
        return new(copy, !copy && enter && !hasCursor);
    }
}
