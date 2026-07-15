namespace QuickText.Core.Models;

public sealed class Snippet
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Abbr { get; set; } = "";
    public string Body { get; set; } = "";
    // Opt-in per snippet: only when set are {变量}/{剪贴板}/{光标}/{日期} tokens processed on
    // send/expand. Off by default so code/script bodies full of literal {...} go out verbatim.
    public bool UseVariables { get; set; }
    // Per-snippet output override: "" follow global settings, "paste" never auto-Enter,
    // "paste-enter" always auto-Enter, "copy" copy to clipboard only (no paste).
    public string OutputMode { get; set; } = "";
    public string ImagePath { get; set; } = "";   // relative "images/<id>.png" — image snippet when set
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public bool IsImage => !string.IsNullOrEmpty(ImagePath);

    /// <summary>A "new" row the user never filled in: nothing to insert (no body, image, or
    /// abbreviation) and only a blank or untouched-default name — so the Manager discards it on
    /// close instead of persisting an empty row. <paramref name="defaultName"/> is the localized
    /// "New snippet" label a fresh row is seeded with.</summary>
    public bool IsBlankDraft(string defaultName) =>
        string.IsNullOrWhiteSpace(Body) && !IsImage && string.IsNullOrWhiteSpace(Abbr)
        && (string.IsNullOrWhiteSpace(Name) || Name == defaultName);
}

public sealed class Category
{
    public string Name { get; set; } = "";
    public string Color { get; set; } = "";   // optional #RRGGBB accent
    public List<Snippet> Snippets { get; set; } = new();
}

/// <summary>A soft-deleted snippet parked in trash.json, restorable for 30 days.</summary>
public sealed class TrashEntry
{
    public Snippet Snippet { get; set; } = new();
    public string Category { get; set; } = "";   // original category name, for restore
    public DateTimeOffset DeletedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class CategoryRef
{
    public string Name { get; set; } = "";
    public string File { get; set; } = "";
    public string Color { get; set; } = "";
}

public sealed class LibraryIndex
{
    public int SchemaVersion { get; set; } = 1;
    public List<CategoryRef> Categories { get; set; } = new();
}
