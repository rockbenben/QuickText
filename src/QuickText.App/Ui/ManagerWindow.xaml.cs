using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using QuickText.App;
using QuickText.Core.Localization;
using QuickText.Core.Models;

namespace QuickText.App.Ui;

public partial class ManagerWindow : Window
{
    private List<Category> _cats = new();
    private Snippet? _editing;
    private Category? _editingCategory;
    private readonly HashSet<Category> _dirty = new();
    private readonly HashSet<string> _uncommittedSnippets = new();

    /// <summary>AppState.DataVersion this window loaded. A mismatch means an external/in-app
    /// reload replaced the library; writing this window's stale categories would lose that data.</summary>
    private int _loadedVersion;
    private bool _staleNoticeShown;
    private string _saveFailureKey = "Manager.SaveFailed";
    private bool _reloadPending;
    private bool _reloadQueued;

    /// <summary>The editable fields as they were when the current snippet was loaded. Dirty means
    /// "the controls differ from this", not "the editor was touched" — clicking into a snippet and
    /// straight back out must never prompt. Null when no snippet is loaded.</summary>
    private SnippetEdit? _baseline;
    private DateTimeOffset _baselineUpdatedAt;
    private readonly HashSet<string> _createdImages = new(StringComparer.OrdinalIgnoreCase);
    private Core.Persistence.Store _imageStore = AppState.Current.Store;

    /// <summary>Guards the selection-revert we do when the user cancels leaving a dirty editor:
    /// putting SelectedItem back raises SelectionChanged again, which would prompt a second time.</summary>
    private bool _revertingSelection;

    // Snippet.OutputMode values, in the ComboBox's item order.
    private static readonly string[] OutputModes = { "", "paste", "paste-enter", "copy" };

    public ManagerWindow()
    {
        InitializeComponent();
        WindowTheming.UseDarkChrome(this);
        WindowTheming.ApplyFlowDirection(this);
        WindowTheming.PlaceOnActiveMonitor(this);   // shared with Settings: on the user's monitor, capped, on-screen
        AbbrPrefixText.Text = AppState.Current.Settings.AbbrPrefix;   // the auto-added trigger prefix

        var loc = LocalizationService.Instance;
        foreach (var key in new[] { "Manager.Output.Default", "Manager.Output.Paste", "Manager.Output.PasteEnter", "Manager.Output.Copy" })
            OutputMode.Items.Add(loc[key]);
        OutputMode.SelectedIndex = 0;

        // Point at the Manager's own live _cats, NOT AppState's index — that index is only
        // rebuilt on Save/Close, while _cats is the authoritative in-memory model while this
        // window is open. Using the stale index made a nested reference to a snippet just created
        // (or renamed into place) here show a false "no such snippet" squiggle until Save. Match
        // AppState.SnippetForNesting's own rule: case-insensitive name, image snippets excluded.
        Body.SnippetExists = name => _cats.SelectMany(c => c.Snippets)
            .Any(x => !x.IsImage && string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
        WrapToggle.IsChecked = AppState.Current.Settings.EditorWrap;
        LineNumberToggle.IsChecked = AppState.Current.Settings.EditorLineNumbers;
        ImageToggle.IsChecked = AppState.Current.Settings.EditorImageExpanded;
        ApplyWrap();
        ApplyLineNumbers();
        ApplyImageSection();

        Closing += OnClosing;
        _filterDebounce.Tick += (_, _) => ApplyFilter();
        AppState.Current.DataReloaded += OnDataReloaded;
        Closed += (_, _) =>
        {
            _filterDebounce.Stop();
            AppState.Current.DataReloaded -= OnDataReloaded;
        };
        LoadFromState();
        SnippetName.TextChanged += (_, _) => UpdateSaveStatus();
        Abbr.TextChanged += (_, _) => UpdateSaveStatus();
        Body.EditorTextChanged += (_, _) => UpdateSaveStatus();
        UseVars.Checked += (_, _) => UpdateSaveStatus();
        UseVars.Unchecked += (_, _) => UpdateSaveStatus();
        OutputMode.SelectionChanged += (_, _) => UpdateSaveStatus();
        AddHandler(Button.ClickEvent, new RoutedEventHandler((_, _) => UpdateSaveStatus()), true);
        UpdateSaveStatus();
    }

    private bool IsStale => _loadedVersion != AppState.Current.DataVersion;

    private bool HasOpenOwnedWindow()
    {
        foreach (Window w in OwnedWindows)
            if (w.IsVisible) return true;
        return false;
    }

    private void OnDataReloaded()
    {
        if (App.InSmoke || !IsVisible || !IsStale) return;
        _reloadPending = true;
        if (!HasOpenOwnedWindow()) ApplyReloadToModel();
    }

    /// <summary>Reconcile with the latest AppState snapshot. Called only when no modal owned
    /// window is open, so a full body editor's target cannot be orphaned mid-save.</summary>
    private void ApplyReloadToModel()
    {
        if (!IsVisible || HasOpenOwnedWindow()) return;
        if (!IsStale) { _reloadPending = false; return; }
        _reloadPending = false;

        if (!EditorIsDirty() && _dirty.Count == 0)
        {
            var keep = SelectedSnippet?.Id;
            LoadFromState();
            if (!string.IsNullOrEmpty(keep)) SelectSnippet(keep);
            return;
        }

        // Never replace an editor with unsaved work behind the user's back; tell them once and
        // leave the write gate (EnsureFreshForWrite) armed.
        Dispatcher.BeginInvoke(new Action(ShowStaleNotice),
            System.Windows.Threading.DispatcherPriority.Background);
    }

    /// <summary>Queue reconciliation after the current modal-close call stack has finished; using
    /// Background priority is what lets the enlarged editor's Save/Discard result reach the old
    /// model before the model is replaced.</summary>
    private void QueuePendingReload()
    {
        if (_reloadQueued || !IsVisible) return;
        if (!IsStale) { _reloadPending = false; return; }
        _reloadPending = true;
        _reloadQueued = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _reloadQueued = false;
            if (_reloadPending) ApplyReloadToModel();
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    private void ShowStaleNotice()
    {
        if (!IsLoaded || !IsStale || _staleNoticeShown) return;
        _staleNoticeShown = true;
        AppDialog.Alert(this, L("App.Name"), L("Manager.StaleNotice"));
    }

    /// <summary>Called at every write point. A stale window may not write: offer a reload that
    /// discards its local snapshot, or abort the operation.</summary>
    private bool EnsureFreshForWrite()
    {
        if (AppState.Current.IsLibraryBusy) { AlertSaveFailed(); return false; }
        if (!IsStale) return true;
        bool reload = AppDialog.Confirm(this, L("App.Name"), L("Manager.StaleNotice"),
            L("Manager.Reload"), L("Dialog.Cancel"));
        if (reload) LoadFromState();
        return false;
    }

    private void ApplyWrap() => Body.WrapText = WrapToggle.IsChecked == true;

    private void OnWrapToggled(object sender, RoutedEventArgs e)
    {
        ApplyWrap();
        var state = AppState.Current;
        if (state.Settings.EditorWrap != (WrapToggle.IsChecked == true))
        {
            state.Settings.EditorWrap = WrapToggle.IsChecked == true;
            state.SettingsStore.Save(state.Settings);
        }
    }

    private void ApplyLineNumbers() => Body.ShowLineNumbers = LineNumberToggle.IsChecked == true;

    private void OnLineNumbersToggled(object sender, RoutedEventArgs e)
    {
        ApplyLineNumbers();
        var state = AppState.Current;
        if (state.Settings.EditorLineNumbers != (LineNumberToggle.IsChecked == true))
        {
            state.Settings.EditorLineNumbers = LineNumberToggle.IsChecked == true;
            state.SettingsStore.Save(state.Settings);
        }
    }

    /// <summary>Show/hide the image block. A snippet that HAS an image always shows it — the
    /// remembered collapse is a default for image-less snippets, not a way to hide real content.</summary>
    private void ApplyImageSection()
    {
        bool hasImage = _editing?.IsImage == true;
        bool open = ImageToggle.IsChecked == true || hasImage;
        ImageSection.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        ImagePeek.Visibility = (!open && hasImage) ? Visibility.Visible : Visibility.Collapsed;
        // On an image snippet the section is always forced open, so the checkbox has nothing to
        // do visually — but left clickable it still silently rewrote the persisted default
        // (EditorImageExpanded) for every OTHER snippet. Disable it while it would be a no-op.
        ImageToggle.IsEnabled = !hasImage;
    }

    private void OnImageSectionToggled(object sender, RoutedEventArgs e)
    {
        ApplyImageSection();
        var state = AppState.Current;
        if (App.InSmoke) return;   // --smoke/--shots fixtures must not write the user's settings
        if (state.Settings.EditorImageExpanded != (ImageToggle.IsChecked == true))
        {
            state.Settings.EditorImageExpanded = ImageToggle.IsChecked == true;
            state.SettingsStore.Save(state.Settings);
        }
    }

    /// <summary>Open the enlarged body editor, then take its text, caret and selection back.</summary>
    private void OnExpandBody(object sender, RoutedEventArgs e)
    {
        if (_editing is not { } target) return;
        var win = new BodyEditorWindow(SnippetName.Text, Body.Text, UseVars.IsChecked == true,
                                       Body.CaretIndex, Body.SelectionLength, target.CodeFormat)
        {
            Owner = this,
        };
        win.ShowDialog();

        // The enlarged editor's own wrap checkbox writes straight to AppSettings, so the disk
        // value can have moved out from under this window's checkbox/inline editor — re-sync now.
        WrapToggle.IsChecked = AppState.Current.Settings.EditorWrap;
        ApplyWrap();

        // The selection can move under a modal: the global summon hotkey still fires (it's
        // delivered to a message-only window, not blocked by WPF modality), so the SearchPanel can
        // re-target THIS open Manager at a different snippet. If that happened, writing the
        // enlarged editor's result into the inline Body box would paste this snippet's edited text
        // over the now-selected one's — land it on the snippet the window was opened for instead.
        if (!ReferenceEquals(_editing, target))
        {
            if (!win.Committed) { if (_reloadPending) QueuePendingReload(); return; }   // discarded
            bool changed = target.Body != win.ResultText
                           || (target.CodeFormat ?? "") != win.ResultCodeFormat;
            if (changed)
            {
                target.Body = win.ResultText;
                target.CodeFormat = win.ResultCodeFormat.Length == 0 ? null : win.ResultCodeFormat;
                target.UpdatedAt = DateTimeOffset.UtcNow;
                if (_cats.FirstOrDefault(c => c.Snippets.Contains(target)) is { } cat) _dirty.Add(cat);
            }
            if (_reloadPending) QueuePendingReload();
            return;
        }

        bool changedInWindow = Body.Text != win.ResultText || (target.CodeFormat ?? "") != win.ResultCodeFormat;
        Body.Text = win.ResultText;
        Body.CaretIndex = win.ResultCaret;
        Body.SelectionLength = win.ResultSelectionLength;
        if (win.Committed && changedInWindow)
        {
            // Save was chosen in the enlarged editor, and Save means disk everywhere. The code
            // format has no control in the Manager, so it goes onto the snippet directly before
            // CommitAndPersistCurrent() writes the rest.
            target.CodeFormat = win.ResultCodeFormat.Length == 0 ? null : win.ResultCodeFormat;
            CommitAndPersistCurrent();
        }
        Body.FocusEditor();
        if (_reloadPending) QueuePendingReload();
    }

    /// <summary>Ctrl+Shift+Enter / F11 while editing the body opens the enlarged editor.</summary>
    private void MaybeExpandBodyFromKey(KeyEventArgs e)
    {
        if (_editing == null || !Body.IsKeyboardFocusWithin) return;
        bool combo = e.Key == Key.Enter &&
                     Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift);
        if (combo || e.Key == Key.F11)
        {
            OnExpandBody(this, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    private void OnUseVarsToggled(object sender, RoutedEventArgs e) =>
        Body.UseVariables = UseVars.IsChecked == true;

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        // Reflect a prefix change made in Settings while this window stayed open.
        AbbrPrefixText.Text = AppState.Current.Settings.AbbrPrefix;
        // A reload that arrived while a modal dialog was open is reconciled after that dialog's
        // caller has finished handling its result.
        if (IsStale && !HasOpenOwnedWindow()) QueuePendingReload();
        // A filter tick deferred while a modal was open is applied now, after its selection can no
        // longer change behind the dialog.
        if (_filterRefreshPending && !HasOpenOwnedWindow())
        {
            _filterRefreshPending = false;
            RefreshSnippets(SelectedSnippet);
        }
    }

    private void Load() => ApplyModel(AppState.Current.Store.LoadAll());

    /// <summary>Refresh from the snapshot AppState just loaded, without another disk read.</summary>
    private void LoadFromState() =>
        ApplyModel(AppState.Current.SnapshotCategories().ToList());

    private void ApplyModel(List<Category> cats)
    {
        CleanupCreatedImages();
        _imageStore = AppState.Current.Store;
        var keepId = SelectedSnippet?.Id;
        _cats = cats;
        _editing = null;
        _editingCategory = null;
        _baseline = null;
        _dirty.Clear();
        _uncommittedSnippets.Clear();
        _deleted.Clear();
        _staleNoticeShown = false;
        Categories.ItemsSource = null;
        Categories.ItemsSource = _cats;
        if (_cats.Count > 0) Categories.SelectedIndex = 0;
        // With no category/snippet (e.g. an empty library) nothing set _editing, so make sure
        // the editor reflects that and is grayed out rather than a live-looking dead form.
        EditorPane.IsEnabled = _editing != null;
        // A reload can change which snippet names exist (add/rename/delete/category
        // add-or-delete all route through here) — clear any now-stale nested-reference marks.
        Body.Rescan();
        if (!string.IsNullOrEmpty(keepId)) SelectSnippet(keepId);
        _loadedVersion = AppState.Current.DataVersion;
    }

    private Category? SelectedCategory => Categories.SelectedItem as Category;
    private Snippet? SelectedSnippet => Snippets.SelectedItem as Snippet;

    /// <summary>Focus the name box ready to type (used after creating from the panel/tray).</summary>
    public void FocusNameField()
    {
        SnippetName.Focus();
        SnippetName.SelectAll();
    }

    /// <summary>Navigate to the snippet with the given id (e.g. after "new from clipboard" or
    /// "edit" from the search panel).</summary>
    public void SelectSnippet(string id)
    {
        foreach (var c in _cats)
        {
            if (c.Snippets.FirstOrDefault(x => x.Id == id) is not { } sn) continue;
            Categories.SelectedItem = c;      // triggers RefreshSnippets
            Snippets.SelectedItem = sn;
            Snippets.ScrollIntoView(sn);
            // Deferred, not synchronous: the SelectedItem assignments above only queue the
            // selection-changed handling that loads sn into Body — calling FocusEditor() here would
            // focus a control that's about to be repopulated (or, worse, still holds the PREVIOUS
            // snippet's content). Dispatching at Input priority runs after that load has completed,
            // in both the freshly-created-window case and the already-open one (the caller may be
            // re-targeting an already-visible Manager at a different snippet).
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input,
                new Action(() => Body.FocusEditor()));
            return;
        }
    }

    /// <summary>Create a snippet from the search panel / tray straight into THIS window's model —
    /// the single source of truth — and focus its name box. Nothing is written to disk behind the
    /// Manager, so a stale in-memory cache can never overwrite it on the next save (the two-window /
    /// concurrent-edit data loss the earlier write-then-reload attempts kept reintroducing). It is
    /// persisted together with the Manager's other edits on save/close. <paramref name="body"/> is
    /// the snippet text (empty for a panel "new"); <paramref name="preferCategoryName"/> is an
    /// optional home category, resolved against this window's own list.</summary>
    public void AddSnippet(string name, string body, string? preferCategoryName = null)
    {
        if (!TryLeaveEditor()) return;
        if (!EnsureFreshForWrite()) return;
        var c = (preferCategoryName is not null ? _cats.FirstOrDefault(x => x.Name == preferCategoryName) : null)
                ?? SelectedCategory ?? _cats.FirstOrDefault();
        if (c is null)                   // empty library: start a category so the snippet has a home
        {
            c = new Category { Name = UniqueCategoryName() };
            _cats.Add(c);
            Categories.ItemsSource = null;
            Categories.ItemsSource = _cats;
        }
        var sn = new Snippet
            { Name = string.IsNullOrWhiteSpace(name) ? L("Manager.NewSnippetName") : name, Body = body ?? "" };
        c.Snippets.Add(sn);
        _uncommittedSnippets.Add(sn.Id);
        _dirty.Add(c);
        if (FilterBox.Text.Length > 0) FilterBox.Text = "";   // a filter would hide the new row
        SelectCategoryByName(c.Name);
        RefreshSnippets(sn);                                  // land on (and reveal) the new snippet
        Body.Rescan();   // the new name may resolve a nested reference elsewhere
        FocusNameField();
    }

    /// <summary>Silent capture saves the committed category and updates this window's membership,
    /// preserving its drafts while ensuring a later editor save retains the captured snippet.
    /// Returns false when this window is stale or capture fails; the caller then falls back to
    /// the disk path (whose reload will make this window reconcile or pause saving).</summary>
    public async Task<bool> TryAddCapturedSnippetAsync(string name, string body)
    {
        if (IsStale) return false;
        Snippet? sn;
        try
        {
            sn = await AppState.Current.CaptureClipboardAsync(name, body, notify: false);
        }
        catch (Exception ex)
        {
            Core.Log.Error("capture clipboard", ex);
            return false;
        }
        if (sn == null) return false;
        var state = AppState.Current;
        var categoryName = state.CategoryOf(sn.Id);
        var c = _cats.FirstOrDefault(cat => cat.Name == categoryName);
        if (c == null)
        {
            c = new Category { Name = categoryName, Color = state.Categories.First(cat => cat.Name == categoryName).Color };
            _cats.Add(c);
            var selectedCategory = Categories.SelectedItem;
            _revertingSelection = true;
            try
            {
                Categories.ItemsSource = null;
                Categories.ItemsSource = _cats;
                Categories.SelectedItem = selectedCategory;
            }
            finally { _revertingSelection = false; }
        }
        if (!c.Snippets.Any(s => s.Id == sn.Id)) c.Snippets.Add(sn);
        _loadedVersion = state.DataVersion;
        _staleNoticeShown = false;
        if (ReferenceEquals(SelectedCategory, c))
        {
            // Keep the editor exactly where it was: RefreshSnippets would re-select a row and
            // re-enter TryLeaveEditor, turning a silent capture into an unsaved-changes prompt.
            var selected = SelectedSnippet;
            _revertingSelection = true;
            try
            {
                Snippets.ItemsSource = FilteredSnippets(c);
                if (selected != null) Snippets.SelectedItem = selected;
            }
            finally { _revertingSelection = false; }
        }
        Body.Rescan();   // the new name may resolve a nested reference in the open editor
        return true;
    }

    /// <summary>The editable fields as the controls currently hold them.</summary>
    private SnippetEdit CurrentEdit() => SnippetEdit.Of(
        SnippetName.Text, Abbr.Text, Body.Text, UseVars.IsChecked == true,
        OutputModes[Math.Max(0, OutputMode.SelectedIndex)], _editing?.CodeFormat, _editing?.ImagePath);

    /// <summary>Snapshot the loaded snippet as the new "unchanged" state.</summary>
    private void ResetBaseline()
    {
        _baseline = _editing == null ? null : SnippetEdit.From(_editing);
        _baselineUpdatedAt = _editing?.UpdatedAt ?? default;
        UpdateSaveStatus();
    }

    private void UpdateSaveStatus()
    {
        bool dirty = EditorIsDirty() || _dirty.Count > 0;
        UnsavedHint.Visibility = dirty ? Visibility.Visible : Visibility.Collapsed;
        // A success animation must stop as soon as a new edit makes it out of date.
        if (dirty)
        {
            SavedHint.BeginAnimation(OpacityProperty, null);
            SavedHint.Opacity = 0;
            SavedHint.Visibility = Visibility.Collapsed;
        }
    }

    private bool EditorIsDirty() =>
        _editing != null && _baseline != null && (CurrentEdit() != _baseline ||
            (_uncommittedSnippets.Contains(_editing.Id) && !_editing.IsBlankDraft(L("Manager.NewSnippetName"))));

    // Flush the editor fields back into the snippet currently loaded, so switching
    // selection (or closing) never silently discards unsaved edits.
    private void CommitEditor()
    {
        if (_editing == null) return;
        bool useVars = UseVars.IsChecked == true;
        string outputMode = OutputModes[Math.Max(0, OutputMode.SelectedIndex)];
        if (CurrentEdit() != SnippetEdit.From(_editing) || EditorIsDirty())
        {
            _editing.Name = SnippetName.Text;
            _editing.Abbr = Abbr.Text;
            _editing.Body = Body.Text;
            _editing.UseVariables = useVars;
            _editing.OutputMode = outputMode;
            _editing.UpdatedAt = DateTimeOffset.UtcNow;
            if (_editingCategory != null) _dirty.Add(_editingCategory);
        }
    }

    /// <summary>Commit the editor onto the loaded snippet, write just that snippet's category, and
    /// take a fresh baseline. The single "the user said save" path — used by the Save button's
    /// prompt-free flow's sibling and by every TryLeaveEditor prompt that answers Save.</summary>
    private bool CommitAndPersistCurrent()
    {
        if (!EnsureFreshForWrite()) return false;
        CommitEditor();
        bool ok = _editingCategory == null || PersistCategory(_editingCategory);
        if (!ok) { AlertSaveFailed(); return false; }
        ResetBaseline();
        // A rename changes the list label and can create or break a {片段:名称} reference.
        Snippets.Items.Refresh();
        Body.Rescan();
        return ok;
    }

    /// <summary>
    /// Gate every path that leaves the snippet currently in the editor. Returns false when the
    /// user chose Cancel, a required save failed, or a stale-library reload discarded the local snapshot; the caller
    /// MUST then abort whatever it was doing (the navigation can be retried after the reload).
    /// <para>Structural operations call this too, but what it asks about is the half-finished
    /// snippet sitting in the EDITOR (those operations move the selection or rewrite the list).
    /// It never asks about the structural operation itself — renames, drags and deletes are
    /// deliberate acts with a visible result and keep their own existing save timing.</para>
    /// </summary>
    private bool TryLeaveEditor()
    {
        if (!EditorIsDirty()) return true;
        if (IsStale)
        {
            // The dialog below would save into a stale model; offer the reload first.
            return EnsureFreshForWrite();
        }

        bool? choice = AppDialog.ConfirmSaveDiscard(this, L("App.Name"), L("Manager.UnsavedConfirm"),
            L("Manager.Save"), L("Manager.DontSave"), L("Dialog.Cancel"));
        if (choice == null) return false;

        if (choice == true) return CommitAndPersistCurrent();
        RestoreEditorFromBaseline();
        return true;
    }

    /// <summary>Restore both controls and the model: a failed save may already have copied the
    /// draft into the model, and a later category save must not resurrect a discarded edit.</summary>
    private void RestoreEditorFromBaseline()
    {
        if (_baseline is not { } b) return;
        if (_editing != null && _uncommittedSnippets.Remove(_editing.Id))
        {
            var category = _editingCategory;
            category?.Snippets.Remove(_editing);
            if (category != null) ReleaseUnchangedCategory(category);
            _editing = null;
            _editingCategory = null;
            CleanupCreatedImages(preserveDrafts: true);
            return;
        }
        if (_editing != null)
        {
            b.ApplyTo(_editing);
            _editing.UpdatedAt = _baselineUpdatedAt;
        }
        if (_editingCategory != null) ReleaseUnchangedCategory(_editingCategory);
        CleanupCreatedImages(preserveDrafts: true);
        UpdateSnippetImage();
        SnippetName.Text = b.Name;
        Abbr.Text = b.Abbr;
        Body.Text = b.Body;
        UseVars.IsChecked = b.UseVariables;
        Body.UseVariables = b.UseVariables;
        int mode = Array.IndexOf(OutputModes, b.OutputMode);
        OutputMode.SelectedIndex = mode < 0 ? 0 : mode;
        UpdateAbbrConflictHint();
    }

    private void ReleaseUnchangedCategory(Category category)
    {
        var saved = AppState.Current.Categories.FirstOrDefault(c => c.Name == category.Name);
        if (saved == null && category.Snippets.Count == 0)
        {
            _cats.Remove(category);
            _dirty.Remove(category);
        }
        else if (saved != null && saved.Color == category.Color && category.Snippets
            .Select(s => (s.Id, SnippetEdit.From(s), s.UpdatedAt))
            .SequenceEqual(saved.Snippets.Select(s => (s.Id, SnippetEdit.From(s), s.UpdatedAt))))
            _dirty.Remove(category);
    }

    /// <summary>Drop empty drafts so a "new" the user abandoned isn't kept as a blank row (see
    /// Snippet.IsBlankDraft). Runs only on window close: while the Manager is open a blank row may be
    /// one the user just created and is about to fill (or lined up several of), so pruning on every
    /// Save would delete those — and an unrelated blank starter in another category — out from under
    /// them. By close, a still-blank draft is genuinely abandoned. Marks touched categories dirty so
    /// the removal is written, and clears the editor reference if the edited snippet was removed.</summary>
    private void PruneBlankDrafts()
    {
        var defaultName = L("Manager.NewSnippetName");   // resolve the localized label once, not per snippet
        foreach (var c in _cats)
            if (c.Snippets.RemoveAll(s => s.IsBlankDraft(defaultName)) > 0) _dirty.Add(c);
        if (_editing != null && !_cats.Any(c => c.Snippets.Contains(_editing)))
        {
            _editing = null; _editingCategory = null;
        }
    }

    /// <summary>
    /// Write every dirty category. A write can throw (IOException) when the data folder is a
    /// sync drive and the client has the file open — catch it so a save never crashes the app;
    /// the category stays dirty and retries on the next save/close. Returns false if any failed.
    ///
    /// Callers decide how to report failure. Navigation and close must keep the draft available
    /// for retry when a required write fails.
    /// </summary>
    private bool PersistDirty()
    {
        if (IsStale || AppState.Current.IsLibraryBusy) return false;
        bool ok = true;
        foreach (var c in _dirty.ToList())
            if (!PersistCategory(c))
            {
                ok = false;
                if (_saveFailureKey == "Manager.StaleNotice") break;
            }
        return ok;
    }

    /// <summary>Write ONE dirty category; drop it from _dirty on success. Returns false if the
    /// write threw (it stays dirty for a later retry). Lets a specific commit point (DeleteSnippets)
    /// report its own category's failure without misattributing an unrelated dirty category's.</summary>
    private bool PersistCategory(Category c)
    {
        if (!_dirty.Contains(c)) return true;
        if (IsStale || AppState.Current.IsLibraryBusy) return false;
        _saveFailureKey = "Manager.SaveFailed";
        try
        {
            AppState.Current.MarkSelfWrite();
            var previousImages = AppState.Current.Categories.Where(x => x.Name == c.Name)
                .SelectMany(x => x.Snippets).Where(s => s.IsImage).Select(s => s.ImagePath).ToList();
            AppState.Current.Store.SaveCategory(c);
            _dirty.Remove(c);
            foreach (var snippet in c.Snippets) _uncommittedSnippets.Remove(snippet.Id);
            // Publish only this successful write. Other categories may still contain drafts or
            // have failed to save; publishing the whole editor would expose unpersisted content.
            var saved = AppState.Current.SnapshotCategories().ToList();
            int index = saved.FindIndex(x => x.Name == c.Name);
            if (index < 0) saved.Add(c);
            else saved[index] = c;
            AppState.Current.ApplyCategories(saved);
            _loadedVersion = AppState.Current.DataVersion;
            foreach (var image in previousImages.Except(c.Snippets.Select(s => s.ImagePath), StringComparer.OrdinalIgnoreCase))
                AppState.Current.Store.DeleteUnreferencedImage(image);
            CleanupCreatedImages(preserveDrafts: true);
            return true;
        }
        catch (Core.Persistence.LibraryChangedException ex)
        {
            Core.Log.Error("save conflict", ex);
            _saveFailureKey = "Manager.StaleNotice";
            ReloadAfterConflict();
            return false;
        }
        catch (Exception ex)
        {
            Core.Log.Error("save category", ex);
            if (ex is InvalidDataException or System.Text.Json.JsonException) _saveFailureKey = "Msg.LibraryLoadWarning";
            return false;
        }
        finally { UpdateSaveStatus(); }
    }

    private async void ReloadAfterConflict()
    {
        try { await AppState.Current.ReloadDataAsync(); }
        catch (Exception ex) { Core.Log.Error("reload after save conflict", ex); }
    }

    /// <summary>The one "save failed" message. Centralized so every disk-write catch reports it
    /// identically and a future change (e.g. surfacing the exception detail) lands in one place.</summary>
    private void AlertSaveFailed() => AppDialog.Alert(this, L("App.Name"), L(_saveFailureKey));

    // ---------- snippet list (filter-aware) ----------
    private string _filter = "";
    private bool _filterRefreshPending;
    private readonly DispatcherTimer _filterDebounce = new() { Interval = TimeSpan.FromMilliseconds(120) };

    private void OnClearFilter(object sender, RoutedEventArgs e)
    {
        var selected = Snippets.SelectedItems.Cast<Snippet>().ToArray();
        FilterBox.Clear();
        FilterBox.Focus();
        _filterDebounce.Stop();
        // Clearing only broadens the list. Keep the editor's draft while ItemsSource resets
        // selection, instead of treating that transient reset as leaving the snippet.
        if (_editing != null && ReferenceEquals(_editingCategory, SelectedCategory))
        {
            _revertingSelection = true;
            try
            {
                RefreshSnippets(_editing);
                foreach (var snippet in selected)
                    if (!Snippets.SelectedItems.Contains(snippet)) Snippets.SelectedItems.Add(snippet);
                Snippets.ScrollIntoView(_editing);
            }
            finally { _revertingSelection = false; }
        }
        else ApplyFilter();
    }

    private void OnFilterChanged(object s, TextChangedEventArgs e)
    {
        // Keep the effective filter current immediately, but defer the full-category scan:
        // typing a word otherwise rebuilds the list once per keystroke, scanning every body.
        _filter = FilterBox.Text.Trim();
        _filterDebounce.Stop();
        _filterDebounce.Start();
    }

    private void ApplyFilter()
    {
        _filterDebounce.Stop();
        // A DispatcherTimer keeps ticking inside ShowDialog's nested loop. Refreshing here would
        // move the selection behind the modal (and can stack a second unsaved-changes prompt).
        if (HasOpenOwnedWindow()) { _filterRefreshPending = true; return; }
        _filterRefreshPending = false;
        RefreshSnippets(SelectedSnippet);
    }

    private List<Snippet> FilteredSnippets(Category c) =>
        string.IsNullOrEmpty(_filter)
            ? c.Snippets.ToList()
            : c.Snippets.Where(x =>
                    x.Name.Contains(_filter, StringComparison.OrdinalIgnoreCase)
                    || x.Abbr.Contains(_filter, StringComparison.OrdinalIgnoreCase)
                    || x.Body.Contains(_filter, StringComparison.OrdinalIgnoreCase))
                .ToList();

    /// <summary>Re-apply the filter; keep <paramref name="select"/> selected when it still matches, else select the first row.</summary>
    private void RefreshSnippets(Snippet? select = null)
    {
        if (SelectedCategory is not { } c) { Snippets.ItemsSource = null; return; }
        var items = FilteredSnippets(c);
        Snippets.ItemsSource = items;
        if (select != null && items.Contains(select)) Snippets.SelectedItem = select;
        else if (items.Count > 0) Snippets.SelectedIndex = 0;
        UpdateSaveStatus();
    }

    private void OnCategorySelected(object s, SelectionChangedEventArgs e)
    {
        if (_revertingSelection) return;
        if (!TryLeaveEditor())
        {
            _revertingSelection = true;
            try { Categories.SelectedItem = _editingCategory; }
            finally { _revertingSelection = false; }
            return;
        }
        RefreshSnippets();
    }

    private void OnSnippetSelected(object s, SelectionChangedEventArgs e)
    {
        if (_revertingSelection) return;
        if (!TryLeaveEditor())
        {
            // Cancelled: SelectionChanged already moved the selection, so put it back. The
            // re-entrancy flag stops that assignment prompting a second time.
            _revertingSelection = true;
            try { Snippets.SelectedItem = _editing; }
            finally { _revertingSelection = false; }
            return;
        }

        if (SelectedSnippet is { } sn)
        {
            _editing = sn;
            _editingCategory = SelectedCategory;
            SnippetName.Text = sn.Name; Abbr.Text = sn.Abbr; Body.Text = sn.Body;
            UseVars.IsChecked = sn.UseVariables;
            Body.UseVariables = sn.UseVariables;
            int mode = Array.IndexOf(OutputModes, sn.OutputMode ?? "");
            OutputMode.SelectedIndex = mode < 0 ? 0 : mode;
            UpdateAbbrConflictHint();
            UpdateUsageStat(sn.Id);
            UpdateSnippetImage();
            EditorPane.IsEnabled = true;
        }
        else { _editing = null; _editingCategory = null; ClearEditor(); }

        ResetBaseline();   // whatever we just loaded (or cleared) is the new "unchanged" state
    }

    private void ClearEditor()
    {
        SnippetName.Text = Abbr.Text = Body.Text = "";
        UseVars.IsChecked = false;
        Body.UseVariables = false;
        SnippetImagePeek.Source = null;
        OutputMode.SelectedIndex = 0;
        AbbrConflict.Visibility = Visibility.Collapsed;
        UsageStat.Text = "";
        SnippetImage.Source = null;
        // _editing is already null by every call site, so this collapses the (now dead) image
        // section instead of leaving it expanded over a form with no snippet behind it.
        ApplyImageSection();
        // Nothing selected — gray the editor out so typing can't silently vanish (there's no
        // snippet behind these fields until one is added/selected).
        EditorPane.IsEnabled = false;
    }

    /// <summary>Demystify frecency: show how often (and when) the selected snippet was sent.</summary>
    private void UpdateUsageStat(string id)
    {
        var usage = AppState.Current.Usage;
        int count = usage.CountOf(id);
        if (count == 0) { UsageStat.Text = L("Manager.NeverUsed"); return; }
        var last = usage.LastUsedOf(id);
        UsageStat.Text = string.Format(L("Manager.UsageStat"), count,
            last?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "—");
    }

    private void OnAbbrChanged(object sender, TextChangedEventArgs e) => UpdateAbbrConflictHint();

    /// <summary>
    /// Duplicate abbreviations collide silently in the matcher (last rebuilt wins), so
    /// surface the conflict right where the user is typing it. Case-insensitive, matching
    /// the matcher's comparer.
    /// </summary>
    private void UpdateAbbrConflictHint()
    {
        var abbr = Abbr.Text.Trim();
        string? other = null;
        if (abbr.Length > 0 && _editing != null)
            other = _cats.SelectMany(c => c.Snippets)
                .FirstOrDefault(s => s.Id != _editing.Id
                    && string.Equals(s.Abbr, abbr, StringComparison.OrdinalIgnoreCase))
                ?.Name;
        AbbrConflict.Text = other == null ? ""
            : string.Format(LocalizationService.Instance["Manager.AbbrConflict"], other);
        AbbrConflict.Visibility = other == null ? Visibility.Collapsed : Visibility.Visible;
    }

    // ---------- image snippets ----------
    // Decode off the UI thread: ImageLoader uses BitmapCacheOption.OnLoad, which reads and
    // decodes the whole file synchronously — and if the image is a OneDrive "files on-demand"
    // placeholder, merely touching it forces a synchronous download. Doing that on the UI
    // thread froze the window on a simple click. ImageLoader.Load Freezes the bitmap, so it's
    // safe to build on the background thread and assign on the UI thread.
    private int _imageLoadGen;   // UI-thread only: newest image load wins
    private void UpdateSnippetImage()
    {
        UpdateSaveStatus();
        // Bump first so any in-flight load (remove/replace/re-select all call this) is superseded
        // — keying only on the snippet identity let a slow load of a just-removed/replaced image
        // reappear over the correct state.
        int gen = ++_imageLoadGen;
        SnippetImage.Source = null;
        SnippetImagePeek.Source = null;
        // Collapse state keys off _editing.IsImage, not off the (still-loading) bitmap, so the
        // section opens for an image snippet immediately rather than after the load lands.
        ApplyImageSection();
        if (_editing is not { IsImage: true } s) return;
        var rel = s.ImagePath;
        // The 72-DIP box leaves 64 DIPs for the image; at Windows' 500% maximum scale that is
        // 320 physical px. Reuse the row's decode width so both surfaces share one cache entry.
        System.Threading.Tasks.Task.Run(() =>
        {
            var img = ImageLoader.Load(rel, 320);
            Dispatcher.BeginInvoke(new Action(() =>
            {
                // Both surfaces are filled HERE, once the bitmap exists — assigning the peek
                // synchronously below would only ever copy the null set above.
                if (gen == _imageLoadGen) { SnippetImage.Source = img; SnippetImagePeek.Source = img; }
            }));
        });
    }

    private void OnImageFromClipboard(object sender, RoutedEventArgs e)
    {
        if (_editing == null) return;
        // Clipboard access throws (CLIPBRD_E_CANT_OPEN) when another process holds it open —
        // a clipboard manager, RDP, Office. Retry a few times, then give up quietly rather
        // than crashing the window.
        BitmapSource? img = null;
        for (int i = 0; i < 5 && img == null; i++)
        {
            try { if (Clipboard.ContainsImage()) img = Clipboard.GetImage(); else return; }
            catch { System.Threading.Thread.Sleep(30); }
        }
        if (img != null) SetImage(EncodePng(img), ".png");
    }

    private void OnImageFromFile(object sender, RoutedEventArgs e)
    {
        if (_editing == null) return;
        var dlg = new Microsoft.Win32.OpenFileDialog
        { Filter = "Image (*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var ext = Path.GetExtension(dlg.FileName);
            SetImage(File.ReadAllBytes(dlg.FileName), string.IsNullOrEmpty(ext) ? ".png" : ext);
        }
        catch { }
    }

    private void OnImageRemove(object sender, RoutedEventArgs e)
    {
        if (_editing == null) return;
        if (!EnsureFreshForWrite()) return;
        _editing.ImagePath = "";
        _editing.UpdatedAt = DateTimeOffset.UtcNow;
        if (_editingCategory != null) _dirty.Add(_editingCategory);
        UpdateSnippetImage();
    }

    private void SetImage(byte[] data, string ext)
    {
        if (_editing == null) return;
        if (!EnsureFreshForWrite()) return;
        _editing.ImagePath = AppState.Current.Store.SaveImage(data, ext);
        _createdImages.Add(_editing.ImagePath);
        _editing.UpdatedAt = DateTimeOffset.UtcNow;
        if (_editingCategory != null) _dirty.Add(_editingCategory);
        UpdateSnippetImage();
    }

    private void CleanupCreatedImages(bool preserveDrafts = false)
    {
        foreach (var image in _createdImages.ToList())
        {
            if (preserveDrafts && _cats.SelectMany(c => c.Snippets).Any(s => s.ImagePath == image)) continue;
            _imageStore.DeleteUnreferencedImage(image);
            _createdImages.Remove(image);
        }
    }

    private static byte[] EncodePng(BitmapSource img)
    {
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(img));
        using var ms = new MemoryStream();
        enc.Save(ms);
        return ms.ToArray();
    }

    private static string L(string key) => LocalizationService.Instance[key];

    private void OnCategoryColor(object sender, RoutedEventArgs e)
    {
        if (SelectedCategory is not Category c || sender is not Button b) return;
        if (!TryLeaveEditor()) return;
        if (!EnsureFreshForWrite()) return;
        var prevColor = c.Color;
        c.Color = (b.Tag as string) ?? "";
        AppState.Current.MarkSelfWrite();
        try { AppState.Current.Store.SaveCategory(c); }
        catch { c.Color = prevColor; AlertSaveFailed(); }   // write didn't happen — revert to match disk
        Categories.Items.Refresh();
    }

    private void OnAddCategory(object s, RoutedEventArgs e)
    {
        if (!TryLeaveEditor()) return;
        if (!EnsureFreshForWrite()) return;
        var name = AppDialog.Prompt(this, L("Manager.NewCategory"), L("Manager.CategoryName"), UniqueCategoryName());
        if (string.IsNullOrWhiteSpace(name)) return;
        if (_cats.Any(x => x.Name == name))
        {
            AppDialog.Alert(this, L("App.Name"), L("Manager.DuplicateCategory"));
            return;
        }
        if (!PersistDirty()) { AlertSaveFailed(); return; }
        if (FilterBox.Text.Length > 0) FilterBox.Text = "";   // a stale filter would hide the starter snippet
        // Seed the new category with one starter snippet, so the (always-visible) editor is
        // bound to a real item from the start — otherwise anything typed into the fields of a
        // brand-new, empty category would be silently discarded on save.
        var first = new Snippet { Name = L("Manager.NewSnippetName") };
        var cat = new Category { Name = name, Snippets = { first } };
        AppState.Current.MarkSelfWrite();
        // On failure no index entry is committed, so the category doesn't appear. (SaveCategory
        // writes the file then the index; a rare partial failure — file written, index write
        // failed — can leave an orphan .json that the index-driven load simply ignores. Cleaning
        // it up would need transactional writes, which this file store doesn't do.)
        try { AppState.Current.Store.SaveCategory(cat); }
        catch { AlertSaveFailed(); return; }
        Load();                                    // one disk read: picks up the new category + index order
        AppState.Current.ApplyCategories(_cats);   // rebuild indexes from it (was a second full read)
        _loadedVersion = AppState.Current.DataVersion;
        SelectCategoryByName(name);                // its lone snippet auto-selects (RefreshSnippets picks row 0)
        FocusNameField();                          // ready to rename the starter snippet
    }

    private void SelectCategoryByName(string name)
    {
        if (_cats.FirstOrDefault(x => x.Name == name) is { } c) Categories.SelectedItem = c;
    }

    /// <summary>A default name for a new category — the localized base, suffixed to stay unique.</summary>
    private string UniqueCategoryName()
    {
        var baseName = L("Manager.NewCategoryName");
        if (_cats.All(x => x.Name != baseName)) return baseName;
        for (int i = 2; ; i++)
        {
            var candidate = $"{baseName} {i}";
            if (_cats.All(x => x.Name != candidate)) return candidate;
        }
    }

    private void OnRenameCategory(object s, RoutedEventArgs e)
    {
        if (!TryLeaveEditor()) return;
        if (!EnsureFreshForWrite()) return;
        if (SelectedCategory is not { } c) return;
        var name = AppDialog.Prompt(this, L("Manager.RenameCategory"), L("Manager.CategoryName"), c.Name);
        if (string.IsNullOrWhiteSpace(name) || name == c.Name) return;
        if (_cats.Any(x => x != c && x.Name == name))
        {
            AppDialog.Alert(this, L("App.Name"), L("Manager.DuplicateCategory"));
            return;
        }
        if (!PersistDirty()) { AlertSaveFailed(); return; }
        var oldName = c.Name;
        c.Name = name;
        // Write the new-name file+index entry, THEN delete the old one — never the reverse, so a
        // mid-failure can't lose the category. On failure revert the in-memory name and abort with
        // a message rather than a raw exception. KNOWN LIMITATION: SaveCategory adds the new index
        // entry before DeleteCategory removes the old, and these aren't a single transaction — if
        // the delete fails on a locked sync drive, index.json is left listing BOTH names (a
        // duplicate category appears on the next reload). A transactional store would be needed to
        // prevent it; not worth it for this rare case.
        AppState.Current.MarkSelfWrite();
        try
        {
            AppState.Current.Store.SaveCategory(c);
            AppState.Current.MarkSelfWrite();
            AppState.Current.Store.DeleteCategory(oldName);
        }
        catch { c.Name = oldName; AlertSaveFailed(); return; }
        Load();                                    // one disk read instead of ReloadData + Load
        AppState.Current.ApplyCategories(_cats);
        _loadedVersion = AppState.Current.DataVersion;
        SelectCategoryByName(name);
    }

    private void OnDeleteCategory(object s, RoutedEventArgs e)
    {
        if (!EnsureFreshForWrite()) return;
        if (SelectedCategory is not { } c) return;
        if (!AppDialog.Confirm(this, L("App.Name"),
                string.Format(L("Manager.DeleteCategoryConfirm"), c.Name), L("Manager.Delete"))) return;
        _dirty.Remove(c);   // don't bother persisting a category we're about to delete
        int idx = Categories.SelectedIndex;
        if (!PersistDirty()) { _dirty.Add(c); AlertSaveFailed(); return; }
        // Copy the snippets to the trash and delete the category on disk, BEFORE mutating the model:
        // on failure abort with a message rather than a raw exception, and re-add c to _dirty so its
        // own unsaved edits (dropped above) aren't lost. The snippets live on in the trash 30 days.
        // KNOWN LIMITATION: if MoveToTrash succeeds but DeleteCategory fails, the snippets sit in
        // the trash while still live in the library (a duplicate on restore, and a retry appends
        // another copy) — a non-transactional artifact not worth undoing here (the undo is itself a
        // write that can fail).
        try
        {
            if (c.Snippets.Count > 0) MoveToTrash(c.Name, c.Snippets.ToArray());
            AppState.Current.MarkSelfWrite();
            AppState.Current.Store.DeleteCategory(c.Name);
        }
        catch { _dirty.Add(c); AlertSaveFailed(); return; }
        if (_editingCategory == c) { _editing = null; _editingCategory = null; }
        Load();                                    // one disk read instead of ReloadData + Load
        AppState.Current.ApplyCategories(_cats);
        _loadedVersion = AppState.Current.DataVersion;
        if (_cats.Count > 0) Categories.SelectedIndex = Math.Min(idx, _cats.Count - 1);
    }

    private void OnAddSnippet(object s, RoutedEventArgs e)
    {
        if (!TryLeaveEditor()) return;
        if (!EnsureFreshForWrite()) return;
        if (SelectedCategory is not { } c) return;
        var sn = new Snippet { Name = L("Manager.NewSnippetName") };
        c.Snippets.Add(sn);
        _dirty.Add(c);
        if (FilterBox.Text.Length > 0) FilterBox.Text = "";   // a filter would hide the new row
        RefreshSnippets(sn);
        Body.Rescan();   // the new name may resolve a nested reference elsewhere
        SnippetName.Focus();
        SnippetName.SelectAll();
    }

    private void OnDuplicateSnippet(object s, RoutedEventArgs e)
    {
        if (!TryLeaveEditor()) return;
        if (!EnsureFreshForWrite()) return;
        if (SelectedCategory is not { } c || SelectedSnippet is not { } sn) return;
        // Abbr stays empty: two snippets with the same abbreviation would be ambiguous.
        var copy = new Snippet { Name = sn.Name + " (2)", Body = sn.Body, UseVariables = sn.UseVariables, OutputMode = sn.OutputMode, CodeFormat = sn.CodeFormat };
        if (sn.IsImage)
        {
            // Copy the image file — sharing one file would break when either snippet is deleted.
            try
            {
                var source = AppState.Current.Store.ResolveImage(sn.ImagePath)
                             ?? throw new FileNotFoundException("image path is outside the library");
                var bytes = File.ReadAllBytes(source);
                var ext = Path.GetExtension(sn.ImagePath);
                copy.ImagePath = AppState.Current.Store.SaveImage(bytes, string.IsNullOrEmpty(ext) ? ".png" : ext);
            }
            catch { AlertSaveFailed(); return; }   // don't create a silently image-less duplicate
        }
        c.Snippets.Insert(c.Snippets.IndexOf(sn) + 1, copy);
        _dirty.Add(c);
        RefreshSnippets(copy);
        Body.Rescan();   // the new name (" (2)") may resolve a nested reference elsewhere
        SnippetName.Focus();
        SnippetName.SelectAll();
    }

    private List<Snippet> SelectedSnippets() => Snippets.SelectedItems.Cast<Snippet>().ToList();

    private void OnDeleteSnippet(object s, RoutedEventArgs e) => DeleteSnippets(SelectedSnippets());

    private void DeleteSnippets(List<Snippet> selected)
    {
        if (!EnsureFreshForWrite()) return;
        if (SelectedCategory is not { } c || selected.Count == 0) return;
        int idx = Snippets.SelectedIndex;
        // Persist the trash copy FIRST (never lose the copy). Do it BEFORE touching the model:
        // if the trash write fails (locked sync drive), we must not have already removed the
        // snippets, or a later save would drop them from disk with no backup. Abort on failure.
        try { MoveToTrash(c.Name, selected.ToArray()); }
        catch { AlertSaveFailed(); return; }
        foreach (var sn in selected)
        {
            if (_editing == sn) { _editing = null; _editingCategory = null; }
            int at = c.Snippets.IndexOf(sn);
            if (at < 0) continue;
            c.Snippets.RemoveAt(at);
            // Recoverable two ways: Ctrl+Z in this session (one by one), or the trash
            // (30 days). Image files stay alive as long as their trash entry does.
            _deleted.Push((c, sn, at));
        }
        // A crash between the trash write and the category save leaves a duplicate, which
        // TrashDialog's restore de-dupes by Id.
        _dirty.Add(c);
        // A genuine commit point: if THIS category's save fails the delete silently reverts on
        // reopen (the copy is safe in the trash), so surface it. Check c specifically — the
        // aggregate PersistDirty result would misattribute an unrelated dirty category's write
        // failure to the delete. Other pending edits still flush (silently) as before.
        bool saved = PersistCategory(c);
        PersistDirty();
        if (!saved) AlertSaveFailed();
        RefreshSnippets();
        Body.Rescan();   // a deleted name may now dangle a nested reference elsewhere
        if (Snippets.Items.Count > 0) Snippets.SelectedIndex = Math.Min(idx, Snippets.Items.Count - 1);
        else ClearEditor();
    }

    // ---------- multi-select context menu (batch move / delete) ----------
    private void OnSnippetsMenuOpening(object sender, ContextMenuEventArgs e)
    {
        var selected = SelectedSnippets();
        if (selected.Count == 0) { e.Handled = true; return; }
        Snippets.ContextMenu = BuildSelectionMenu(selected);
    }

    private ContextMenu BuildSelectionMenu(List<Snippet> selected)
    {
        var menu = new ContextMenu { Style = (Style)FindResource("DarkContextMenu"), FlowDirection = FlowDirection };
        var itemStyle = (Style)FindResource("DarkMenuItem");

        // Icons ride the same vocabulary as the tray and row menus: every command
        // item in the app shows its glyph. Delete stays muted, not red — deletion here is
        // reversible (the trash's 30 days), and the danger colour means "gone for good".
        MenuItem WithGlyph(string header, string glyph, System.Windows.Media.Brush? tint = null)
        {
            var mi = new MenuItem { Header = header, Style = itemStyle };
            mi.Icon = new System.Windows.Controls.TextBlock
            {
                Text = glyph,
                FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
                FontSize = 14,
                Foreground = tint ?? (System.Windows.Media.Brush)FindResource("Brush.TextMuted"),
            };
            return mi;
        }

        var move = WithGlyph(L("Manager.MoveTo"), "\uE8DE");
        foreach (var cat in _cats.Where(x => !ReferenceEquals(x, SelectedCategory)))
        {
            var target = cat;
            var mi = new MenuItem { Header = cat.Name, Style = itemStyle };
            mi.Click += (_, _) => MoveSelectedTo(target);
            move.Items.Add(mi);
        }
        move.IsEnabled = move.Items.Count > 0;
        menu.Items.Add(move);

        var del = WithGlyph(string.Format(L("Manager.DeleteSelected"), selected.Count), "\uE74D");
        del.Click += (_, _) => DeleteSnippets(SelectedSnippets());
        menu.Items.Add(del);
        return menu;
    }

    /// <summary>--shots only: the selection menu exactly as a real right-click builds it.</summary>
    internal ContextMenu? ShotsSelectionMenu()
    {
        if (Snippets.Items.Count == 0) return null;
        Snippets.SelectedIndex = 0;
        return BuildSelectionMenu(SelectedSnippets());
    }

    /// <summary>--shots only: focus the tiny ghost "+" — the hardest focus target to make visible.</summary>
    internal void ShotsFocusGhost() => System.Windows.Input.Keyboard.Focus(AddCategoryButton);

    /// <summary>--shots only: load a snippet whose abbreviation collides with another's, so the
    /// red inline conflict hint has a fixture (it only ever appears from live typing otherwise).</summary>
    internal void ShotsAbbrConflict()
    {
        if (Snippets.Items.Count < 2) return;
        Snippets.SelectedIndex = 0;
        if (Snippets.Items[1] is Snippet other && !string.IsNullOrEmpty(other.Abbr))
            Abbr.Text = other.Abbr;
    }

    /// <summary>--shots only: freeze the "✓ 已保存" flash at full opacity (it self-fades in 2s).</summary>
    internal void ShotsSaved()
    {
        SavedHint.BeginAnimation(OpacityProperty, null);
        SavedHint.Visibility = Visibility.Visible;
        SavedHint.Opacity = 1;
    }

    /// <summary>--shots only: expand the image section with a generated thumbnail.</summary>
    internal void ShotsImageSection()
    {
        var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(64, 64, 96, 96,
            System.Windows.Media.PixelFormats.Pbgra32);
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
            dc.DrawRectangle(new System.Windows.Media.LinearGradientBrush(
                System.Windows.Media.Colors.DeepSkyBlue, System.Windows.Media.Colors.MediumSeaGreen, 45),
                null, new Rect(0, 0, 64, 64));
        rtb.Render(dv);
        rtb.Freeze();   // ImageSource straight from memory — a temp PNG would lock on the second theme
        ImageToggle.IsChecked = true;          // fires OnImageSectionToggled → reveals the section
        SnippetImage.Source = rtb;
        ImagePeek.Visibility = Visibility.Visible;
        SnippetImagePeek.Source = rtb;
    }

    private void MoveSelectedTo(Category target)
    {
        if (!TryLeaveEditor()) return;
        if (!EnsureFreshForWrite()) return;
        if (SelectedCategory is not { } from || ReferenceEquals(from, target)) return;
        MoveSnippets(from, target, SelectedSnippets());
        RefreshSnippets();
        if (Snippets.Items.Count == 0) ClearEditor();
    }

    private void MoveSnippets(Category from, Category target, List<Snippet> snippets)
    {
        var added = snippets.Where(s => !target.Snippets.Any(t => t.Id == s.Id)).ToList();
        bool targetWasDirty = _dirty.Contains(target);
        target.Snippets.AddRange(added);
        _dirty.Add(target);
        if (!PersistCategory(target))
        {
            foreach (var sn in added) target.Snippets.Remove(sn);
            if (!targetWasDirty) _dirty.Remove(target);
            AlertSaveFailed();
            return;
        }
        // The destination is durable before removing the source. A failed source write leaves a
        // recoverable duplicate, and the Id check above makes retrying that move idempotent.
        foreach (var sn in snippets)
        {
            if (_editing == sn) { _editing = null; _editingCategory = null; }
            from.Snippets.Remove(sn);
        }
        _dirty.Add(from);
        if (!PersistCategory(from)) AlertSaveFailed();
    }

    private static void MoveToTrash(string category, params Snippet[] snippets)
    {
        var state = AppState.Current;
        state.MarkSelfWrite();
        var trash = state.Store.LoadTrash();
        foreach (var sn in snippets)
            trash.Add(new TrashEntry { Snippet = sn, Category = category });
        state.MarkSelfWrite();
        state.Store.SaveTrash(trash);
    }

    // ---------- undo delete (Ctrl+Z) ----------
    private readonly Stack<(Category Cat, Snippet Snippet, int Index)> _deleted = new();

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Z && (Keyboard.Modifiers & ModifierKeys.Control) != 0
            && e.OriginalSource is not TextBox)   // don't hijack undo inside text editors
        {
            UndoDelete();
            e.Handled = true;
        }
        else
        {
            // Ctrl+Shift+Enter / F11 while editing the body opens the enlarged editor.
            MaybeExpandBodyFromKey(e);
        }
        UpdateSaveStatus();
        base.OnPreviewKeyDown(e);
    }

    private void UndoDelete()
    {
        if (!EnsureFreshForWrite()) return;
        if (_deleted.Count == 0) return;
        var (cat, sn, index) = _deleted.Peek();
        if (!_cats.Contains(cat)) { _deleted.Pop(); return; }   // its category was deleted meanwhile
        // Match the trash dialog's restore order: keep the trash copy until the category is on
        // disk. A retry after trash cleanup failed must not insert the same Id twice.
        bool inserted = !cat.Snippets.Any(s => s.Id == sn.Id);
        bool wasDirty = _dirty.Contains(cat);
        if (inserted) cat.Snippets.Insert(Math.Min(index, cat.Snippets.Count), sn);
        _dirty.Add(cat);
        if (!PersistCategory(cat))
        {
            if (inserted) cat.Snippets.Remove(sn);
            if (!wasDirty) _dirty.Remove(cat);
            AlertSaveFailed();
            return;
        }

        var state = AppState.Current;
        try
        {
            state.MarkSelfWrite();
            var trash = state.Store.LoadTrash();
            if (trash.RemoveAll(t => t.Snippet.Id == sn.Id) > 0)
            {
                state.MarkSelfWrite();
                state.Store.SaveTrash(trash);
            }
            _deleted.Pop();
        }
        catch { AlertSaveFailed(); }   // the category is safe; retain the undo entry to retry cleanup
        Categories.SelectedItem = cat;
        RefreshSnippets(sn);
        Body.Rescan();   // the restored name may resolve a nested reference elsewhere
    }

    private void CanSaveCommand(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = !AppState.Current.IsLibraryBusy && !HasOpenOwnedWindow()
            && (_editing != null || _dirty.Count > 0);
        e.Handled = true;
    }

    private void OnSaveCommand(object sender, ExecutedRoutedEventArgs e)
    {
        OnSave(sender, e);
        e.Handled = true;
    }

    private void CanFindCommand(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = !HasOpenOwnedWindow();
        e.Handled = true;
    }

    private void OnFindCommand(object sender, ExecutedRoutedEventArgs e)
    {
        FilterBox.Focus();
        FilterBox.SelectAll();
        e.Handled = true;
    }

    private void OnSave(object s, RoutedEventArgs e)
    {
        if (!EnsureFreshForWrite()) return;
        CommitEditor();
        // Two snippets with the same abbreviation would expand ambiguously — warn (still saves).
        if (_editing is { } cur && !string.IsNullOrWhiteSpace(cur.Abbr))
        {
            var clash = _cats.SelectMany(x => x.Snippets)
                .FirstOrDefault(x => x.Id != cur.Id
                    && string.Equals(x.Abbr.Trim(), cur.Abbr.Trim(), StringComparison.OrdinalIgnoreCase));
            if (clash != null)
                AppDialog.Alert(this, L("App.Name"),
                    string.Format(L("Manager.DuplicateAbbr"), cur.Abbr, clash.Name));
        }
        var keep = SelectedSnippet;
        if (SelectedCategory is { } c) _dirty.Add(c);
        bool ok = PersistDirty();
        if (!ok) { AlertSaveFailed(); return; }
        AppState.Current.ApplyCategories(_cats);
        _loadedVersion = AppState.Current.DataVersion;
        _staleNoticeShown = false;
        ResetBaseline();   // what we just wrote is the new "unchanged" state
        // Refresh names, then keep the user on the snippet they were editing.
        RefreshSnippets(keep);
        Categories.Items.Refresh();
        FlashSaved();
    }

    private void FlashSaved()
    {
        SavedHint.Visibility = Visibility.Visible;
        SavedHint.Opacity = 1;
        var fade = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromSeconds(1.4)))
            { BeginTime = TimeSpan.FromSeconds(0.7) };
        fade.Completed += (_, _) => SavedHint.Visibility = Visibility.Collapsed;
        SavedHint.BeginAnimation(OpacityProperty, fade);
    }

    // ---------- drag: reorder + move-to-category ----------
    private Point _dragStart;
    private object? _dragItem;   // Category or Snippet under the mouse at press

    private static T? FindAncestor<T>(DependencyObject? d) where T : DependencyObject =>
        TreeSearch.FindAncestor<T>(d);

    private void OnListPressed(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(null);
        _dragItem = e.OriginalSource is DependencyObject d && FindAncestor<ListBoxItem>(d) is { } item
            ? item.DataContext : null;
    }

    private void OnListMove(object sender, MouseEventArgs e)
    {
        if (_dragItem == null || e.LeftButton != MouseButtonState.Pressed) return;
        var pos = e.GetPosition(null);
        if (Math.Abs(pos.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        var item = _dragItem;
        _dragItem = null;
        DragDrop.DoDragDrop((DependencyObject)sender, item, DragDropEffects.Move);
    }

    private static T? DropTarget<T>(DragEventArgs e) where T : class =>
        e.OriginalSource is DependencyObject d && FindAncestor<ListBoxItem>(d) is { } it
            ? it.DataContext as T : null;

    /// <summary>Drop on the snippet list: reorder within the current category.</summary>
    private void OnSnippetsDrop(object sender, DragEventArgs e)
    {
        if (!EnsureFreshForWrite()) return;
        if (SelectedCategory is not { } c) return;
        if (e.Data.GetData(typeof(Snippet)) is not Snippet dragged) return;
        if (!string.IsNullOrEmpty(_filter)) return;   // reordering a filtered view is ambiguous
        int from = c.Snippets.IndexOf(dragged);
        if (from < 0) return;
        var target = DropTarget<Snippet>(e);
        if (ReferenceEquals(target, dragged)) return; // dropped onto itself — nothing to move
        c.Snippets.RemoveAt(from);
        int to = target != null ? c.Snippets.IndexOf(target) : c.Snippets.Count;
        if (to < 0) to = c.Snippets.Count;
        c.Snippets.Insert(to, dragged);
        _dirty.Add(c);
        RefreshSnippets(dragged);
    }

    /// <summary>Drop on the category list: a snippet moves to that category; a category reorders.</summary>
    private void OnCategoriesDrop(object sender, DragEventArgs e)
    {
        if (!EnsureFreshForWrite()) return;
        if (e.Data.GetData(typeof(Snippet)) is Snippet sn)
        {
            var dest = DropTarget<Category>(e);
            if (dest == null || SelectedCategory is not { } src || ReferenceEquals(dest, src)) return;
            if (!TryLeaveEditor()) return;
            MoveSnippets(src, dest, new List<Snippet> { sn });
            RefreshSnippets();
            Categories.Items.Refresh();   // update counts
            return;
        }

        if (e.Data.GetData(typeof(Category)) is Category dragged)
        {
            var target = DropTarget<Category>(e);
            if (target == null || ReferenceEquals(dragged, target)) return;
            int from = _cats.IndexOf(dragged);
            int to = _cats.IndexOf(target);
            if (from < 0 || to < 0) return;
            _cats.RemoveAt(from);
            _cats.Insert(to, dragged);
            AppState.Current.MarkSelfWrite();
            try { AppState.Current.Store.ReorderCategories(_cats.Select(x => x.Name).ToList()); }
            catch
            {
                _cats.Remove(dragged);
                _cats.Insert(from, dragged);   // reorder didn't persist — restore the shown order
                AlertSaveFailed();
            }
            Categories.ItemsSource = null;
            Categories.ItemsSource = _cats;
            Categories.SelectedItem = dragged;
        }
    }

    private void OnClosing(object? s, System.ComponentModel.CancelEventArgs e)
    {
        // --smoke/--shots park and close windows whose fixtures deliberately dirty the editor
        // (the conflict shot types into Abbr). The unsaved-changes prompt would open a modal
        // dialog the harness waits on forever, and a close-time persist would write fixture
        // state to the real library. Discard silently.
        if (App.InSmoke) { _deleted.Clear(); return; }
        // The editor's own half-finished edit is the only thing that gets asked about. Structural
        // changes (renames, drags, deletes) are deliberate acts with a visible result and keep
        // their existing save timing — asking about them again would be noise, not safety.
        if (IsStale)
        {
            if (_dirty.Count > 0 || EditorIsDirty())
            {
                bool discard = AppDialog.Confirm(this, L("App.Name"), L("Manager.StaleNotice"),
                    L("Manager.DontSave"), L("Dialog.Cancel"));
                if (!discard) { e.Cancel = true; return; }
            }
            _dirty.Clear();
            _deleted.Clear();
            CleanupCreatedImages();
            return;   // never write or publish the stale model
        }
        if (!TryLeaveEditor()) { e.Cancel = true; return; }

        PruneBlankDrafts();   // an abandoned blank "new" shouldn't survive to the next launch
        if (!PersistDirty())
        {
            e.Cancel = true;
            AlertSaveFailed();
            return;
        }
        // Deleted snippets (and their image files) are owned by the trash now — its
        // 30-day purge is what finally deletes image files.
        _deleted.Clear();
        AppState.Current.ApplyCategories(_cats);
        _loadedVersion = AppState.Current.DataVersion;
        CleanupCreatedImages();
    }

    private void OnOpenTrash(object s, RoutedEventArgs e)
    {
        if (!TryLeaveEditor()) return;
        if (!EnsureFreshForWrite()) return;
        if (!PersistDirty()) { AlertSaveFailed(); return; }
        new TrashDialog { Owner = this }.ShowDialog();
        Load();   // one disk read: restored snippets should appear immediately
        AppState.Current.ApplyCategories(_cats);
        _loadedVersion = AppState.Current.DataVersion;
    }
}
