using System;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Highlighting;

namespace QuickText.App.Ui.Syntax;

/// <summary>
/// Repaints a highlighting definition for a dark background. AvalonEdit's bundled .xshd files
/// are authored for a WHITE editor (navy keywords, dark-red strings, near-black comments); on
/// this app's #232830 ink they range from muted to unreadable.
///
/// This used to work off an allow-list of ~26 colour names and leave anything it didn't
/// recognise at the bundled light-theme value — which is how 44 of 129 named colours across the
/// 13 shipped languages ended up below 3:1 contrast (pure black, navy, unmodified blue…). Instead
/// every named colour in the definition is classified by substring and repainted; nothing is
/// ever skipped. Unrecognised names fall back to <see cref="Default"/> — the editor's normal
/// foreground, unstyled but never invisible. <see cref="HighlightingCatalog.UnreadableColors"/>
/// is the machine-checked guarantee that the palette below actually clears the contrast bar.
/// </summary>
internal static class SyntaxTheme
{
    // Tuned against Brush.InputBg (#232830) — every value here clears 3:1 contrast on that
    // background, verified by HighlightingCatalog.UnreadableColors() — and drawn from the app's
    // own palette so code reads as part of QuickText rather than as a transplanted IDE: accent
    // teal for keywords/tags, brand amber for strings, the same hues the placeholder capsules use.
    /// <summary>One bucket per semantic role. Only the values differ between themes — the
    /// classification in <see cref="PickFor"/> is about what a token MEANS, which no palette changes.</summary>
    private sealed record Palette(string Comment, string Str, string Number, string TypeName,
        string Method, string Tag, string Attribute, string Meta, string Punctuation,
        string Keyword, string Default);

    private static readonly Palette DarkPalette = new(
        Comment: "#7A828D",      // recessive, still readable (Brush.TextFaint-ish)
        Str: "#F2B457",          // Brush.Amber
        Number: "#A98CE8",
        TypeName: "#7FD9C4",
        Method: "#8FD0F0",
        Tag: "#3DC2A0",          // Brush.Accent
        Attribute: "#8FD0F0",
        Meta: "#C4A6F0",         // preprocessor/entity/annotation
        Punctuation: "#99A1AC",  // Brush.TextMuted — present but quiet
        Keyword: "#3DC2A0",      // Brush.Accent
        Default: "#D7DCE3");     // editor's normal foreground: unstyled, never invisible

    /// <summary>Same hue families, re-tuned for the light theme's white editor: the dark values run
    /// 1.1–1.6:1 on white, which is not a styling problem but an unreadable one. Every value below
    /// measures ≥4.5:1 on #FFFFFF — above this file's own 3:1 bar, since code is small text.</summary>
    private static readonly Palette LightPalette = new(
        Comment: "#67707C",
        Str: "#8A5A00",          // the light Brush.Amber
        Number: "#6D3FC4",
        TypeName: "#0F766E",
        Method: "#1D5FA8",
        Tag: "#0E7A61",          // the light Brush.Accent
        Attribute: "#1D5FA8",
        Meta: "#7A4BC9",
        Punctuation: "#5A626D",
        Keyword: "#0E7A61",
        Default: "#1F2630");

    /// <summary>Recolour in place. AvalonEdit hands out one shared definition instance per name,
    /// so this must run exactly once per definition — <see cref="HighlightingCatalog"/> owns that.
    /// Walks every named colour the definition declares — not a fixed list — so a language whose
    /// colour names nobody enumerated still gets themed instead of silently skipped.</summary>
    public static void ApplyDark(IHighlightingDefinition def) => Apply(def, light: false);

    /// <summary>Re-themable in place: AvalonEdit hands out one shared definition instance per name,
    /// so switching the app theme re-runs this over the same objects and every open editor repaints.</summary>
    public static void Apply(IHighlightingDefinition def, bool light)
    {
        var palette = light ? LightPalette : DarkPalette;
        foreach (var color in def.NamedHighlightingColors)
        {
            color.Foreground = new SimpleHighlightingBrush(Parse(PickFor(color.Name, palette)));
            // Bundled definitions sometimes set a light background on a colour (e.g. a
            // selection-ish highlight) that would punch a pale box into the dark editor.
            color.Background = null;
        }
    }

    /// <summary>Classify a colour NAME (not its original value) into a palette bucket. Every
    /// colour lands somewhere; anything unrecognised falls back to <see cref="Default"/> rather
    /// than being left at whatever the bundled light-theme definition originally set. Order
    /// matters — more specific buckets sit above the broad "keyword" catch-all, since e.g.
    /// NumberLiteral must hit "number" before "literal" reaches the keyword rule, and
    /// AttributeValue must hit "attribute" before "value" reaches it.</summary>
    private static string PickFor(string colorName, Palette p)
    {
        string n = colorName.ToLowerInvariant();
        if (Has(n, "comment", "doc", "quote")) return p.Comment;
        if (Has(n, "string", "char", "regex", "verbatim", "heredoc", "interpolation", "emphasis")) return p.Str;
        if (Has(n, "number", "digit")) return p.Number;
        if (Has(n, "type", "class", "struct", "interface", "enum", "void")) return p.TypeName;
        if (Has(n, "method", "function", "call", "code", "link")) return p.Method;
        if (Has(n, "tag", "element")) return p.Tag;
        if (Has(n, "attribute", "attrib", "property")) return p.Attribute;
        if (Has(n, "preprocessor", "directive", "entit", "annotation", "section")) return p.Meta;
        if (Has(n, "punctuation")) return p.Punctuation;
        if (Has(n, "keyword", "visibility", "modifier", "access", "statement", "jump",
                    "selection", "iteration", "goto", "context", "namespace", "package",
                    "literal", "operator", "command", "intrinsic", "global", "declaration",
                    "exception", "reference", "value", "this", "base", "heading")) return p.Keyword;
        return p.Default;
    }

    private static bool Has(string name, params string[] substrings)
    {
        foreach (var s in substrings)
            if (name.Contains(s, StringComparison.Ordinal)) return true;
        return false;
    }

    private static Color Parse(string hex) => (Color)ColorConverter.ConvertFromString(hex)!;
}
