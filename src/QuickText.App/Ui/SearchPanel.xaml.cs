using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using QuickText.App;
using QuickText.App.Interop;
using QuickText.Core.Localization;
using QuickText.Core.Models;
using QuickText.Core.Search;
using QuickText.Core.Snippets;

namespace QuickText.App.Ui;

public partial class SearchPanel : Window
{
    private IntPtr _target;
    private readonly DispatcherTimer _debounce;
    private Category? _recents;     // synthetic "最近常用" rail item, if any
    private Category? _favorites;   // synthetic "收藏" rail item, if any
    private bool _pinned;           // keep panel open after sending (连发)

    // Current query, bound by result rows to highlight matches (search mode only).
    public static readonly DependencyProperty HighlightQueryProperty =
        DependencyProperty.Register(nameof(HighlightQuery), typeof(string), typeof(SearchPanel), new PropertyMetadata(""));

    public string HighlightQuery
    {
        get => (string)GetValue(HighlightQueryProperty);
        set => SetValue(HighlightQueryProperty, value);
    }

    // Foreground-change hook: the reliable auto-hide. Window.Deactivated alone misses cross-monitor
    // focus changes and never fires if the panel showed without truly activating (both reported), so
    // we also watch the system foreground and hide when it moves to another process's window.
    private IntPtr _fgHook;
    private NativeMethods.WinEventProc? _fgProc;   // kept alive to avoid GC of the delegate

    public SearchPanel()
    {
        InitializeComponent();
        WindowTheming.ApplyFlowDirection(this);   // mirror for a right-to-left UI language (Arabic)
        // This panel is a process-lifetime singleton (only Show/Hide'd, never rebuilt), so unlike
        // the other windows its ctor-time mirroring would freeze — re-apply on a live language
        // switch so an RTL⇄LTR change flips the layout, not just the (bound) text. Both this
        // window and the service live for the whole process, so the subscription can't leak.
        Core.Localization.LocalizationService.Instance.PropertyChanged +=
            (_, _) => Dispatcher.Invoke(() => WindowTheming.ApplyFlowDirection(this));
        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
        _debounce.Tick += (_, _) => { _debounce.Stop(); Refresh(); };
        // Hook only while the panel is on screen; hiding (or the process exit) tears it down.
        IsVisibleChanged += (_, _) => { if (IsVisible) HookForeground(); else UnhookForeground(); };
    }

    /// <summary>--smoke only: seed one browse row so the shared <c>SnippetRowTemplate</c> actually
    /// inflates when the panel is laid out, letting the CI smoke pass catch a broken row template
    /// (which otherwise only fails at first real render).</summary>
    internal void SmokeFill()
    {
        var s = new Core.Models.Snippet { Name = "smoke", Body = "smoke" };
        BrowseList.ItemsSource = new[] { new SearchHit(s, "", 0) };
    }

    private void HookForeground()
    {
        if (_fgHook != IntPtr.Zero) return;
        _fgProc = OnForegroundChanged;
        _fgHook = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_FOREGROUND, NativeMethods.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, _fgProc, 0, 0, NativeMethods.WINEVENT_OUTOFCONTEXT);
    }

    private void UnhookForeground()
    {
        if (_fgHook != IntPtr.Zero) { NativeMethods.UnhookWinEvent(_fgHook); _fgHook = IntPtr.Zero; }
        _fgProc = null;
    }

    private void OnForegroundChanged(IntPtr hook, uint ev, IntPtr hwnd, int idObj, int idChild, uint thread, uint time)
    {
        if (_pinned || hwnd == IntPtr.Zero) return;
        // Ignore our OWN windows (the panel, its context menu, the {变量} dialog) — those share our
        // process. Any window in ANOTHER process taking the foreground means the user left us: hide.
        // Process-based, so it works no matter which monitor the new window is on.
        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid != 0 && pid != (uint)Environment.ProcessId) Hide();
    }

    /// <param name="captureTarget">False when the panel is opened BY a launch of the exe rather than
    /// from a window the user was working in: there is no meaningful paste destination then, so the
    /// send falls back to the clipboard instead of typing into the launcher's window.</param>
    public void ShowForCurrentForeground(bool captureTarget = true)
    {
        _target = captureTarget ? NativeMethods.GetForegroundWindow() : IntPtr.Zero;
        Query.Text = "";
        Refresh();
        PositionAndSize();
        Show();
        // Grab the foreground reliably even when summoned via the tap hook (which, unlike a
        // RegisterHotKey WM_HOTKEY, gets no foreground grant from Windows). Without this the
        // panel shows but never becomes active, so it never fires Deactivated to auto-hide.
        // StealForeground (attach to the current foreground's thread), NOT ForceForeground(self)
        // which no-ops on our own thread and stays lock-refused.
        NativeMethods.StealForeground(new System.Windows.Interop.WindowInteropHelper(this).Handle);
        Activate();
        Query.Focus();
        PlayIntro();
    }

    // Compact auto-height until the user resizes; then a fixed remembered size.
    private void EnterManualMode()
    {
        SizeToContent = SizeToContent.Manual;
        BodyRow.Height = new GridLength(1, GridUnitType.Star);
        CategoryRail.MaxHeight = BrowseList.MaxHeight = Results.MaxHeight = double.PositiveInfinity;
    }

    private void EnterAutoMode()
    {
        SizeToContent = SizeToContent.Height;
        BodyRow.Height = GridLength.Auto;
        CategoryRail.MaxHeight = BrowseList.MaxHeight = Results.MaxHeight = 404;
    }

    private void PositionAndSize()
    {
        var s = AppState.Current.Settings;

        if (s.PanelW >= MinWidth && s.PanelH >= MinHeight)
        {
            EnterManualMode();
            Width = s.PanelW;
            Height = s.PanelH;
        }
        else
        {
            EnterAutoMode();
            Width = 680;
        }
        // Estimated height for clamping before layout has run (auto mode).
        double estH = s.PanelH >= MinHeight ? s.PanelH : 520;

        switch (s.PanelPlacement)
        {
            case "fixed":
                var wa = SystemParameters.WorkArea;
                if (s.PanelX != 0 || s.PanelY != 0)
                {
                    Left = Math.Max(wa.Left, Math.Min(s.PanelX, wa.Right - 120));
                    Top = Math.Max(wa.Top, Math.Min(s.PanelY, wa.Bottom - 120));
                }
                else PlaceTopCenter(wa);
                break;

            case "caret":
                if (TryPlaceAtCaret(estH)) break;
                PlaceTopCenter(WorkAreaOf(_target));   // no caret info → active window's monitor
                break;

            default:   // "window": the monitor the active window is on
                PlaceTopCenter(WorkAreaOf(_target));
                break;
        }
    }

    private void PlaceTopCenter(Rect wa)
    {
        Left = wa.Left + (wa.Width - Width) / 2;
        Top = wa.Top + wa.Height * 0.16;
    }

    // WPF (system-DPI-aware) device px -> DIP scale factor. Shared so the panel and the dialogs
    // (WindowTheming) convert monitor geometry the same way.
    private static double PxToDip => WindowTheming.SystemPxToDip;

    /// <summary>Work area (in DIPs) of the monitor hosting the given window; primary if unknown.</summary>
    private static Rect WorkAreaOf(IntPtr hwnd) =>
        WindowTheming.MonitorWorkAreaDip(
            hwnd != IntPtr.Zero ? NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST) : IntPtr.Zero);

    /// <summary>Place the panel just below the target window's text caret. False if the app exposes no caret.</summary>
    private bool TryPlaceAtCaret(double estHeight)
    {
        if (_target == IntPtr.Zero) return false;
        uint tid = NativeMethods.GetWindowThreadProcessId(_target, out _);
        var gti = new NativeMethods.GUITHREADINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.GUITHREADINFO>() };
        if (!NativeMethods.GetGUIThreadInfo(tid, ref gti) || gti.hwndCaret == IntPtr.Zero) return false;

        var pt = new NativeMethods.POINT { X = gti.rcCaret.Left, Y = gti.rcCaret.Bottom };
        if (!NativeMethods.ClientToScreen(gti.hwndCaret, ref pt)) return false;

        double k = PxToDip;
        var wa = WorkAreaOf(_target);
        double left = Math.Max(wa.Left, Math.Min(pt.X * k, wa.Right - Width));
        double top = pt.Y * k + 8;
        if (top + estHeight > wa.Bottom)   // no room below → open above the caret
            top = Math.Max(wa.Top, gti.rcCaret.Top * k + (pt.Y - gti.rcCaret.Bottom) * k - estHeight - 8);
        Left = left;
        Top = Math.Max(wa.Top, Math.Min(top, wa.Bottom - 120));
        return true;
    }

    private void SaveBounds()
    {
        if (WindowState != WindowState.Normal) return;
        var s = AppState.Current.Settings;
        bool manual = SizeToContent == SizeToContent.Manual;
        double w = manual ? Width : s.PanelW;
        double h = manual ? Height : s.PanelH;
        // Skip the settings write when nothing actually moved or resized.
        if (s.PanelX == Left && s.PanelY == Top && s.PanelW == w && s.PanelH == h) return;
        s.PanelX = Left;
        s.PanelY = Top;
        s.PanelW = w;
        s.PanelH = h;
        try { AppState.Current.SettingsStore.Save(s); } catch { }
    }

    private void OnResizeDrag(object sender, DragDeltaEventArgs e)
    {
        if (SizeToContent != SizeToContent.Manual)
        {
            Width = ActualWidth;
            Height = ActualHeight;
            EnterManualMode();
        }
        if (FlowDirection == FlowDirection.RightToLeft)
        {
            // RTL mirrors the grip to the bottom-LEFT corner and inverts the horizontal delta, so
            // the panel must resize from its LEFT edge: track the (negated) delta for width AND
            // shift Left by the same amount, keeping the right edge anchored under the grip.
            double newWidth = Math.Max(MinWidth, Width - e.HorizontalChange);
            Left -= newWidth - Width;
            Width = newWidth;
        }
        else
        {
            Width = Math.Max(MinWidth, Width + e.HorizontalChange);
        }
        Height = Math.Max(MinHeight, Height + e.VerticalChange);
    }

    private void PlayIntro()
    {
        var root = (UIElement)Content;
        var tt = new TranslateTransform(0, 8);
        root.RenderTransform = tt;
        root.Opacity = 0;
        root.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(120)));
        tt.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(8, 0, TimeSpan.FromMilliseconds(160))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
    }

    private void OnQueryChanged(object sender, RoutedEventArgs e) { _debounce.Stop(); _debounce.Start(); }

    private bool Browsing => BrowseView.Visibility == Visibility.Visible;

    // Empty query -> browse by category; typed query -> flat ranked search.
    private void Refresh()
    {
        if (string.IsNullOrWhiteSpace(Query.Text)) ShowBrowse();
        else ShowSearch();
    }

    private void ShowBrowse()
    {
        var loc = LocalizationService.Instance;
        HighlightQuery = "";
        var cats = AppState.Current.Categories;
        if (cats.Count == 0 || cats.Sum(c => c.Snippets.Count) == 0)
        {
            BrowseView.Visibility = Visibility.Collapsed;
            Results.Visibility = Visibility.Collapsed;
            HintCat.Visibility = Visibility.Collapsed;
            ShowEmpty(loc["Search.Empty.Title"], loc["Search.Empty.Sub"]);
            CountText.Text = "";
            return;
        }

        EmptyState.Visibility = Visibility.Collapsed;
        Results.Visibility = Visibility.Collapsed;
        BrowseView.Visibility = Visibility.Visible;
        HintCat.Visibility = Visibility.Visible;

        // Prepend virtual categories: 最近常用 (usage) and 收藏 (favorites), when present.
        var recent = AppState.Current.Recents(9);
        _recents = recent.Count > 0 ? new Category { Name = loc["Search.Recents"], Snippets = recent } : null;
        var favs = AppState.Current.Favorites(50);
        _favorites = favs.Count > 0 ? new Category { Name = loc["Search.Favorites"], Snippets = favs } : null;

        var railItems = new List<Category>();
        if (_recents != null) railItems.Add(_recents);
        if (_favorites != null) railItems.Add(_favorites);
        railItems.AddRange(cats);

        CategoryRail.ItemsSource = railItems;
        CategoryRail.SelectedItem = ResolveLastCategory(railItems) ?? railItems[0];
        PopulateBrowseList();
    }

    // Stable keys so the remembered category survives a language switch
    // (虚拟分类的名字会随语言变，真实分类用用户起的名字).
    private Category? ResolveLastCategory(List<Category> rail)
    {
        var key = AppState.Current.LastCategory;
        if (key == "@recents") return _recents;
        if (key == "@favorites") return _favorites;
        return rail.FirstOrDefault(c => c.Name == key);
    }

    private void OnCategoryChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CategoryRail.SelectedItem is Category c)
            AppState.Current.LastCategory =
                ReferenceEquals(c, _recents) ? "@recents"
                : ReferenceEquals(c, _favorites) ? "@favorites"
                : c.Name;
        PopulateBrowseList();
    }

    private void OnSnippetSelectionChanged(object sender, SelectionChangedEventArgs e) => UpdatePreview();

    private void UpdatePreview()
    {
        if (ActiveList.SelectedItem is not SearchHit hit)
        {
            PreviewPane.Visibility = Visibility.Collapsed;
            return;
        }
        var sn = hit.Snippet;
        if (sn.IsImage)
        {
            PreviewImage.Source = LoadImage(sn.ImagePath);
            PreviewImage.Visibility = Visibility.Visible;
            PreviewText.Visibility = Visibility.Collapsed;
            PreviewPane.Visibility = PreviewImage.Source != null ? Visibility.Visible : Visibility.Collapsed;
        }
        else if (!string.IsNullOrEmpty(sn.Body))
        {
            // The pane shows ~150px; a full multi-hundred-KB body would make WPF lay out
            // every wrapped line and stall selection. Preview only needs the head.
            const int previewMaxChars = 2000;
            PreviewText.Text = Core.SnippetNaming.Ellipsize(sn.Body, previewMaxChars, " …");
            PreviewText.Visibility = Visibility.Visible;
            PreviewImage.Visibility = Visibility.Collapsed;
            PreviewPane.Visibility = Visibility.Visible;
        }
        else
        {
            PreviewPane.Visibility = Visibility.Collapsed;
        }
    }

    private static ImageSource? LoadImage(string rel)
    {
        try
        {
            var abs = AppState.Current.ResolveImagePath(rel);
            if (!System.IO.File.Exists(abs)) return null;
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.UriSource = new System.Uri(abs);
            bi.EndInit();
            bi.Freeze();
            return bi;
        }
        catch { return null; }
    }

    private void PopulateBrowseList()
    {
        if (CategoryRail.SelectedItem is not Category cat)
        {
            BrowseList.ItemsSource = null;
            CountText.Text = "";
            return;
        }
        // Virtual categories (recents/favorites) show each snippet's real category as a
        // chip; normal categories keep the user's manual (file) order and hide the chip.
        bool isVirtual = ReferenceEquals(cat, _recents) || ReferenceEquals(cat, _favorites);
        var hits = cat.Snippets.Select(s => new SearchHit(s, isVirtual ? AppState.Current.CategoryOf(s.Id) : "", 0)).ToList();
        BrowseList.ItemsSource = hits;
        BrowseList.SelectedIndex = hits.Count > 0 ? 0 : -1;
        CountText.Text = hits.Count == 0 ? "" : string.Format(LocalizationService.Instance["Search.Count"], hits.Count);
    }

    /// <summary>Split "@分类 关键词" into (category, keywords); no @-prefix → (null, query).</summary>
    private static (string? Category, string Keywords) ParseQuery(string raw)
    {
        var q = raw.TrimStart();
        if (!q.StartsWith('@') || q.Length < 2) return (null, raw);
        int sp = q.IndexOfAny(new[] { ' ', '\t', '　' });
        return sp < 0
            ? (q[1..], "")                       // "@模板" — browse the whole category
            : (q[1..sp], q[(sp + 1)..]);         // "@模板 会议" — search within it
    }

    private void ShowSearch()
    {
        var loc = LocalizationService.Instance;
        BrowseView.Visibility = Visibility.Collapsed;
        HintCat.Visibility = Visibility.Collapsed;

        var (category, keywords) = ParseQuery(Query.Text);
        if (category != null && !AppState.Current.Search.HasCategory(category))
        {
            // Not a category — the user is searching for literal @-text (an email, a handle).
            category = null;
            keywords = Query.Text;
        }
        HighlightQuery = keywords;

        var hits = AppState.Current.Search.Search(keywords, category: category);
        Results.ItemsSource = hits;
        CountText.Text = hits.Count == 0 ? "" : string.Format(loc["Search.Count"], hits.Count);

        if (hits.Count > 0)
        {
            Results.SelectedIndex = 0;
            Results.Visibility = Visibility.Visible;
            EmptyState.Visibility = Visibility.Collapsed;
        }
        else
        {
            Results.Visibility = Visibility.Collapsed;
            ShowEmpty(string.Format(loc["Search.NoMatch"], Query.Text.Trim()), loc["Search.NoMatch.Sub"]);
            // Preview the text that will actually become the body — the stripped keywords, not the
            // raw "@category …" — so the button matches what CreateNew produces.
            CreateButton.Content = string.Format(loc["Search.CreateNew"], keywords.Trim());
            CreateButton.Visibility = Visibility.Visible;
        }
    }

    private void ShowEmpty(string title, string sub)
    {
        EmptyState.Visibility = Visibility.Visible;
        PreviewPane.Visibility = Visibility.Collapsed;
        CreateButton.Visibility = Visibility.Collapsed;
        EmptyText.Text = title;
        EmptySub.Text = sub;
    }

    private void OnCreateFromQuery(object sender, RoutedEventArgs e) => CreateNew();

    private void OnNewSnippet(object sender, RoutedEventArgs e) => CreateNew();

    /// <summary>
    /// Create a snippet and jump to the Manager to finish it. The typed text becomes the snippet's
    /// BODY (you searched for content that isn't saved yet — this saves it), with a listable name
    /// derived from its first line. A "@category …" scope is parsed the same way the search is, so
    /// the "@category" prefix doesn't end up in the body; when it names a real category exactly the
    /// snippet is homed there, and in browse mode it lands in the selected real category.
    /// </summary>
    private void CreateNew()
    {
        var (scoped, keywords) = ParseQuery(Query.Text);
        string text;
        string? catHint;
        // Decide "is this a category directive" with the SAME test the search uses (HasCategory), so
        // the empty-state button preview and the created body can't diverge. Home only when the token
        // is ALSO an exact category name (the Manager homes by exact ordinal match); HasCategory is
        // fuzzy (prefix/substring), so a partial "@Wor" strips the body but falls to current/first.
        if (scoped != null && AppState.Current.Search.HasCategory(scoped))
        {
            text = keywords.Trim();
            var home = AppState.Current.Categories.FirstOrDefault(c => string.Equals(c.Name, scoped, StringComparison.OrdinalIgnoreCase));
            catHint = home?.Name;
        }
        else
        {
            text = Query.Text.Trim();
            // The real category being browsed (if any) becomes the snippet's home; else the Manager
            // picks its current/first. Pass the NAME as a hint — the Manager resolves it against its
            // own model.
            catHint = (Browsing && CategoryRail.SelectedItem is Category sel
                && !ReferenceEquals(sel, _recents) && !ReferenceEquals(sel, _favorites)) ? sel.Name : null;
        }
        Hide();

        // First-line name (surrogate-safe, capped); empty text leaves it blank so the Manager falls
        // back to "New snippet". Shared with the clipboard-capture path so the two can't drift.
        var name = Core.SnippetNaming.FromFirstLine(text);

        // Create the snippet directly in the single Manager's in-memory model — NOT a disk write
        // behind an open editor that its next whole-category save would clobber. Persists with the
        // Manager's other edits on save/close.
        App.ShowSingleton(() => new ManagerWindow()).AddSnippet(name, text, catHint);
    }

    /// <summary>Open the Manager focused on the selected snippet.</summary>
    private void EditSelected()
    {
        if (ActiveList.SelectedItem is not SearchHit hit) return;
        Hide();
        App.ShowSingleton(() => new ManagerWindow()).SelectSnippet(hit.Snippet.Id);
    }

    // Panel toolbar shortcuts to the full windows — dismiss the launcher, then focus/open the one.
    private void OnOpenManager(object sender, RoutedEventArgs e) { Hide(); App.ShowSingleton(() => new ManagerWindow()); }
    private void OnOpenSettings(object sender, RoutedEventArgs e) { Hide(); App.ShowSingleton(() => new SettingsWindow()); }

    private ListBox ActiveList => Browsing ? BrowseList : Results;

    // Handle navigation at the window level so keys work regardless of focus.
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Alt+1..9 → instantly send the Nth visible snippet.
        if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0 && key >= Key.D1 && key <= Key.D9)
        {
            QuickSend(key - Key.D1);
            e.Handled = true;
            base.OnPreviewKeyDown(e);
            return;
        }

        switch (key)
        {
            case Key.Down: Move(ActiveList, 1); e.Handled = true; break;
            case Key.Up: Move(ActiveList, -1); e.Handled = true; break;
            case Key.Left when Browsing: Move(CategoryRail, -1); e.Handled = true; break;
            case Key.Right when Browsing: Move(CategoryRail, 1); e.Handled = true; break;
            case Key.Enter:
                if (!Browsing && Results.Items.Count == 0 && Query.Text.Trim().Length > 0) CreateNew();
                else Output();
                e.Handled = true;
                break;
            case Key.Escape: Hide(); e.Handled = true; break;
            case Key.D when (Keyboard.Modifiers & ModifierKeys.Control) != 0:
                ToggleFavoriteSelected(); e.Handled = true; break;
            case Key.N when (Keyboard.Modifiers & ModifierKeys.Control) != 0:
                CreateNew(); e.Handled = true; break;
            case Key.E when (Keyboard.Modifiers & ModifierKeys.Control) != 0:
                EditSelected(); e.Handled = true; break;
        }
        base.OnPreviewKeyDown(e);
    }

    private void ToggleFavoriteSelected()
    {
        if (ActiveList.SelectedItem is not SearchHit hit) return;
        int idx = ActiveList.SelectedIndex;
        AppState.Current.ToggleFavorite(hit.Snippet.Id);
        // Rebuild rows so the star updates; keep the caret on the same row.
        if (Browsing) PopulateBrowseList(); else ShowSearch();
        if (idx >= 0 && idx < ActiveList.Items.Count) ActiveList.SelectedIndex = idx;
    }

    private void QuickSend(int index)
    {
        if (index < 0 || index >= ActiveList.Items.Count) return;
        ActiveList.SelectedIndex = index;
        Output();
    }

    private void OnResultsDoubleClick(object sender, MouseButtonEventArgs e) => Output();

    // ---------- row context menu ----------
    private void OnRowRightClick(object sender, MouseButtonEventArgs e)
    {
        // Right-click doesn't select in a ListBox by default — select the row under the cursor.
        if (sender is ListBox lb && e.OriginalSource is DependencyObject d
            && FindAncestor<ListBoxItem>(d) is { } item)
            lb.SelectedItem = item.DataContext;
    }

    private void OnCtxCopy(object sender, RoutedEventArgs e)
    {
        if (ActiveList.SelectedItem is not SearchHit hit) return;
        var sn = hit.Snippet;
        if (sn.IsImage)
        {
            if (LoadImage(sn.ImagePath) is BitmapSource b) PasteEngine.CopyImage(b);
        }
        else
        {
            var resolved = BodyResolver.Resolve(sn.Body, sn.UseVariables);
            if (resolved == null) return;   // cancelled — copy nothing, keep the panel open
            PasteEngine.CopyText(resolved.Text);
        }
        AppState.Current.RecordUse(sn.Id);
        if (!_pinned) Hide();
    }

    private void OnCtxFavorite(object sender, RoutedEventArgs e) => ToggleFavoriteSelected();

    private void OnCtxEdit(object sender, RoutedEventArgs e) => EditSelected();

    private void OnListClick(object sender, MouseButtonEventArgs e)
    {
        if (!AppState.Current.Settings.ClickToSend) return;
        if (e.OriginalSource is DependencyObject src && FindAncestor<ListBoxItem>(src) != null)
            Output();
    }

    private static T? FindAncestor<T>(DependencyObject? d) where T : DependencyObject
    {
        while (d != null) { if (d is T t) return t; d = VisualTreeHelper.GetParent(d); }
        return null;
    }

    private static void Move(ListBox list, int delta)
    {
        if (list.Items.Count == 0) return;
        int i = Math.Clamp(list.SelectedIndex + delta, 0, list.Items.Count - 1);
        list.SelectedIndex = i;
        list.ScrollIntoView(list.SelectedItem);
    }

    private void Output()
    {
        if (ActiveList.SelectedItem is not SearchHit hit) return;
        var sn = hit.Snippet;

        if (!_pinned) Hide();

        string? text = null;
        int caret = 0;
        bool hasCursor = false;
        BitmapSource? image = null;
        if (sn.IsImage)
        {
            image = LoadImage(sn.ImagePath) as BitmapSource;
        }
        else
        {
            var resolved = BodyResolver.Resolve(sn.Body, sn.UseVariables);
            if (resolved == null) return;   // user cancelled — send nothing
            text = resolved.Text;
            caret = resolved.CaretFromEnd;
            hasCursor = resolved.HasCursor;
        }

        AppState.Current.RecordUse(sn.Id);
        var settings = AppState.Current.Settings;
        var pinned = _pinned;

        // Per-snippet output override beats the global settings ("" = follow global). No paste target
        // (the panel was opened BY launching the app, so the "previous window" is just whatever the
        // user double-clicked from — typically an Explorer window) degrades to copy: pasting there
        // types into the file list or, worse, into an open rename box.
        var mode = sn.OutputMode ?? "";
        bool copyOnly = mode == "copy" || (mode.Length == 0 && settings.CopyToClipboardOnly)
                        || _target == IntPtr.Zero;

        if (copyOnly)
        {
            // Just put it on the clipboard; the user pastes it themselves.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (image != null) PasteEngine.CopyImage(image);
                else if (text != null) PasteEngine.CopyText(text);
                if (pinned) ReactivatePinned();
            }), DispatcherPriority.Background);
            return;
        }

        NativeMethods.ForceForeground(_target);   // robust handoff — the panel just Hid and may have lost foreground
        var restore = settings.RestoreClipboard;
        // A {光标} token means "keep typing here" — never auto-Enter, even when the marker is
        // trailing (caret 0). PasteEngine can't tell that case apart, so decide it here.
        var autoSend = mode switch { "paste-enter" => true, "paste" => false, _ => settings.AutoSend }
                       && !hasCursor;
        // small delay so focus lands before paste
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (image != null) PasteEngine.PasteImage(image, autoSend, restoreClipboard: restore);
            else if (text != null) PasteEngine.Paste(text, restore, autoSend, caret);
            if (pinned) ReactivatePinned();
        }), DispatcherPriority.Background);
    }

    private void ReactivatePinned() =>
        System.Threading.Tasks.Task.Delay(160).ContinueWith(_ =>
            Dispatcher.Invoke(() =>
            {
                // Pinned (连发) mode keeps the panel as the active input surface for the next pick,
                // so unconditionally reset the search box and pull the foreground back to it. The
                // send handed foreground to the target app (paste path) or the panel kept it
                // (copy-only) — either way StealForeground attaches to whatever holds it now and
                // raises us; ForceForeground(self) no-ops on our own thread and bare Activate is
                // foreground-locked. Grabbing back even if the user just moved elsewhere is the
                // point of pinning — to stop, close/unpin the panel.
                Query.Text = "";
                NativeMethods.StealForeground(new System.Windows.Interop.WindowInteropHelper(this).Handle);
                Activate();
                Query.Focus();
            }));

    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (App.InSmoke) return;   // --smoke exercises the panel off-screen; never persist its bounds
        SaveBounds();
        if (!_pinned) Hide();
    }

    private void OnTogglePin(object sender, RoutedEventArgs e)
    {
        _pinned = !_pinned;
        PinButton.Foreground = (Brush)FindResource(_pinned ? "Brush.Accent" : "Brush.TextMuted");
    }

    private void OnInputDrag(object sender, MouseButtonEventArgs e)
    {
        // Drag the borderless panel by its input row (but not when clicking into the search box).
        if (e.OriginalSource is TextBox || e.ButtonState != MouseButtonState.Pressed) return;
        try { DragMove(); } catch { /* not draggable right now */ }
    }
}
