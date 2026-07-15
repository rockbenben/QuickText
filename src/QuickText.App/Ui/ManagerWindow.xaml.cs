using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
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

        WrapToggle.IsChecked = AppState.Current.Settings.EditorWrap;
        ApplyWrap();

        Closing += OnClosing;
        Load();
    }

    private void ApplyWrap()
    {
        bool wrap = WrapToggle.IsChecked == true;
        Body.TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
        Body.HorizontalScrollBarVisibility = wrap ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
    }

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

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        // Reflect a prefix change made in Settings while this window stayed open.
        AbbrPrefixText.Text = AppState.Current.Settings.AbbrPrefix;
    }

    private void Load()
    {
        _cats = AppState.Current.Store.LoadAll();
        _editing = null;
        _editingCategory = null;
        Categories.ItemsSource = null;
        Categories.ItemsSource = _cats;
        if (_cats.Count > 0) Categories.SelectedIndex = 0;
        // With no category/snippet (e.g. an empty library) nothing set _editing, so make sure
        // the editor reflects that and is grayed out rather than a live-looking dead form.
        EditorPane.IsEnabled = _editing != null;
    }

    private Category? SelectedCategory => Categories.SelectedItem as Category;
    private Snippet? SelectedSnippet => Snippets.SelectedItem as Snippet;

    /// <summary>Focus the name box ready to type (used after creating from the panel/tray).</summary>
    public void FocusNameField()
    {
        SnippetName.Focus();
        SnippetName.SelectAll();
    }

    /// <summary>Navigate to the snippet with the given id (e.g. after "new from clipboard").</summary>
    public void SelectSnippet(string id)
    {
        foreach (var c in _cats)
        {
            if (c.Snippets.FirstOrDefault(x => x.Id == id) is not { } sn) continue;
            Categories.SelectedItem = c;      // triggers RefreshSnippets
            Snippets.SelectedItem = sn;
            Snippets.ScrollIntoView(sn);
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
        CommitEditor();                  // fold any in-progress editor text into _dirty first — keep it
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
        _dirty.Add(c);
        if (FilterBox.Text.Length > 0) FilterBox.Text = "";   // a filter would hide the new row
        SelectCategoryByName(c.Name);
        RefreshSnippets(sn);                                  // land on (and reveal) the new snippet
        FocusNameField();
    }

    // Flush the editor fields back into the snippet currently loaded, so switching
    // selection (or closing) never silently discards unsaved edits.
    private void CommitEditor()
    {
        if (_editing == null) return;
        bool useVars = UseVars.IsChecked == true;
        string outputMode = OutputModes[Math.Max(0, OutputMode.SelectedIndex)];
        if (_editing.Name != SnippetName.Text || _editing.Abbr != Abbr.Text || _editing.Body != Body.Text
            || _editing.UseVariables != useVars || (_editing.OutputMode ?? "") != outputMode)
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
    /// Deliberately silent: most callers use it only to flush unrelated pending edits before a
    /// structural op (add/rename/delete-category, undo, close, open-trash) and MUST NOT pop a
    /// modal — a dialog during window close, or one misattributed to an unrelated category the
    /// user didn't ask to save, is worse than the silent retry. Only the two genuine "commit"
    /// points — OnSave and DeleteSnippets, where a failed write silently reverts on reopen —
    /// check the bool and alert.
    /// </summary>
    private bool PersistDirty()
    {
        bool ok = true;
        foreach (var c in _dirty.ToList())
            if (!PersistCategory(c)) ok = false;
        return ok;
    }

    /// <summary>Write ONE dirty category; drop it from _dirty on success. Returns false if the
    /// write threw (it stays dirty for a later retry). Lets a specific commit point (DeleteSnippets)
    /// report its own category's failure without misattributing an unrelated dirty category's.</summary>
    private bool PersistCategory(Category c)
    {
        if (!_dirty.Contains(c)) return true;
        try
        {
            AppState.Current.MarkSelfWrite();
            AppState.Current.Store.SaveCategory(c);
            _dirty.Remove(c);
            return true;
        }
        catch { return false; }   // keep it dirty; a later save/close retries
    }

    /// <summary>The one "save failed" message. Centralized so every disk-write catch reports it
    /// identically and a future change (e.g. surfacing the exception detail) lands in one place.</summary>
    private void AlertSaveFailed() => AppDialog.Alert(this, L("App.Name"), L("Manager.SaveFailed"));

    // ---------- snippet list (filter-aware) ----------
    private string _filter = "";

    private void OnFilterChanged(object s, TextChangedEventArgs e)
    {
        _filter = FilterBox.Text.Trim();
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
    }

    private void OnCategorySelected(object s, SelectionChangedEventArgs e)
    {
        CommitEditor();
        RefreshSnippets();
    }

    private void OnSnippetSelected(object s, SelectionChangedEventArgs e)
    {
        CommitEditor();
        if (SelectedSnippet is { } sn)
        {
            _editing = sn;
            _editingCategory = SelectedCategory;
            SnippetName.Text = sn.Name; Abbr.Text = sn.Abbr; Body.Text = sn.Body;
            UseVars.IsChecked = sn.UseVariables;
            int mode = Array.IndexOf(OutputModes, sn.OutputMode ?? "");
            OutputMode.SelectedIndex = mode < 0 ? 0 : mode;
            UpdateAbbrConflictHint();
            UpdateUsageStat(sn.Id);
            UpdateSnippetImage();
            EditorPane.IsEnabled = true;
        }
        else { _editing = null; _editingCategory = null; ClearEditor(); }
    }

    private void ClearEditor()
    {
        SnippetName.Text = Abbr.Text = Body.Text = "";
        UseVars.IsChecked = false;
        OutputMode.SelectedIndex = 0;
        AbbrConflict.Visibility = Visibility.Collapsed;
        UsageStat.Text = "";
        SnippetImage.Source = null;
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

    // Keep the snippet list's display name in sync when the name box loses focus.
    private void OnNameLostFocus(object sender, RoutedEventArgs e)
    {
        if (_editing == null) return;
        CommitEditor();
        Snippets.Items.Refresh();
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
        // Bump first so any in-flight load (remove/replace/re-select all call this) is superseded
        // — keying only on the snippet identity let a slow load of a just-removed/replaced image
        // reappear over the correct state.
        int gen = ++_imageLoadGen;
        SnippetImage.Source = null;
        if (_editing is not { IsImage: true } s) return;
        var rel = s.ImagePath;
        System.Threading.Tasks.Task.Run(() =>
        {
            var img = ImageLoader.Load(rel);
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (gen == _imageLoadGen) SnippetImage.Source = img;
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
        var old = _editing.ImagePath;
        _editing.ImagePath = "";
        _editing.UpdatedAt = DateTimeOffset.UtcNow;
        if (_editingCategory != null) _dirty.Add(_editingCategory);
        UpdateSnippetImage();
        AppState.Current.Store.DeleteImage(old);
    }

    private void SetImage(byte[] data, string ext)
    {
        if (_editing == null) return;
        var old = _editing.ImagePath;
        _editing.ImagePath = AppState.Current.Store.SaveImage(data, ext);
        _editing.UpdatedAt = DateTimeOffset.UtcNow;
        if (_editingCategory != null) _dirty.Add(_editingCategory);
        UpdateSnippetImage();
        if (old != _editing.ImagePath) AppState.Current.Store.DeleteImage(old);
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
        CommitEditor();
        var prevColor = c.Color;
        c.Color = (b.Tag as string) ?? "";
        AppState.Current.MarkSelfWrite();
        try { AppState.Current.Store.SaveCategory(c); }
        catch { c.Color = prevColor; AlertSaveFailed(); }   // write didn't happen — revert to match disk
        Categories.Items.Refresh();
    }

    private void OnAddCategory(object s, RoutedEventArgs e)
    {
        CommitEditor();
        var name = AppDialog.Prompt(this, L("Manager.NewCategory"), L("Manager.CategoryName"), UniqueCategoryName());
        if (string.IsNullOrWhiteSpace(name)) return;
        if (_cats.Any(x => x.Name == name))
        {
            AppDialog.Alert(this, L("App.Name"), L("Manager.DuplicateCategory"));
            return;
        }
        PersistDirty();
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
        CommitEditor();
        if (SelectedCategory is not { } c) return;
        var name = AppDialog.Prompt(this, L("Manager.RenameCategory"), L("Manager.CategoryName"), c.Name);
        if (string.IsNullOrWhiteSpace(name) || name == c.Name) return;
        if (_cats.Any(x => x != c && x.Name == name))
        {
            AppDialog.Alert(this, L("App.Name"), L("Manager.DuplicateCategory"));
            return;
        }
        PersistDirty();
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
        SelectCategoryByName(name);
    }

    private void OnDeleteCategory(object s, RoutedEventArgs e)
    {
        if (SelectedCategory is not { } c) return;
        if (!AppDialog.Confirm(this, L("App.Name"),
                string.Format(L("Manager.DeleteCategoryConfirm"), c.Name), L("Manager.Delete"))) return;
        _dirty.Remove(c);   // don't bother persisting a category we're about to delete
        int idx = Categories.SelectedIndex;
        PersistDirty();
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
        if (_cats.Count > 0) Categories.SelectedIndex = Math.Min(idx, _cats.Count - 1);
    }

    private void OnAddSnippet(object s, RoutedEventArgs e)
    {
        CommitEditor();
        if (SelectedCategory is not { } c) return;
        var sn = new Snippet { Name = L("Manager.NewSnippetName") };
        c.Snippets.Add(sn);
        _dirty.Add(c);
        if (FilterBox.Text.Length > 0) FilterBox.Text = "";   // a filter would hide the new row
        RefreshSnippets(sn);
        SnippetName.Focus();
        SnippetName.SelectAll();
    }

    private void OnDuplicateSnippet(object s, RoutedEventArgs e)
    {
        CommitEditor();
        if (SelectedCategory is not { } c || SelectedSnippet is not { } sn) return;
        // Abbr stays empty: two snippets with the same abbreviation would be ambiguous.
        var copy = new Snippet { Name = sn.Name + " (2)", Body = sn.Body, UseVariables = sn.UseVariables, OutputMode = sn.OutputMode };
        if (sn.IsImage)
        {
            // Copy the image file — sharing one file would break when either snippet is deleted.
            try
            {
                var bytes = File.ReadAllBytes(AppState.Current.Store.ResolveImage(sn.ImagePath));
                var ext = Path.GetExtension(sn.ImagePath);
                copy.ImagePath = AppState.Current.Store.SaveImage(bytes, string.IsNullOrEmpty(ext) ? ".png" : ext);
            }
            catch { }
        }
        c.Snippets.Insert(c.Snippets.IndexOf(sn) + 1, copy);
        _dirty.Add(c);
        RefreshSnippets(copy);
        SnippetName.Focus();
        SnippetName.SelectAll();
    }

    private List<Snippet> SelectedSnippets() => Snippets.SelectedItems.Cast<Snippet>().ToList();

    private void OnDeleteSnippet(object s, RoutedEventArgs e) => DeleteSnippets(SelectedSnippets());

    private void DeleteSnippets(List<Snippet> selected)
    {
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
        if (Snippets.Items.Count > 0) Snippets.SelectedIndex = Math.Min(idx, Snippets.Items.Count - 1);
        else ClearEditor();
    }

    // ---------- multi-select context menu (batch move / delete) ----------
    private void OnSnippetsMenuOpening(object sender, ContextMenuEventArgs e)
    {
        var selected = SelectedSnippets();
        if (selected.Count == 0) { e.Handled = true; return; }

        var menu = new ContextMenu { Style = (Style)FindResource("DarkContextMenu") };
        var itemStyle = (Style)FindResource("DarkMenuItem");

        var move = new MenuItem { Header = L("Manager.MoveTo"), Style = itemStyle };
        foreach (var cat in _cats.Where(x => !ReferenceEquals(x, SelectedCategory)))
        {
            var target = cat;
            var mi = new MenuItem { Header = cat.Name, Style = itemStyle };
            mi.Click += (_, _) => MoveSelectedTo(target);
            move.Items.Add(mi);
        }
        move.IsEnabled = move.Items.Count > 0;
        menu.Items.Add(move);

        var del = new MenuItem
        {
            Header = string.Format(L("Manager.DeleteSelected"), selected.Count),
            Style = itemStyle,
        };
        del.Click += (_, _) => DeleteSnippets(SelectedSnippets());
        menu.Items.Add(del);

        Snippets.ContextMenu = menu;
    }

    private void MoveSelectedTo(Category target)
    {
        CommitEditor();
        if (SelectedCategory is not { } from || ReferenceEquals(from, target)) return;
        foreach (var sn in SelectedSnippets())
        {
            if (_editing == sn) { _editing = null; _editingCategory = null; }
            if (from.Snippets.Remove(sn)) target.Snippets.Add(sn);
        }
        _dirty.Add(from);
        _dirty.Add(target);
        RefreshSnippets();
        if (Snippets.Items.Count == 0) ClearEditor();
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
        base.OnPreviewKeyDown(e);
    }

    private void UndoDelete()
    {
        if (_deleted.Count == 0) return;
        var (cat, sn, index) = _deleted.Pop();
        if (!_cats.Contains(cat)) return;   // its category was deleted meanwhile
        cat.Snippets.Insert(Math.Min(index, cat.Snippets.Count), sn);
        // It's back in the library — remove the corresponding trash entry.
        var state = AppState.Current;
        state.MarkSelfWrite();
        var trash = state.Store.LoadTrash();
        if (trash.RemoveAll(t => t.Snippet.Id == sn.Id) > 0)
        {
            state.MarkSelfWrite();
            state.Store.SaveTrash(trash);
        }
        _dirty.Add(cat);
        PersistDirty();   // deletes persist immediately, so their undo must too
        Categories.SelectedItem = cat;
        RefreshSnippets(sn);
    }

    private void OnSave(object s, RoutedEventArgs e)
    {
        CommitEditor();
        // Two snippets with the same abbreviation would expand ambiguously — warn (still saves).
        if (_editing is { } cur && !string.IsNullOrWhiteSpace(cur.Abbr))
        {
            var clash = _cats.SelectMany(x => x.Snippets)
                .FirstOrDefault(x => x.Id != cur.Id && x.Abbr == cur.Abbr);
            if (clash != null)
                AppDialog.Alert(this, L("App.Name"),
                    string.Format(L("Manager.DuplicateAbbr"), cur.Abbr, clash.Name));
        }
        var keep = SelectedSnippet;
        if (SelectedCategory is { } c) _dirty.Add(c);
        bool ok = PersistDirty();
        // We already hold the authoritative model in _cats — rebuild the search/abbr indexes
        // from it in memory instead of re-reading the whole library off disk (that read-back
        // was the main "卡住" on sync drives).
        AppState.Current.ApplyCategories(_cats);
        // Refresh names, then keep the user on the snippet they were editing.
        RefreshSnippets(keep);
        Categories.Items.Refresh();
        if (ok) FlashSaved();
        else AlertSaveFailed();
    }

    private void FlashSaved()
    {
        SavedHint.Opacity = 1;
        SavedHint.BeginAnimation(OpacityProperty,
            new DoubleAnimation(1, 0, new Duration(TimeSpan.FromSeconds(1.4))) { BeginTime = TimeSpan.FromSeconds(0.7) });
    }

    // ---------- drag: reorder + move-to-category ----------
    private Point _dragStart;
    private object? _dragItem;   // Category or Snippet under the mouse at press

    private static T? FindAncestor<T>(DependencyObject? d) where T : DependencyObject
    {
        while (d != null) { if (d is T t) return t; d = VisualTreeHelper.GetParent(d); }
        return null;
    }

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
        if (e.Data.GetData(typeof(Snippet)) is Snippet sn)
        {
            var dest = DropTarget<Category>(e);
            if (dest == null || SelectedCategory is not { } src || ReferenceEquals(dest, src)) return;
            CommitEditor();
            if (_editing == sn) { _editing = null; _editingCategory = null; }
            src.Snippets.Remove(sn);
            dest.Snippets.Add(sn);
            _dirty.Add(src);
            _dirty.Add(dest);
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
        CommitEditor();
        PruneBlankDrafts();   // an abandoned blank "new" shouldn't survive to the next launch
        PersistDirty();
        // Deleted snippets (and their image files) are owned by the trash now — its
        // 30-day purge is what finally deletes image files.
        _deleted.Clear();
        AppState.Current.ApplyCategories(_cats);   // in memory; no disk read-back on close
    }

    private void OnOpenTrash(object s, RoutedEventArgs e)
    {
        CommitEditor();
        PersistDirty();
        new TrashDialog { Owner = this }.ShowDialog();
        Load();   // one disk read: restored snippets should appear immediately
        AppState.Current.ApplyCategories(_cats);
    }
}
