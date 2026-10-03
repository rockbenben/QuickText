using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Rendering;
using QuickText.App.Ui.Syntax;
using QuickText.Core.Localization;
using QuickText.Core.Snippets;

namespace QuickText.App.Ui;

/// <summary>
/// The enlarged editor's syntax-highlighting surface: an AvalonEdit TextEditor themed for this
/// app's dark palette. Used ONLY when the snippet picked a code format — plain text stays on
/// <see cref="BodyEditor"/>'s native TextBox, which is what preserves Arabic bidi layout.
/// Line numbers are always on here: this window exists to read code.
/// </summary>
public partial class CodeEditor : UserControl, IBodyEditorSurface
{
    private IReadOnlyList<TokenSpan> _spans = Array.Empty<TokenSpan>();
    private bool _useVariables;
    private int _lineCount = 1, _charCount, _varsOffTokenCount;
    private string _languageId = "";
    // Whether the PREVIOUS Rescan actually painted placeholder decoration — so a Rescan that just
    // turned _useVariables off knows it still needs one more redraw to clear it (see Rescan()).
    private bool _hadDecoration;

    public CodeEditor()
    {
        InitializeComponent();

        Editor.Options.EnableHyperlinks = false;
        Editor.Options.EnableEmailHyperlinks = false;
        Editor.Options.HighlightCurrentLine = true;
        Editor.TextArea.TextView.BackgroundRenderers.Add(
            new CurrentLineBarRenderer(() => Editor.TextArea.Caret.Line));
        Editor.Options.ConvertTabsToSpaces = false;
        Editor.TextArea.TextView.CurrentLineBorder = FrozenPen(Frozen("#00000000"), 0);
        Editor.TextArea.SelectionBorder = null;
        Editor.TextArea.SelectionForeground = null;   // keep syntax colours inside a selection
        // Selection and caret are the accent; the current-line wash is a tint of the text colour.
        // All three must follow a theme switch — a white 8% wash is invisible on a light background.
        ApplyEditorTheme();
        ThemeService.Changed += OnThemeChanged;
        Unloaded += (_, _) => ThemeService.Changed -= OnThemeChanged;   // static event: unsubscribe or leak

        // Reads _spans through a closure so the transformer never holds a stale copy — Rescan()
        // replaces the list and calls Redraw(), and the next ColorizeLine picks up the new one.
        Editor.TextArea.TextView.LineTransformers.Add(
            new PlaceholderColorizer(() => _spans, () => _useVariables));

        Editor.TextChanged += (_, _) => Rescan();
        // The bar renderer draws in the background layer, which AvalonEdit does NOT repaint on a
        // bare caret move (only the caret layer). Redraw() is cheap at snippet sizes.
        Editor.TextArea.Caret.PositionChanged += (_, _) => { Editor.TextArea.TextView.Redraw(); UpdateStatus(); };

        // Mirror BodyEditor's hover tooltip (date preview / invalid-token reason) on this surface —
        // PlaceholderColorizer already draws the red underline here, so without this the code editor
        // shows an error marker with no way to learn what it means.
        Editor.TextArea.TextView.MouseHover += OnEditorMouseHover;
        Editor.TextArea.TextView.MouseHoverStopped += (_, _) => Editor.ToolTip = null;

        // Date previews and invalid-token reasons are built with the UI culture; a language
        // change must refresh them. Weakly subscribed: LocalizationService.Instance is a
        // process-wide singleton, and a plain += here would keep every CodeEditor (and its host
        // window) alive forever. PropertyChangedEventManager fires for any property change when
        // propertyName is "" — covers the "Item[]" change SetCulture raises.
        PropertyChangedEventManager.AddHandler(LocalizationService.Instance, OnLocalizationChanged, string.Empty);
        Unloaded += (_, _) =>
            PropertyChangedEventManager.RemoveHandler(LocalizationService.Instance, OnLocalizationChanged, string.Empty);
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e) => Rescan();

    /// <summary>Chrome colours, then the token colours: re-assigning LanguageId re-resolves the
    /// definition through <see cref="Syntax.HighlightingCatalog"/>, which repaints the shared
    /// definitions for the new theme — without it an already-open editor keeps the old palette.</summary>
    private void OnThemeChanged()
    {
        ApplyEditorTheme();
        LanguageId = _languageId;
    }

    /// <summary>Repaint the three editor chrome colours from the current palette. Derived rather
    /// than tabulated: selection and caret are the accent by definition, so they stay right for any
    /// future palette without another table to keep in sync.</summary>
    private void ApplyEditorTheme()
    {
        var accent = (System.Windows.Application.Current?.TryFindResource("Brush.Accent") as SolidColorBrush)?.Color
                     ?? Color.FromRgb(0x3D, 0xC2, 0xA0);
        Editor.TextArea.TextView.CurrentLineBackground = new SolidColorBrush(ThemeService.IsLight
            ? Color.FromArgb(0x12, 0x1B, 0x1E, 0x24)     // a wash of the ink, not of white
            : Color.FromArgb(0x0A, 0xFF, 0xFF, 0xFF));   // 4%: the 8% strip read as a scrollbar
        Editor.TextArea.SelectionBrush = new SolidColorBrush(Color.FromArgb(0x55, accent.R, accent.G, accent.B));
        Editor.TextArea.Caret.CaretBrush = new SolidColorBrush(accent);
    }

    /// <summary>The caret line's real anchor: a 2px accent bar at the line's left edge. A borderless
    /// tint strip across a dark surface is what made the old 8% wash look like a stray horizontal
    /// scrollbar — the bar says "this line" in a shape no scrollbar wears.</summary>
    private sealed class CurrentLineBarRenderer(Func<int> caretLine) : IBackgroundRenderer
    {
        public KnownLayer Layer => KnownLayer.Background;

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            var visualLine = textView.GetVisualLine(caretLine());
            if (visualLine == null) return;
            if (System.Windows.Application.Current?.TryFindResource("Brush.Accent") is not Brush brush) return;
            double y = visualLine.GetTextLineVisualYPosition(visualLine.GetTextLine(0), VisualYPosition.LineTop);
            drawingContext.DrawRectangle(brush, null, new System.Windows.Rect(0, y, 2, visualLine.Height));
        }
    }

    private static SolidColorBrush Frozen(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);
        b.Freeze();
        return b;
    }

    private static Pen FrozenPen(Brush brush, double thickness)
    {
        var p = new Pen(brush, thickness);
        p.Freeze();
        return p;
    }

    // ---------- IBodyEditorSurface ----------

    public string Text
    {
        get => Editor.Text;
        set { if (!string.Equals(Editor.Text, value, StringComparison.Ordinal)) Editor.Text = value ?? ""; }
    }

    public int CaretIndex
    {
        get => Editor.CaretOffset;
        set => Editor.CaretOffset = Math.Clamp(value, 0, Editor.Document.TextLength);
    }

    public bool UseVariables
    {
        get => _useVariables;
        set { _useVariables = value; Rescan(); }
    }

    public Func<string, bool>? SnippetExists { get; set; }

    public void FocusEditor() => Editor.TextArea.Focus();

    // ---------- code-editor specifics ----------

    /// <summary>A <see cref="CodeLanguages"/> id. Unknown ids fall back to no highlighting
    /// rather than throwing — a hand-edited library must still open.</summary>
    public string LanguageId
    {
        get => _languageId;
        set
        {
            _languageId = value ?? "";
            var lang = CodeLanguages.ById(_languageId);
            Editor.SyntaxHighlighting = lang.IsPlainText ? null : HighlightingCatalog.Get(lang.HighlightingName);
        }
    }

    public bool WrapText
    {
        get => Editor.WordWrap;
        set => Editor.WordWrap = value;
    }

    public void Rescan()
    {
        var text = Editor.Text;
        _spans = _useVariables
            ? PlaceholderScanner.Scan(text, SnippetExists, null, LocalizationService.Instance.Culture)
            : Array.Empty<TokenSpan>();
        _lineCount = BodyStatus.LogicalLineCount(text);
        _charCount = BodyStatus.CharCount(text);
        _varsOffTokenCount = BodyStatus.CountTokensForVarsOff(text);
        // AvalonEdit already invalidates the changed line on its own for a plain text edit — a
        // full TextView redraw is only needed when placeholder decoration could actually differ
        // from what's on screen: while variables are on (the scanned spans may have moved), or on
        // the one-time transition where they just turned off and stale decoration from a moment
        // ago must be cleared. Code snippets are exactly the population that leaves variables off,
        // so skipping this on every keystroke there is the main win.
        if (_useVariables || _hadDecoration) Editor.TextArea.TextView.Redraw();
        _hadDecoration = _useVariables;
        UpdateStatus();
    }

    private void UpdateStatus() =>
        Status.Text = BodyStatus.Compose(Editor.Text, Editor.CaretOffset, _lineCount, _charCount,
                                         _varsOffTokenCount, _useVariables, _spans);

    // ---------- hover tooltip ----------

    /// <summary>Same decision BodyEditor's tooltip makes, re-derived for AvalonEdit's own hover
    /// event and coordinate system: a date token's resolved <see cref="TokenSpan.Preview"/>, or an
    /// invalid token's localized reason via <see cref="BodyStatus.InvalidHint"/>. Everything else
    /// (plain variables, {光标}, uuid/random/clipboard, or placeholders disabled entirely) shows
    /// nothing, matching BodyEditor.</summary>
    private void OnEditorMouseHover(object? sender, MouseEventArgs e)
    {
        if (!_useVariables || _spans.Count == 0) return;

        var textView = Editor.TextArea.TextView;
        var pos = textView.GetPositionFloor(e.GetPosition(textView) + textView.ScrollOffset);
        if (pos == null) return;   // not over text

        int offset;
        try
        {
            offset = Editor.Document.GetOffset(pos.Value.Location);
        }
        catch (ArgumentOutOfRangeException)
        {
            // A hover can race a document change (e.g. an in-flight Rescan); just skip this hover.
            return;
        }

        TokenSpan? hit = null;
        foreach (var s in _spans)
        {
            if (offset >= s.Start && offset < s.Start + s.Length) { hit = s; break; }
        }
        if (hit is not { } span) return;

        string? hint = span.Preview ?? BodyStatus.InvalidHint(span);
        if (string.IsNullOrEmpty(hint)) return;

        Editor.ToolTip = hint;
        e.Handled = true;
    }
}
