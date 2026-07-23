namespace QuickText.Core.Snippets;

/// <summary>One selectable body format.</summary>
/// <param name="Id">Stable id persisted in <c>Snippet.CodeFormat</c>. Empty = plain text.</param>
/// <param name="DisplayName">Dropdown label. Proper nouns (JSON, SQL…) are shown as-is in every
/// UI language and are deliberately NOT in the resource files. Empty for plain text — the UI
/// substitutes the localized <c>Manager.CodeFormat.PlainText</c> for that one row.</param>
/// <param name="HighlightingName">Name the App layer resolves to a highlighting definition.
/// Core never loads it — this string is the whole seam between user data and the renderer.</param>
public sealed record CodeLanguage(string Id, string DisplayName, string HighlightingName)
{
    public bool IsPlainText => Id.Length == 0;
}

/// <summary>The body formats the enlarged editor offers. Pure data: no WPF, no AvalonEdit.</summary>
public static class CodeLanguages
{
    private static readonly CodeLanguage[] Entries =
    {
        new("",           "",                        ""),
        new("json",       "JSON",                    "Json"),
        new("yaml",       "YAML",                    "QuickText.Yaml"),
        new("xml",        "XML",                     "XML"),
        new("html",       "HTML",                    "HTML"),
        new("markdown",   "Markdown",                "MarkDown"),
        new("sql",        "SQL",                     "TSQL"),
        new("python",     "Python",                  "Python"),
        new("javascript", "JavaScript / TypeScript", "JavaScript"),
        new("csharp",     "C#",                      "C#"),
        new("java",       "Java",                    "Java"),
        new("powershell", "PowerShell",              "PowerShell"),
        new("shell",      "Shell",                   "QuickText.Shell"),
        new("ini",        "INI / Config",            "QuickText.Ini"),
    };

    // Array.AsReadOnly wraps Entries in a ReadOnlyCollection<T> — a real read-only VIEW, not just
    // a read-only static TYPE. Without this, `(CodeLanguage[])CodeLanguages.All` compiles and lets
    // a caller reassign an element, corrupting the process-wide registry. Computed once: the
    // wrapper is a thin view over Entries, not a copy, so there's no reason to rebuild it per call.
    private static readonly System.Collections.ObjectModel.ReadOnlyCollection<CodeLanguage> EntriesView =
        Array.AsReadOnly(Entries);

    /// <summary>Plain text first — it is the dropdown's default row.</summary>
    public static IReadOnlyList<CodeLanguage> All => EntriesView;

    public static CodeLanguage PlainText => Entries[0];

    /// <summary>Never null: an unknown id (hand-edited file, or one written by a newer version)
    /// degrades to plain text rather than failing to open the snippet.</summary>
    public static CodeLanguage ById(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return PlainText;
        foreach (var e in Entries)
            if (!e.IsPlainText && string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase)) return e;
        return PlainText;
    }
}
