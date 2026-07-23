using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Media;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using QuickText.Core.Snippets;

namespace QuickText.App.Ui.Syntax;

/// <summary>
/// The one place a <see cref="CodeLanguage.HighlightingName"/> becomes a real definition:
/// registers the three definitions AvalonEdit doesn't ship (YAML/INI/Shell) from embedded
/// resources, and hands out dark-themed instances.
/// </summary>
internal static class HighlightingCatalog
{
    private static readonly object Gate = new();
    private static bool _registered;
    private static readonly HashSet<string> Themed = new(StringComparer.Ordinal);

    /// <summary>
    /// Dark-theme EVERY registered definition, not just the one being asked for.
    /// <para>Definitions embed each other — HTML hosts JavaScript and CSS, C# hosts XmlDoc — and an
    /// embedded definition renders inside our editor with its OWN colours. Theming lazily per
    /// requested name meant opening an HTML snippet before ever opening a JavaScript one drew the
    /// &lt;script&gt; block in AvalonEdit's raw light-theme blue, and CSS (which we don't offer in
    /// the dropdown at all) would never have been themed on any code path.</para>
    /// <para>~20 definitions, once per process, behind the same lock as registration.</para>
    /// </summary>
    private static void ThemeEverything()
    {
        foreach (var def in HighlightingManager.Instance.HighlightingDefinitions)
            if (Themed.Add(def.Name)) SyntaxTheme.ApplyDark(def);
    }

    // Resource name suffix -> definition name, matching the <SyntaxDefinition name="…"> inside.
    private static readonly (string Resource, string Name, string[] Ext)[] Bundled =
    {
        ("Ui.Syntax.yaml.xshd",  "QuickText.Yaml",  new[] { ".yaml", ".yml" }),
        ("Ui.Syntax.ini.xshd",   "QuickText.Ini",   new[] { ".ini", ".conf", ".cfg", ".properties" }),
        ("Ui.Syntax.shell.xshd", "QuickText.Shell", new[] { ".sh", ".bash", ".zsh" }),
    };

    /// <summary>Dark-themed definition, or null when the name resolves to nothing.</summary>
    public static IHighlightingDefinition? Get(string? highlightingName)
    {
        if (string.IsNullOrEmpty(highlightingName)) return null;
        lock (Gate)
        {
            EnsureRegistered();
            ThemeEverything();
            return HighlightingManager.Instance.GetDefinition(highlightingName);
        }
    }

    /// <summary>Highlighting names referenced by <see cref="CodeLanguages"/> that do NOT resolve.
    /// Empty is the only healthy result; the --smoke check fails the build on anything else.
    /// This is the guard against an embedded .xshd silently not being embedded — a failure the
    /// compiler cannot see and the app only reveals when a user picks that one format.</summary>
    public static IReadOnlyList<string> MissingDefinitions() =>
        CodeLanguages.All
            .Where(l => !l.IsPlainText)
            .Select(l => l.HighlightingName)
            .Where(n => Get(n) == null)
            .ToList();

    /// <summary>Named colours across all shipped languages whose foreground is too faint to read on
    /// the editor background, as "<language>/<colour> #RRGGBB". Empty is the only healthy result.
    /// Eyeballing a few languages is exactly how a 44-colour regression slipped through once.</summary>
    public static IReadOnlyList<string> UnreadableColors(double minContrast = 3.0)
    {
        var background = (Color)ColorConverter.ConvertFromString("#232830")!;
        var bad = new List<string>();
        // Sweep EVERY registered definition, not just the 13 we offer in the dropdown. Several of
        // ours embed others — HTML hosts JavaScript and CSS, C# hosts XmlDoc — and an embedded
        // definition's colours show up inside our editor even though the user never picks it by
        // name. Auditing only the 13 reported "all clear" while HTML still rendered its <script>
        // block in raw light-theme blue.
        lock (Gate)
        {
            EnsureRegistered();
            ThemeEverything();
        }
        foreach (var def in HighlightingManager.Instance.HighlightingDefinitions)
        {
            foreach (var c in def.NamedHighlightingColors)
            {
                var color = c.Foreground?.GetColor(null);
                if (color == null) continue;   // no foreground set at all -> inherits editor default, fine
                double contrast = Contrast(color.Value, background);
                if (contrast < minContrast)
                    bad.Add($"{def.Name}/{c.Name} #{color.Value.R:X2}{color.Value.G:X2}{color.Value.B:X2}");
            }
        }
        return bad;
    }

    private static double Relative(Color c)
    {
        static double Ch(double v) { v /= 255.0; return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4); }
        return 0.2126 * Ch(c.R) + 0.7152 * Ch(c.G) + 0.0722 * Ch(c.B);
    }

    private static double Contrast(Color a, Color b)
    {
        double la = Relative(a), lb = Relative(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static void EnsureRegistered()
    {
        if (_registered) return;
        var asm = Assembly.GetExecutingAssembly();
        foreach (var (resource, name, ext) in Bundled)
        {
            try
            {
                // Resource ids are "<RootNamespace>.<path with dots>"; find by suffix so a
                // root-namespace change can't silently break the lookup.
                var id = asm.GetManifestResourceNames().FirstOrDefault(r => r.EndsWith(resource, StringComparison.Ordinal));
                if (id == null) continue;   // MissingDefinitions() reports it
                using var stream = asm.GetManifestResourceStream(id);
                if (stream == null) continue;
                using var reader = new XmlTextReader(stream);
                var def = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                HighlightingManager.Instance.RegisterHighlighting(name, ext, def);
            }
            catch
            {
                // A malformed .xshd must degrade to "missing" (MissingDefinitions() reports it,
                // --smoke catches it) rather than throw and strand every OTHER bundled definition
                // unregistered — Get() is documented as returning null, not throwing.
            }
        }
        // Set AFTER the loop, not before: latching early on the very first call would mean a
        // throw partway through leaves the remaining definitions permanently unregistered, since
        // this method would never run its loop body again.
        _registered = true;
    }
}
