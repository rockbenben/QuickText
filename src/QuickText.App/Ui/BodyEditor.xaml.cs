using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QuickText.Core.Localization;
using QuickText.Core.Snippets;

namespace QuickText.App.Ui;

/// <summary>
/// The snippet body editor: a plain TextBox (so IME composition, the undo stack and Arabic
/// bidi layout stay WPF's job) with a highlight layer behind it and a status line under it.
/// Used both inline in the Manager and full-size in <see cref="BodyEditorWindow"/> — the two
/// share this one implementation.
/// </summary>
public partial class BodyEditor : UserControl, IBodyEditorSurface
{
    private IReadOnlyList<TokenSpan> _spans = Array.Empty<TokenSpan>();

    public BodyEditor()
    {
        InitializeComponent();
        Layer.Attach(Editor);
        Gutter.Attach(Editor);

        Editor.TextChanged += (_, _) => { Rescan(); EditorTextChanged?.Invoke(this, EventArgs.Empty); };
        Editor.SelectionChanged += (_, _) => UpdateStatus();
        Editor.SizeChanged += (_, _) => Redraw();
        Editor.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler((_, _) => Redraw()));
        Editor.PreviewKeyDown += OnEditorKeyDown;

        // WPF never raises ToolTipOpening on an element whose ToolTip is null — seed a
        // non-null placeholder so the event fires, then decide per-hover in the handler
        // whether to fill it in or cancel with e.Handled = true.
        Editor.ToolTip = string.Empty;
        Editor.ToolTipOpening += OnEditorToolTipOpening;

        // Date previews and invalid-reason tooltips are built with the culture at scan time — a
        // language change while the Manager is open would otherwise leave them stale until the
        // next keystroke. Weakly subscribed: LocalizationService.Instance is a process-wide
        // singleton, and a plain += here would keep every BodyEditor (and its host window) alive
        // forever. PropertyChangedEventManager fires for any property change when propertyName is
        // "" — covers the "Item[]" change SetCulture raises.
        PropertyChangedEventManager.AddHandler(LocalizationService.Instance, OnLocalizationChanged, string.Empty);
        Unloaded += (_, _) =>
            PropertyChangedEventManager.RemoveHandler(LocalizationService.Instance, OnLocalizationChanged, string.Empty);
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e) => Rescan();

    // ---------- public surface ----------

    /// <summary>The body text. Assigning a different value DOES raise <see cref="EditorTextChanged"/>
    /// (via <c>Editor.TextChanged</c> → <see cref="Rescan"/>) — that's intended. Only a same-value
    /// assignment is a no-op.</summary>
    public string Text
    {
        get => Editor.Text;
        set { if (!string.Equals(Editor.Text, value, StringComparison.Ordinal)) Editor.Text = value ?? ""; }
    }

    public event EventHandler? EditorTextChanged;

    private bool _wrap = true;
    public bool WrapText
    {
        get => _wrap;
        set
        {
            _wrap = value;
            Editor.TextWrapping = value ? TextWrapping.Wrap : TextWrapping.NoWrap;
            Editor.HorizontalScrollBarVisibility = value ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
            Redraw();
        }
    }

    private bool _lineNumbers;
    public bool ShowLineNumbers
    {
        get => _lineNumbers;
        set
        {
            _lineNumbers = value;
            GutterColumn.Width = new GridLength(value ? 34 : 0);
            Redraw();
        }
    }

    /// <summary>Mirrors the snippet's per-item opt-in. False = the body is literal text, so
    /// NOTHING is highlighted (a code snippet full of braces must not light up).</summary>
    private bool _useVariables;
    public bool UseVariables
    {
        get => _useVariables;
        set { _useVariables = value; Rescan(); }
    }

    public bool ShowStatusBar
    {
        get => Status.Visibility == Visibility.Visible;
        set => Status.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Resolves a <c>{片段:名称}</c> target so a dead reference can be flagged.</summary>
    public Func<string, bool>? SnippetExists { get; set; }

    public int CaretIndex
    {
        get => Editor.CaretIndex;
        set => Editor.CaretIndex = Math.Clamp(value, 0, Editor.Text.Length);
    }

    /// <summary>Passthrough so a host (the enlarged <see cref="BodyEditorWindow"/>) can carry the
    /// inline box's selection across, not just its caret.</summary>
    public int SelectionStart
    {
        get => Editor.SelectionStart;
        set => Editor.SelectionStart = Math.Clamp(value, 0, Editor.Text.Length);
    }

    public int SelectionLength
    {
        get => Editor.SelectionLength;
        set => Editor.SelectionLength = Math.Clamp(value, 0, Editor.Text.Length - Editor.SelectionStart);
    }

    public void FocusEditor() => Editor.Focus();

    // ---------- code-editing affordances ----------

    private void OnEditorKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        var mods = System.Windows.Input.Keyboard.Modifiers;

        // Enter: carry the current line's leading whitespace onto the new line. Writing JSON/YAML
        // or code in a snippet otherwise means re-typing the indent by hand every line.
        if (e.Key == System.Windows.Input.Key.Return &&
            mods == System.Windows.Input.ModifierKeys.None)
        {
            var text = Editor.Text;
            int caret = Editor.SelectionStart;
            int lineStart = caret;
            while (lineStart > 0 && text[lineStart - 1] != '\n') lineStart--;
            int ws = lineStart;
            while (ws < text.Length && ws < caret && (text[ws] == ' ' || text[ws] == '\t')) ws++;
            var indent = text.Substring(lineStart, ws - lineStart);
            if (indent.Length == 0) return;   // nothing to carry: let WPF handle it normally

            Editor.SelectedText = "\r\n" + indent;
            Editor.CaretIndex = Editor.SelectionStart + Editor.SelectionLength;
            Editor.SelectionLength = 0;
            e.Handled = true;
            return;
        }

        // Tab / Shift+Tab over a multi-line selection: indent or outdent the whole block.
        // Without this, AcceptsTab="True" REPLACES the selection with one tab — a multi-line
        // selection is silently destroyed.
        if (e.Key == System.Windows.Input.Key.Tab &&
            (mods == System.Windows.Input.ModifierKeys.None ||
             mods == System.Windows.Input.ModifierKeys.Shift) &&
            Editor.SelectionLength > 0 &&
            Editor.SelectedText.Contains('\n'))
        {
            ShiftSelectedLines(outdent: mods == System.Windows.Input.ModifierKeys.Shift);
            e.Handled = true;
        }
    }

    /// <summary>Indent (or outdent) every logical line the selection touches, as ONE undo unit,
    /// keeping the whole block selected afterwards so the user can press Tab again.</summary>
    private void ShiftSelectedLines(bool outdent)
    {
        var text = Editor.Text;
        int selStart = Editor.SelectionStart;
        int selEnd = selStart + Editor.SelectionLength;

        int blockStart = selStart;
        while (blockStart > 0 && text[blockStart - 1] != '\n') blockStart--;
        int blockEnd = selEnd;
        // A selection ending exactly at a line start (Home then Shift+Down) selects NO character
        // of that next line — scanning forward from here would indent a line the user never touched.
        if (selEnd > blockStart && text[selEnd - 1] == '\n') blockEnd = selEnd - 1;
        else while (blockEnd < text.Length && text[blockEnd] != '\n') blockEnd++;

        var lines = text.Substring(blockStart, blockEnd - blockStart).Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (outdent)
            {
                if (lines[i].StartsWith("\t", StringComparison.Ordinal)) lines[i] = lines[i].Substring(1);
                else
                {
                    int strip = 0;
                    while (strip < 4 && strip < lines[i].Length && lines[i][strip] == ' ') strip++;
                    lines[i] = lines[i].Substring(strip);
                }
            }
            else
            {
                // In CRLF text, Split('\n') leaves the trailing '\r' on what is really a blank
                // line — "\r" (length 1), not "" — so the length check alone never caught it and
                // blank lines gained a lone tab on indent.
                bool blank = lines[i].Length == 0 || lines[i] == "\r";
                if (!blank || i < lines.Length - 1) lines[i] = "\t" + lines[i];
            }
        }
        var replacement = string.Join("\n", lines);

        Editor.BeginChange();                       // one undo unit for the whole block
        Editor.SelectionStart = blockStart;
        Editor.SelectionLength = blockEnd - blockStart;
        Editor.SelectedText = replacement;
        Editor.EndChange();

        Editor.SelectionStart = blockStart;
        Editor.SelectionLength = replacement.Length;
    }

    // ---------- hover tooltip ----------

    /// <summary>Decides, on demand, whether the token under the mouse gets a tooltip and what
    /// it says: a date token's resolved <see cref="TokenSpan.Preview"/>, or an invalid token's
    /// localized <see cref="TokenSpan.Reason"/>. Everything else (plain variables, {光标},
    /// uuid/random/clipboard, or placeholders disabled entirely) shows nothing.</summary>
    private void OnEditorToolTipOpening(object sender, ToolTipEventArgs e)
    {
        if (!_useVariables || _spans.Count == 0) { e.Handled = true; return; }

        Point point = Mouse.GetPosition(Editor);
        int idx;
        try
        {
            idx = Editor.GetCharacterIndexFromPoint(point, false);
        }
        catch (ArgumentOutOfRangeException)
        {
            e.Handled = true;
            return;
        }
        if (idx < 0) { e.Handled = true; return; }

        TokenSpan? hit = null;
        foreach (var s in _spans)
        {
            if (idx >= s.Start && idx < s.Start + s.Length) { hit = s; break; }
        }
        if (hit is not { } span) { e.Handled = true; return; }

        string? hint = span.Preview ?? (span.Kind == TokenKind.Invalid ? BodyStatus.InvalidHint(span) : null);
        if (string.IsNullOrEmpty(hint)) { e.Handled = true; return; }

        Editor.ToolTip = hint;
    }

    // ---------- scan + paint ----------

    /// <summary>Re-scan the whole body and repaint. Cheap: one regex pass over a few KB.</summary>
    public void Rescan()
    {
        _spans = _useVariables
            ? PlaceholderScanner.Scan(Editor.Text, SnippetExists, null, LocalizationService.Instance.Culture)
            : Array.Empty<TokenSpan>();
        RecomputeTextStats();
        Redraw();
        UpdateStatus();
    }

    private void Redraw()
    {
        Layer.Update(_spans, _useVariables);
        Gutter.Refresh();
    }

    // ---------- status line ----------

    // Cached on TextChanged (via RecomputeTextStats), NOT on every SelectionChanged — the status
    // line used to redo four O(n) passes over the WHOLE body, plus an uncapped regex token count,
    // on every arrow-key press. -1 for _varsOffTokenCount means "over the scan cap, not counted".
    private int _lineCount = 1;
    private int _charCount;
    private int _varsOffTokenCount;

    /// <summary>Recomputes everything in the status line that depends on the body TEXT (not the
    /// caret) — called once per <see cref="Rescan"/>, i.e. once per keystroke, not once per
    /// selection change.</summary>
    private void RecomputeTextStats()
    {
        var text = Editor.Text;
        _lineCount = BodyStatus.LogicalLineCount(text);
        _charCount = BodyStatus.CharCount(text);
        _varsOffTokenCount = BodyStatus.CountTokensForVarsOff(text);
    }

    private void UpdateStatus()
    {
        if (Status.Visibility != Visibility.Visible) return;
        Status.Text = BodyStatus.Compose(Editor.Text, Editor.CaretIndex, _lineCount, _charCount,
                                         _varsOffTokenCount, _useVariables, _spans);
    }
}
