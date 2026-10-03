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

    /// <summary>True below <see cref="NarrowWidth"/>: result rows shed their category chip and trim
    /// the abbreviation keycap so the NAME — the one thing you pick by — keeps room to read. The
    /// row template binds this off the Window (see SnippetRowTemplate in Theme.xaml).</summary>
    public static readonly DependencyProperty NarrowModeProperty =
        DependencyProperty.Register(nameof(NarrowMode), typeof(bool), typeof(SearchPanel), new PropertyMetadata(false));

    public bool NarrowMode
    {
        get => (bool)GetValue(NarrowModeProperty);
        set => SetValue(NarrowModeProperty, value);
    }

    private const double NarrowWidth = 560;

    // Foreground-change hook: the reliable auto-hide. Window.Deactivated alone misses cross-monitor
    // focus changes and never fires if the panel showed without truly activating (both reported), so
    // we also watch the system foreground and hide when it moves to another process's window.
    private IntPtr _fgHook;
    private NativeMethods.WinEventProc? _fgProc;   // kept alive to avoid GC of the delegate

    /// <summary>
    /// True while a summon's foreground handoff is still in flight. A tap-hook summon carries no
    /// WM_HOTKEY foreground grant, so Windows can hand the foreground straight back to the app we
    /// came from a few ms after our SetForegroundWindow — and the auto-hide below read that bounce
    /// as "the user left us" and hid a panel that had just opened. That is the panel flashing on the
    /// first summon after switching apps, and only the first: the flash itself made us the foreground
    /// process, so the NEXT summon was already granted and stuck.
    /// <para>While it's set, foreground changes are not the user's doing and must be ignored;
    /// App.BringToFront clears it when the handoff is over, and re-checks then.</para>
    /// </summary>
    private bool _settling;
    private int _summonId;   // bumped per summon, so a previous summon's guard can't disarm this one

    public SearchPanel()
    {
        InitializeComponent();
        WindowTheming.ApplyFlowDirection(this);   // mirror for a right-to-left UI language (Arabic)
        ApplyMenuFlow();
        // This panel is a process-lifetime singleton (only Show/Hide'd, never rebuilt), so unlike
        // the other windows its ctor-time mirroring would freeze — re-apply on a live language
        // switch so an RTL⇄LTR change flips the layout, not just the (bound) text. Both this
        // window and the service live for the whole process, so the subscription can't leak.
        Core.Localization.LocalizationService.Instance.PropertyChanged +=
            (_, _) => Dispatcher.Invoke(() => { WindowTheming.ApplyFlowDirection(this); ApplyMenuFlow(); });
        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
        _debounce.Tick += (_, _) => { _debounce.Stop(); Refresh(); };
        SizeChanged += (_, _) => { NarrowMode = ActualWidth < NarrowWidth; CapPreview(); };
        // Hook only while the panel is on screen; hiding (or the process exit) tears it down.
        IsVisibleChanged += (_, _) => { if (IsVisible) HookForeground(); else UnhookForeground(); };
    }

    /// <summary>The row menu is a popup — it does not inherit the window's mirrored layout, so an
    /// Arabic panel showed an LTR skeleton with RTL text. Follow the window explicitly.</summary>
    private void ApplyMenuFlow() =>
        ((ContextMenu)Resources["RowMenu"]).FlowDirection = FlowDirection;

    /// <summary>--smoke only: seed one browse row so the shared <c>SnippetRowTemplate</c> actually
    /// inflates when the panel is laid out, letting the CI smoke pass catch a broken row template
    /// (which otherwise only fails at first real render).</summary>
    /// <summary>--shots only: populate from the real library so the design audit sees real rows.</summary>
    internal void ShotsFill(string query) { Query.Text = query; Refresh(); }

    /// <summary>--shots only: render the pinned (连发) state so the glyph/colour switch has a fixture.</summary>
    internal void ShotsPin() => OnTogglePin(PinButton, new RoutedEventArgs());

    /// <summary>--shots only: the first-run empty library screen (no snippets at all) — the real
    /// ShowBrowse takes the same branch when the store is empty, this just skips the store check.</summary>
    internal void ShotsEmpty()
    {
        var loc = LocalizationService.Instance;
        BrowseView.Visibility = Visibility.Collapsed;
        Results.Visibility = Visibility.Collapsed;
        HintCat.Visibility = Visibility.Collapsed;
        ShowEmpty(loc["Search.Empty.Title"], loc["Search.Empty.Sub"]);
        CreateButton.Content = loc["Search.Empty.Action"];
        CreateButton.Visibility = Visibility.Visible;
        CountText.Text = "";
        SetHints(hasRows: false, canCreate: false);   // nothing saved yet: Enter does nothing
    }

    /// <summary>--shots only: browse rows with one selected, and the row context menu it right-clicks up.</summary>
    internal ContextMenu? ShotsRowMenu()
    {
        ShotsFill("");
        if (BrowseList.Items.Count > 0) BrowseList.SelectedIndex = 0;
        return BrowseList.ContextMenu;
    }

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
        if (_settling) return;   // our own summon handoff bouncing, not the user leaving
        HideIfForeignForeground(hwnd);
    }

    /// <summary>Hide unless <paramref name="hwnd"/> is one of OUR windows (the panel, its context
    /// menu, the {变量} dialog) — those share our process. Any window in ANOTHER process holding the
    /// foreground means the user left us. Process-based, so it works no matter which monitor the
    /// new window is on.</summary>
    private void HideIfForeignForeground(IntPtr hwnd)
    {
        if (_pinned || hwnd == IntPtr.Zero) return;
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
        // BringToFront, not a lone StealForeground: without the grant Windows completes — and can
        // undo — the handoff asynchronously, so the assertion has to be verified and repeated. It
        // also uses StealForeground (attach to the current foreground's thread), NOT
        // ForceForeground(self), which no-ops on our own thread and stays lock-refused.
        _settling = true;
        int summon = ++_summonId;
        App.BringToFront(this, () =>
        {
            // Hiding stops the guard, so a re-summon within one tick can leave the OLD guard to
            // fire here — its verdict is about a handoff that no longer matters.
            if (summon != _summonId) return;
            _settling = false;
            // Bounces were suppressed while settling and are never re-delivered, so ask once, now,
            // where the foreground actually ended up: if the user really did leave, hide.
            HideIfForeignForeground(NativeMethods.GetForegroundWindow());
        });
        Activate();
        Query.Focus();
        PlayIntro();
    }

    // Compact auto-height until the user resizes; then a fixed remembered size.
    private void EnterManualMode()
    {
        SizeToContent = SizeToContent.Manual;
        CategoryRail.MaxHeight = BrowseList.MaxHeight = Results.MaxHeight = double.PositiveInfinity;
    }

    private void EnterAutoMode()
    {
        SizeToContent = SizeToContent.Height;
        // BodyRow stays star in both modes — see the XAML comment. A star row measures to its
        // content under SizeToContent, so auto-height still works; it only changes where the
        // slack goes once MinHeight kicks in.
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
                bool remembered = s.PanelX != 0 || s.PanelY != 0;
                // Clamp a remembered position against ITS OWN monitor, not the primary:
                // SystemParameters.WorkArea covers the primary only, so on a desktop that extends
                // to the left of it (negative X) this dragged the panel off the monitor the user
                // parked it on and pinned it to the primary's left edge.
                var wa = remembered
                    ? WindowTheming.MonitorWorkAreaDipAt(s.PanelX, s.PanelY)
                    : SystemParameters.WorkArea;
                if (remembered)
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

    /// <summary>Preview cap derived from the height the window is actually allowed to use — the
    /// MaxHeight PlaceOnActiveMonitor sets from the monitor's work area (and the same value the
    /// --shots harness simulates). The fixed 150 stole a row and a half from the list on a 574-DIP
    /// work area, and one hardcoded number is wrong at one end or the other between a 1366×768
    /// laptop and a 4K display. The 420-DIP whole-hide threshold (PreviewNeedsPanelHeight) still
    /// decides whether the pane shows at all.</summary>
    private void CapPreview()
    {
        double avail = double.IsPositiveInfinity(MaxHeight) ? SystemParameters.WorkArea.Height : MaxHeight;
        PreviewScroll.MaxHeight = Math.Clamp(avail * 0.21, 60, 150);
    }

    private void PlaceTopCenter(Rect wa)
    {
        Left = wa.Left + (wa.Width - Width) / 2;
        Top = wa.Top + wa.Height * 0.16;
    }

    /// <summary>Monitor handle hosting the given window; the null handle (→ primary) if unknown.</summary>
    private static IntPtr MonitorOf(IntPtr hwnd) =>
        hwnd != IntPtr.Zero ? NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST) : IntPtr.Zero;

    /// <summary>Work area (in DIPs) of the monitor hosting the given window; primary if unknown.</summary>
    private static Rect WorkAreaOf(IntPtr hwnd) => WindowTheming.MonitorWorkAreaDip(MonitorOf(hwnd));

    /// <summary>Place the panel just below the target window's text caret. False if the app exposes no caret.</summary>
    private bool TryPlaceAtCaret(double estHeight)
    {
        if (_target == IntPtr.Zero) return false;
        uint tid = NativeMethods.GetWindowThreadProcessId(_target, out _);
        var gti = new NativeMethods.GUITHREADINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.GUITHREADINFO>() };
        if (!NativeMethods.GetGUIThreadInfo(tid, ref gti) || gti.hwndCaret == IntPtr.Zero) return false;

        var pt = new NativeMethods.POINT { X = gti.rcCaret.Left, Y = gti.rcCaret.Bottom };
        if (!NativeMethods.ClientToScreen(gti.hwndCaret, ref pt)) return false;

        // The caret rect is raw Win32 px from the TARGET window's process — convert it with that
        // window's OWN monitor DPI (not a global factor), the same monitor the work-area clamp
        // below already uses, so a caret on a non-primary monitor at a different scale still lands
        // at the right DIP position.
        var monitor = MonitorOf(_target);
        double k = WindowTheming.PxToDipFactor(monitor);
        var wa = WindowTheming.MonitorWorkAreaDip(monitor);
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

    /// <summary>Re-run the current query because the index finished building underneath it. Readers
    /// only join an unfinished build for a bounded time (SearchIndex.ReaderJoinTimeout — the UI
    /// thread carries the keyboard hooks and cannot be blocked longer), so a panel summoned during a
    /// cold start can have shown "no results" from a half-built index. Only the typed case: browsing
    /// reads AppState.Categories, which never went through the index.</summary>
    internal void RefreshAfterIndexBuild()
    {
        if (!string.IsNullOrWhiteSpace(Query.Text)) ShowSearch();
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
            // The first-run screen used to send the user to the tray menu while its own header
            // carried the + button; give the empty state the CTA the no-match state already had
            // Same CreateNew the + button runs — empty query lands the default name.
            CreateButton.Content = loc["Search.Empty.Action"];
            CreateButton.Visibility = Visibility.Visible;
            CountText.Text = "";
            SetHints(hasRows: false, canCreate: false);   // nothing saved yet: Enter does nothing
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
        SetHints(hasRows: BrowseList.Items.Count > 0, canCreate: false);
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

    /// <summary>
    /// Roughly how much of a body the row's own subtitle already shows, in half-width units. The
    /// subtitle renders the first line at 12.5px across ~460 DIP: ~70 half-width characters, or ~35
    /// full-width ones. Measured as WIDTH, not character count — a plain count is whichever script's
    /// answer you picked, and was wrong by 2× for the other, which is how a Latin line that fitted
    /// the subtitle entirely still got a preview pane repeating it verbatim.
    /// Deliberately conservative: overshooting shows a redundant preview, undershooting HIDES text
    /// the user cannot otherwise read.
    /// </summary>
    private const int SubtitleVisibleWidth = 70;

    /// <summary>Below this the panel is too short to spend ~60 DIP on a preview: at the 340 DIP
    /// minimum it left barely two rows of the list, which is the part you actually pick from.</summary>
    private const double PreviewNeedsPanelHeight = 420;

    /// <summary>Does the preview pane show the user anything the row doesn't already?</summary>
    private static bool PreviewAddsAnything(string body) =>
        body.IndexOfAny(new[] { '\r', '\n' }) >= 0
        || Core.SnippetNaming.DisplayWidth(body.Trim()) > SubtitleVisibleWidth;

    private void UpdatePreview()
    {
        // A user who dragged the panel down to its minimum asked for a compact launcher; the list
        // wins the remaining space over a preview of the row that is already on screen.
        if (ActiveList.SelectedItem is not SearchHit hit
            || (SizeToContent == SizeToContent.Manual && ActualHeight > 0 && ActualHeight < PreviewNeedsPanelHeight))
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
        else if (!string.IsNullOrEmpty(sn.Body) && PreviewAddsAnything(sn.Body))
        {
            // The pane shows ~150px; a full multi-hundred-KB body would make WPF lay out
            // every wrapped line and stall selection. Preview only needs the head.
            const int previewMaxChars = 2000;
            PreviewText.Text = Core.SnippetNaming.Ellipsize(sn.Body, previewMaxChars, " …");
            // The body is the user's text — read it in its own direction, not the UI's.
            PreviewText.FlowDirection = Core.BidiText.IsRightToLeft(sn.Body)
                ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
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
        CountText.Text = hits.Count == 0 ? "" : string.Format(LocalizationService.Instance["Search.Count.Browse"], hits.Count);
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
        // The index builds on a background thread (see SearchIndex.Build) and its readers join that
        // build. Joining from HERE would block the UI thread — the same thread the two keyboard
        // hooks are dispatched on — and past 300ms Windows drops those hooks for the session. On a
        // cold start the user can out-run the build, so leave the current view alone; App's
        // BuildCompleted handler calls RefreshAfterIndexBuild the moment there is something to find.
        if (!AppState.Current.Search.IsBuilt) return;

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
        CountText.Text = hits.Count == 0 ? "" : string.Format(loc["Search.Count.Hits"], hits.Count);

        SetHints(hasRows: hits.Count > 0, canCreate: true);   // Enter creates when nothing matched
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

    /// <summary>
    /// Point the footer legend at what the keys actually do right now. With no rows there is nothing
    /// to select and Enter creates rather than sends — advertising "选择 / 发送" over an empty list
    /// promises two things that don't happen.
    /// </summary>
    private void SetHints(bool hasRows, bool canCreate)
    {
        HintNav.Visibility = HintSend.Visibility = hasRows ? Visibility.Visible : Visibility.Collapsed;
        HintCreate.Visibility = !hasRows && canCreate ? Visibility.Visible : Visibility.Collapsed;
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
    /// <summary>
    /// Open a full window from the panel, then dismiss the panel — in that order.
    /// <para>Hiding first left the screen blank for however long the window took to construct
    /// (the first ManagerWindow of a session is the slow one), so the click read as "nothing
    /// happened". It also handed the foreground to whatever sat BEHIND the panel before the new
    /// window existed to claim it, which is how the window ended up buried.</para>
    /// <para>Opening first costs a brief moment where the topmost panel still covers the new
    /// window — which reads as the panel dissolving into it, and is honest feedback that the click
    /// registered.</para>
    /// </summary>
    private T OpenThenDismiss<T>(Func<T> create) where T : Window
    {
        var w = App.ShowSingleton(create);
        Hide();
        App.BringToFront(w);   // Hide() re-shuffles the foreground; re-assert after it, not before
        return w;
    }

    private void EditSelected()
    {
        if (ActiveList.SelectedItem is not SearchHit hit) return;
        OpenThenDismiss(() => new ManagerWindow()).SelectSnippet(hit.Snippet.Id);
    }

    // Panel toolbar shortcuts to the full windows — open the one, then dismiss the launcher.
    private void OnOpenManager(object sender, RoutedEventArgs e) => OpenThenDismiss(() => new ManagerWindow());
    private void OnOpenSettings(object sender, RoutedEventArgs e) => OpenThenDismiss(() => new SettingsWindow());

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

    private static T? FindAncestor<T>(DependencyObject? d) where T : DependencyObject =>
        TreeSearch.FindAncestor<T>(d);

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
        WhenTargetIsForeground(() =>
        {
            if (image != null) PasteEngine.PasteImage(image, autoSend, restoreClipboard: restore);
            else if (text != null) PasteEngine.Paste(text, restore, autoSend, caret);
            if (pinned) ReactivatePinned();
        });
    }

    /// <summary>
    /// Run <paramref name="paste"/> once the target window actually holds the foreground.
    /// SetForegroundWindow only *requests* the handoff — activation is cross-process and lands
    /// asynchronously, so a Ctrl+V sent into that gap sits in the target's message queue and is
    /// consumed at an unpredictable time. That is what lets the clipboard restore beat the paste
    /// (the panel pastes, the app reads late, and by then the user's clipboard is back). The
    /// abbreviation path never sees this: there the target is already active.
    /// </summary>
    private void WhenTargetIsForeground(Action paste, int triesLeft = 12)
    {
        // ponytail: polling beats a EVENT_SYSTEM_FOREGROUND hook here — no unhook bookkeeping, and
        // the budget is bounded (12 × 20ms). Raise it if a heavy app still misses the window.
        if (triesLeft == 0 || NativeMethods.GetForegroundWindow() == _target)
        {
            Dispatcher.BeginInvoke(paste, DispatcherPriority.Background);
            return;
        }
        System.Threading.Tasks.Task.Delay(20).ContinueWith(_ =>
            Dispatcher.Invoke(() => WhenTargetIsForeground(paste, triesLeft - 1)));
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
        // Same guard as the foreground hook: a summon that hasn't landed yet deactivates us
        // spuriously, and hiding on it is the flash.
        if (!_pinned && !_settling) Hide();
    }

    private void OnTogglePin(object sender, RoutedEventArgs e)
    {
        _pinned = !_pinned;
        // Glyph AND colour: the accent tint alone collided with the hover brighten, so pinned vs
        // resting was unreadable at a glance. E718 is the outline pin, E840 the filled one.
        PinButton.Foreground = (Brush)FindResource(_pinned ? "Brush.Accent" : "Brush.TextMuted");
        PinButton.Content = _pinned ? "\uE840" : "\uE718";
    }

    private void OnInputDrag(object sender, MouseButtonEventArgs e)
    {
        // Drag the borderless panel by its input row (but not when clicking into the search box).
        if (e.OriginalSource is TextBox || e.ButtonState != MouseButtonState.Pressed) return;
        try { DragMove(); } catch { /* not draggable right now */ }
    }
}
