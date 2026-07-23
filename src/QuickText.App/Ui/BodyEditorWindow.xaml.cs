using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using QuickText.App.Interop;
using QuickText.Core.Localization;
using QuickText.Core.Snippets;

namespace QuickText.App.Ui;

/// <summary>
/// The enlarged body editor. NOT a separate commit point — it's the Manager's inline body box
/// at full size. Hosts exactly one of two editor surfaces at a time (see
/// <see cref="IBodyEditorSurface"/>): plain text keeps the native <see cref="BodyEditor"/>
/// TextBox (full Arabic bidi support), while an explicitly chosen code format swaps in
/// <see cref="CodeEditor"/> (AvalonEdit, syntax highlighting). Closing with unsaved changes
/// prompts Save/Don't save/Cancel (<see cref="Committed"/> records the answer); disk writes still
/// happen only in the Manager, which owns the <see cref="QuickText.Core.Models.Snippet"/> this
/// window never sees.
/// </summary>
public partial class BodyEditorWindow : Window
{
    public string ResultText { get; private set; } = "";
    public int ResultCaret { get; private set; }
    public int ResultSelectionLength { get; private set; }
    public string ResultCodeFormat { get; private set; } = "";

    /// <summary>True when the user answered Save to the unsaved-changes prompt (or closed with
    /// nothing changed). False means discard — the Result* values are then the ORIGINALS, so the
    /// Manager can write them back unconditionally without a special case.</summary>
    public bool Committed { get; private set; } = true;

    private IBodyEditorSurface _surface = null!;
    private bool _loadingLanguages;      // suppress SelectionChanged while filling the ComboBox
    private bool _useVariables;
    private Func<string, bool>? _snippetExists;

    /// <summary>The <c>codeFormat</c> ctor argument, verbatim. An id this build doesn't recognise
    /// (hand-edited, or written by a newer version) must survive an open/close round trip
    /// untouched — CodeLanguages.ById() resolves it to the plain-text entry for RENDERING only,
    /// and that fallback must not overwrite what was actually stored. Only OnCodeFormatChanged
    /// (an explicit user action) is allowed to replace ResultCodeFormat after construction.</summary>
    private readonly string _incomingCodeFormat;

    /// <summary>Text as it was when this window opened — the other half of the dirty check.</summary>
    private readonly string _incomingText;

    public BodyEditorWindow() : this("", "", false, 0, 0, null) { }

    public BodyEditorWindow(string snippetName, string body, bool useVariables, int caret,
                            int selectionLength = 0, string? codeFormat = null)
    {
        InitializeComponent();
        WindowTheming.UseDarkChrome(this);
        WindowTheming.ApplyFlowDirection(this);

        Title = string.Format(LocalizationService.Instance["Manager.BodyEditorTitle"], snippetName);

        var settings = AppState.Current.Settings;
        _useVariables = useVariables;
        _snippetExists = name => AppState.Current.SnippetForNesting(name) != null;
        _incomingCodeFormat = codeFormat ?? "";
        _incomingText = body ?? "";
        ResultCodeFormat = _incomingCodeFormat;

        // Guess, but never write: only when the snippet has no format set do we ask the detector
        // for a high-confidence structural guess, and the result feeds ONLY the rendering choice
        // below (dropdown selection + which surface SwapSurface builds) — never ResultCodeFormat,
        // which was already pinned to _incomingCodeFormat above. A misdetected format must never
        // get saved over the user's actual (absence of a) choice; only OnCodeFormatChanged, an
        // explicit user pick, is allowed to change what gets persisted.
        var renderCodeFormat = _incomingCodeFormat.Length == 0
            ? CodeFormatDetector.Detect(body)
            : _incomingCodeFormat;

        _loadingLanguages = true;
        foreach (var lang in CodeLanguages.All)
            CodeFormatBox.Items.Add(lang.IsPlainText
                ? LocalizationService.Instance["Manager.CodeFormat.PlainText"]
                : lang.DisplayName);
        // Rendering only: an unrecognised incoming id, or a detected-but-not-stored id, falls back
        // to / selects its row here, without _loadingLanguages ever being false in between, so
        // OnCodeFormatChanged never treats this as a user edit.
        CodeFormatBox.SelectedIndex = IndexOfCodeFormat(CodeLanguages.ById(renderCodeFormat).Id);
        _loadingLanguages = false;

        // AFTER SwapSurface: assigning IsChecked synchronously raises Checked/Unchecked, and that
        // handler reaches into _surface — which does not exist yet at this point in the ctor.
        SwapSurface(CodeLanguages.ById(renderCodeFormat).Id, body ?? "", caret, selectionLength);
        WrapToggle.IsChecked = settings.EditorWrap;
        ResultText = _surface.Text;
        ResultCaret = caret;
        ResultSelectionLength = selectionLength;

        RestoreSavedBounds(settings);

        Loaded += (_, _) =>
        {
            // Selection is only meaningful for BodyEditor — SelectionStart (not CaretIndex)
            // carries the caret AND the selection in one move there. CodeEditor has no selection
            // concept in IBodyEditorSurface, so it only ever gets the caret (set in SwapSurface).
            if (_surface is BodyEditor be)
            {
                be.SelectionStart = Math.Clamp(caret, 0, be.Text.Length);
                be.SelectionLength = Math.Clamp(selectionLength, 0, be.Text.Length - be.SelectionStart);
            }
            _surface.FocusEditor();
        };
        Closing += OnClosing;
    }

    private static int IndexOfCodeFormat(string id)
    {
        for (int i = 0; i < CodeLanguages.All.Count; i++)
            if (CodeLanguages.All[i].Id == id) return i;
        return 0;
    }

    /// <summary>Build the surface the chosen format needs and hand the text over. Exactly one
    /// editor exists at a time — plain text keeps the native TextBox (and with it full Arabic
    /// bidi); a code format switches to AvalonEdit.
    ///
    /// A pathologically long line forces plain text even when a code format was explicitly
    /// chosen — AvalonEdit's word-wrap layout degrades super-linearly on one enormous line
    /// (measured: a 46 KB line took 14s to open, 96 KB took 59s), so routing it into CodeEditor
    /// would hang the window regardless of who chose the format. This is a RENDERING fallback
    /// only: <see cref="ResultCodeFormat"/> (the stored choice) is untouched — only the surface
    /// object built here changes, exactly like the detector's own guard in
    /// <see cref="CodeFormatDetector.MaxLineLengthForCode"/>, which shares this same threshold.</summary>
    private void SwapSurface(string languageId, string text, int caret, int selectionLength)
    {
        var settings = AppState.Current.Settings;
        bool longLineFallback = !CodeLanguages.ById(languageId).IsPlainText
            && CodeFormatDetector.LongestLineLength(text) > CodeFormatDetector.MaxLineLengthForCode;
        LongLineNotice.Visibility = longLineFallback ? Visibility.Visible : Visibility.Collapsed;

        if (CodeLanguages.ById(languageId).IsPlainText || longLineFallback)
        {
            var plain = new BodyEditor
            {
                ShowStatusBar = true,
                UseVariables = _useVariables,
                SnippetExists = _snippetExists,
                WrapText = settings.EditorWrap,
                ShowLineNumbers = true,          // always on in the enlarged window
            };
            plain.Text = text;
            plain.SelectionStart = Math.Clamp(caret, 0, text.Length);
            plain.SelectionLength = Math.Clamp(selectionLength, 0, text.Length - plain.SelectionStart);
            EditorHost.Content = plain;
            _surface = plain;
        }
        else
        {
            var code = new CodeEditor
            {
                UseVariables = _useVariables,
                SnippetExists = _snippetExists,
                WrapText = settings.EditorWrap,
            };
            code.Text = text;
            code.LanguageId = languageId;
            code.CaretIndex = Math.Clamp(caret, 0, text.Length);
            code.Rescan();
            EditorHost.Content = code;
            _surface = code;
        }
        _surface.FocusEditor();
    }

    private void OnCodeFormatChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingLanguages || _surface == null) return;
        int i = CodeFormatBox.SelectedIndex;
        if (i < 0 || i >= CodeLanguages.All.Count) return;
        var id = CodeLanguages.All[i].Id;
        if (id == ResultCodeFormat) return;

        // Carry the work across the swap — the text lives in the editor, not in the window.
        var text = _surface.Text;
        int caret = _surface.CaretIndex;
        ResultCodeFormat = id;
        // Reconstructing the surface resets the undo stack, scroll position and selection (only
        // the caret is carried across, deliberately, above) — a known, accepted consequence of
        // every format change, not a bug to chase.
        SwapSurface(id, text, caret, 0);
    }

    /// <summary>Always centers on the current monitor; only the SIZE is ever remembered (see the
    /// XML doc on <see cref="Core.Settings.AppSettings.BodyWinX"/> for why the position is not).
    /// Named distinctly from WPF's own <see cref="Window.RestoreBounds"/> property (same name would
    /// hide it and warn CS0108).</summary>
    private void RestoreSavedBounds(Core.Settings.AppSettings settings)
    {
        WindowStartupLocation = WindowStartupLocation.Manual;
        // Owner is assigned by the caller's object initializer AFTER this constructor returns
        // (`new BodyEditorWindow(...) { Owner = this }`), so it can't be read here yet. Defer to
        // SourceInitialized — it fires when Show/ShowDialog creates the Hwnd, well after the object
        // initializer has run, so Owner is reliably set by then. PlaceOnOwnerMonitor falls back to
        // the cursor's monitor when there is no owner (the parameterless ctor used by --smoke).
        SourceInitialized += (_, _) => PlaceOnOwnerMonitor(settings);
    }

    /// <summary>Default opening size when nothing is remembered, floored well above the XAML
    /// MinWidth/MinHeight (420x320 — that stays the RESIZE floor, so the window can still be
    /// dragged compact by hand) but capped to the work area so the floor can't overflow a small
    /// monitor (defensive: the author's smallest monitor is 1536x864 DIP, already bigger than
    /// this floor).</summary>
    private const double DefaultMinWidth = 900;
    private const double DefaultMinHeight = 640;

    /// <summary>Center on the owner's (the Manager's) monitor, sized from the remembered W/H when
    /// sane, else 80% of that monitor's work area floored at <see cref="DefaultMinWidth"/> x
    /// <see cref="DefaultMinHeight"/> — clamped to the work area either way, so a size remembered
    /// on a larger monitor (or the default floor, on a small one) can't overflow a smaller monitor
    /// the window now opens on (the author's three monitors are 2560 / 2048 / 1536 DIP wide).</summary>
    private void PlaceOnOwnerMonitor(Core.Settings.AppSettings settings)
    {
        if (App.InSmoke) return;   // --smoke parks windows off-screen and never shows them
        var area = OwnerWorkArea();

        bool hasSavedSize = settings.BodyWinW > 200 && settings.BodyWinH > 150;
        // Math.Clamp would throw if the floor (900/640) exceeds area.Width/Height on a very small
        // monitor, so the upper bound is applied with Math.Min instead — that's what lets it win
        // over the floor rather than raising, on the (currently unreached) monitors smaller than
        // the floor itself.
        double width = hasSavedSize
            ? settings.BodyWinW
            : Math.Min(Math.Max(area.Width * 0.8, DefaultMinWidth), area.Width);
        double height = hasSavedSize
            ? settings.BodyWinH
            : Math.Min(Math.Max(area.Height * 0.8, DefaultMinHeight), area.Height);
        width = Math.Min(Math.Max(MinWidth, width), area.Width);
        height = Math.Min(Math.Max(MinHeight, height), area.Height);

        Width = width;
        Height = height;
        Left = area.Left + Math.Max(0, (area.Width - width) / 2);
        Top = area.Top + Math.Max(0, (area.Height - height) / 2);
    }

    /// <summary>Work area (DIPs) of the monitor the owner (the Manager) is on; the cursor's monitor
    /// as a fallback when there is no owner.</summary>
    private Rect OwnerWorkArea()
    {
        if (Owner != null)
        {
            var hwnd = new WindowInteropHelper(Owner).Handle;
            if (hwnd != IntPtr.Zero)
                return WindowTheming.MonitorWorkAreaDip(
                    NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST));
        }
        return CursorWorkArea();
    }

    /// <summary>Work area (DIPs) of the monitor under the mouse cursor; the primary work area as a
    /// fallback. Mirrors WindowTheming's own cursor-monitor lookup (shares its DIP conversion via
    /// the internal <see cref="WindowTheming.MonitorWorkAreaDip"/>), duplicated here in miniature
    /// because that helper's own cursor lookup is private.</summary>
    private static Rect CursorWorkArea()
    {
        try
        {
            if (NativeMethods.GetCursorPos(out var pt))
                return WindowTheming.MonitorWorkAreaDip(
                    NativeMethods.MonitorFromPoint(pt, NativeMethods.MONITOR_DEFAULTTONEAREST));
        }
        catch { /* fall through to the primary work area */ }
        return SystemParameters.WorkArea;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        // Esc just closes: OnClosing runs the same unsaved-changes prompt as every other close
        // path (OK button, the window's own [X]), so Esc gets no special-cased behaviour here.
        if (e.Key == Key.Escape) { Close(); e.Handled = true; }
        else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control) { Close(); e.Handled = true; }
        else if (e.Key == Key.F11)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            e.Handled = true;
        }
    }

    private void OnDone(object sender, RoutedEventArgs e) => Close();

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Dirty against what this window OPENED with — not against disk. Text and the code format
        // are the only two things this window can change.
        bool dirty = !string.Equals(_surface.Text, _incomingText, StringComparison.Ordinal)
                     || !string.Equals(ResultCodeFormat, _incomingCodeFormat, StringComparison.Ordinal);
        if (dirty)
        {
            bool? choice = AppDialog.ConfirmSaveDiscard(this, Title, L("Manager.UnsavedConfirm"),
                L("Manager.Save"), L("Manager.DontSave"), L("Dialog.Cancel"));
            if (choice == null) { e.Cancel = true; return; }   // stay in the enlarged editor
            Committed = choice == true;
        }

        if (Committed)
        {
            ResultText = _surface.Text;
            ResultCaret = _surface.CaretIndex;
            ResultSelectionLength = _surface is BodyEditor be ? be.SelectionLength : 0;
            // ResultCodeFormat is already current — OnCodeFormatChanged keeps it in step.
        }
        else
        {
            // Discard: hand back exactly what we were given, so the Manager's write-back is a
            // no-op instead of needing its own "was it discarded?" branch.
            ResultText = _incomingText;
            ResultCaret = 0;
            ResultSelectionLength = 0;
            ResultCodeFormat = _incomingCodeFormat;
        }

        var state = AppState.Current;
        // NOT during --smoke. That check constructs this window at its XAML default size, parks it
        // off-screen and closes it; without this guard every smoke run (CI *and* any local one)
        // overwrote the user's remembered size with 900x640, so the next real "enlarge" opened
        // barely bigger than the Manager itself. A self-check must never write user settings.
        if (!App.InSmoke && WindowState == WindowState.Normal)
        {
            // Position is deliberately not saved — see the XML doc on AppSettings.BodyWinX. Only
            // the size is remembered; the window always re-centers on the current monitor.
            state.Settings.BodyWinW = Width;
            state.Settings.BodyWinH = Height;
            state.SettingsStore.Save(state.Settings);
        }
    }

    private static string L(string key) => LocalizationService.Instance[key];

    private void OnWrapToggled(object sender, RoutedEventArgs e)
    {
        bool wrap = WrapToggle.IsChecked == true;
        ApplyWrap(wrap);
        var state = AppState.Current;
        if (state.Settings.EditorWrap != wrap)
        {
            state.Settings.EditorWrap = wrap;
            state.SettingsStore.Save(state.Settings);
        }
    }

    /// <summary><see cref="IBodyEditorSurface"/> deliberately has no WrapText member — the two
    /// implementations have different wrap semantics (TextWrapping vs WordWrap) — so switch on
    /// the concrete type here instead of widening the interface.</summary>
    private void ApplyWrap(bool wrap)
    {
        if (_surface is BodyEditor b) b.WrapText = wrap;
        else if (_surface is CodeEditor c) c.WrapText = wrap;
    }
}
