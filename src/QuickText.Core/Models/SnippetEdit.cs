namespace QuickText.Core.Models;

/// <summary>
/// The editable fields of a snippet, as a value. Record equality IS the editor's dirty check —
/// keeping "did the user actually change anything" out of the UI layer and under test.
/// <para>Both constructors normalize, and they must stay in step: <see cref="Snippet.CodeFormat"/>
/// is null when no format was ever picked while the UI hands back "", and
/// <see cref="Snippet.OutputMode"/> can be null too. Without normalizing, such a snippet would
/// read as dirty the moment it is opened and prompt on every selection change.</para>
/// </summary>
public sealed record SnippetEdit(
    string Name, string Abbr, string Body, bool UseVariables, string OutputMode, string? CodeFormat)
{
    /// <summary>Snapshot a snippet's editable fields.</summary>
    public static SnippetEdit From(Snippet s) =>
        Of(s.Name, s.Abbr, s.Body, s.UseVariables, s.OutputMode, s.CodeFormat);

    /// <summary>Build from raw editor-control values. The ONE normalizing entry point —
    /// <see cref="From"/> routes through it so the two can never diverge.</summary>
    public static SnippetEdit Of(string? name, string? abbr, string? body, bool useVariables,
                                 string? outputMode, string? codeFormat) =>
        new(name ?? "", abbr ?? "", body ?? "", useVariables,
            outputMode ?? "", string.IsNullOrEmpty(codeFormat) ? null : codeFormat);

    /// <summary>Write these six fields onto a snippet. Deliberately does NOT touch Id, ImagePath
    /// or UpdatedAt — images are an immediate/structural operation, and the caller owns the
    /// timestamp so it only moves when something actually changed.</summary>
    public void ApplyTo(Snippet s)
    {
        s.Name = Name;
        s.Abbr = Abbr;
        s.Body = Body;
        s.UseVariables = UseVariables;
        s.OutputMode = OutputMode;
        s.CodeFormat = CodeFormat;   // already normalized to null when empty
    }
}
