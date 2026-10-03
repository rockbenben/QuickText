using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using QuickText.App.Interop;
using QuickText.App.Ui;
using QuickText.Core.Interop;
using QuickText.Core.Localization;

namespace QuickText.App;

public partial class App : Application
{
    private TaskbarIcon _tray = null!;
    private Window _hidden = null!;
    private GlobalHotkey _hotkey = null!;
    private GlobalHotkey? _captureHotkey;
    private KeyboardHook _hook = null!;
    private SearchPanel? _panel;

    // Set only when the update-check balloon is showing; a click on it opens this release page.
    private string? _updateUrl;
    private static readonly System.Net.Http.HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

    /// <summary>True during the --smoke window check, so window placement (WindowTheming) leaves the
    /// deliberately off-screen windows where the check parked them instead of centering them.</summary>
    internal static bool InSmoke { get; private set; }

    /// <summary>Show the single instance of a window type: focus/restore an already-open one instead
    /// of opening a SECOND editor over the same data (two Manager windows racing → the last to close
    /// overwrites the other's edits). Returns the shown window so callers can navigate it.</summary>
    public static T ShowSingleton<T>(Func<T> create) where T : Window
    {
        foreach (Window w in Current.Windows)
            if (w is T open)
            {
                if (open.WindowState == WindowState.Minimized) open.WindowState = WindowState.Normal;
                open.Activate();
                BringToFront(open);
                return open;
            }
        var created = create();
        created.Show();
        BringToFront(created);
        return created;
    }

    /// <summary>How long to keep re-asserting the foreground, and how often. Callers of
    /// <see cref="ShowSingleton"/> are themselves in the middle of giving focus away — the search
    /// panel hides itself around the same moment — and Windows completes that handoff
    /// ASYNCHRONOUSLY, so a single SetForegroundWindow can be undone microseconds later by the
    /// system finishing its own transfer to whatever sat behind the panel. Fire-and-forget lost
    /// that race intermittently, which is why the window kept coming up behind everything.
    /// <para>The window is short on purpose: long enough to outlast the handoff, short enough that
    /// it can never fight a deliberate click on another app a moment later.</para></summary>
    private static readonly TimeSpan ForegroundGuardFor = TimeSpan.FromMilliseconds(900);
    private static readonly TimeSpan ForegroundGuardEvery = TimeSpan.FromMilliseconds(50);

    /// <summary>Consecutive ticks the window must HOLD the foreground before the guard disarms.
    /// Disarming on "we are foreground right now" is what makes fire-and-forget fail: the very
    /// first assertion usually succeeds, and the theft lands a few tens of ms LATER. Requiring the
    /// hold to persist means the guard is still armed when that happens — and dropping it as soon
    /// as the hold is stable means it is long gone before the user could click somewhere else and
    /// have us fight them for it.</summary>
    private const int ForegroundStableTicks = 6;

    /// <summary>Raise a window to the foreground and hold it there against Windows' asynchronous
    /// handoff. Opened from the search panel (a topmost tool window summoned by a hook), a plain
    /// Show/Activate is foreground-locked and the window comes up behind others; and even a
    /// successful SetForegroundWindow gets undone microseconds later when the system finishes
    /// transferring foreground to whatever sat behind the panel. So assert, then VERIFY with
    /// GetForegroundWindow for a short window afterwards, re-asserting whenever we have lost it.</summary>
    /// <param name="onSettled">Called on the UI thread once the guard disarms — the window is
    /// holding the foreground, or we gave up. Until then the foreground is still in flight, so a
    /// caller that reacts to foreground changes (the search panel auto-hides on them) must not
    /// trust what it sees; this is how it learns the handoff is over.</param>
    internal static void BringToFront(Window w, Action? onSettled = null)
    {
        var hwnd = new WindowInteropHelper(w).Handle;
        if (hwnd == IntPtr.Zero) { onSettled?.Invoke(); return; }
        // Who held the foreground when we started, i.e. the window we are about to fight. The
        // last-resort raise below only ever goes above THIS window — see RaiseAboveBlocker.
        var blocker = Interop.NativeMethods.GetForegroundWindow();
        Interop.NativeMethods.StealForeground(hwnd);

        var deadline = DateTime.UtcNow + ForegroundGuardFor;
        int stable = 0;
        var timer = new System.Windows.Threading.DispatcherTimer(
            System.Windows.Threading.DispatcherPriority.Send, w.Dispatcher) { Interval = ForegroundGuardEvery };
        timer.Tick += (_, _) =>
        {
            // A stray guard that outlives its window would keep yanking focus from whatever the
            // user moved on to, so it also stops the moment the window closes or hides.
            var live = new WindowInteropHelper(w).Handle;
            bool gaveUp = DateTime.UtcNow > deadline;
            if (live == IntPtr.Zero || !w.IsVisible || gaveUp)
            {
                timer.Stop();
                // Only on the give-up path. The same branch is also how the guard exits when the
                // window was closed or hidden mid-flight, and raising a window the user just
                // dismissed is at best wasted interop.
                if (gaveUp && stable == 0) RaiseAboveBlocker(w, live, blocker);
                onSettled?.Invoke();
                return;
            }
            if (Interop.NativeMethods.GetForegroundWindow() == live)
            {
                if (++stable >= ForegroundStableTicks) { timer.Stop(); onSettled?.Invoke(); }
                return;
            }
            stable = 0;
            Interop.NativeMethods.StealForeground(live);
        };
        timer.Start();
    }

    /// <summary>Last resort after the guard expired without EVER holding the foreground: Windows
    /// refused the grant for this window. Foreground is not ours to take, but Z-ORDER is — a topmost
    /// flip puts the window in front of the app that kept winning, which is what the user asked for
    /// when they clicked "Settings", and dropping straight back to non-topmost keeps it from
    /// floating over everything afterwards.
    /// <para>NEVER for a <see cref="Window.Topmost"/> window. SetWindowPos writes WS_EX_TOPMOST
    /// directly, behind WPF's back: the Topmost property still reads true afterwards, so it never
    /// changes, WPF never re-applies the style, and the search panel — Topmost="True", one instance
    /// for the whole process — would silently stop being topmost for the rest of the session and
    /// start opening behind full-screen apps. It would buy nothing there anyway: the panel's own
    /// onSettled hides it a moment later when the foreground turns out to be foreign.</para>
    /// <para>Only while the SAME app is still in front. If the foreground has moved on to a third
    /// window, the user switched away deliberately during the guard, and shoving our window over
    /// the one they are now typing into is a jump scare with no click to explain it.</para></summary>
    private static void RaiseAboveBlocker(Window w, IntPtr live, IntPtr blocker)
    {
        if (live == IntPtr.Zero || w.Topmost) return;
        var fg = Interop.NativeMethods.GetForegroundWindow();
        if (fg == live || fg != blocker) return;
        const uint f = Interop.NativeMethods.SWP_NOMOVE | Interop.NativeMethods.SWP_NOSIZE
                     | Interop.NativeMethods.SWP_NOACTIVATE;
        Interop.NativeMethods.SetWindowPos(live, Interop.NativeMethods.HWND_TOPMOST, 0, 0, 0, 0, f);
        Interop.NativeMethods.SetWindowPos(live, Interop.NativeMethods.HWND_NOTOPMOST, 0, 0, 0, 0, f);
    }

    private IntPtr _hotkeyHwnd;
    private System.Threading.Mutex? _instanceMutex;   // held for the app's lifetime

    // Posted by a second launch so the running instance pops the search panel.
    private static readonly uint ShowPanelMsg =
        Interop.NativeMethods.RegisterWindowMessage("QuickText.ShowPanel.9C41");

    /// <summary>Title of the hidden message window, so a second launch can FIND it (see
    /// <see cref="HandOffToRunningInstance"/>). Never rendered — the window is 0-size, off-screen,
    /// chrome-less and out of the taskbar.</summary>
    private const string MessageWindowTitle = "QuickText.MessageWindow.9C41";

    /// <summary>
    /// Did Windows start us at login (as opposed to the user launching the exe)? Both the first-instance
    /// and the hand-off path gate the search panel on this, so a login never pops it.
    /// <para>The flag covers entries WE wrote. It can't cover the rest — an HKLM Run value, a Task
    /// Scheduler logon task, a login script, a hand-made Startup shortcut under any name, or an entry
    /// written by a build older than the flag — all of which launch us with no arguments and would
    /// otherwise pop the panel over the desktop at every single boot, with no setting to stop it.
    /// So fall back to a signal that doesn't care HOW we were started: did we come up together with
    /// the session? Reading someone else's autostart entry (let alone rewriting it) can't answer that
    /// and isn't ours to touch.</para>
    /// </summary>
    private static bool IsAutostartLaunch(StartupEventArgs e) =>
        e.Args.Contains(Interop.Autostart.Flag, StringComparer.OrdinalIgnoreCase) || StartedWithSession();

    /// <summary>
    /// Did this process start alongside the user's shell, i.e. as part of logging in? Compared against
    /// the shell (explorer.exe) of OUR session, since that is what "the session started" means — and
    /// autostart mechanisms fire within seconds of it, in either order.
    /// <para>The one-minute window is a deliberate trade: a slow boot can delay a login launch well past
    /// the shell, and getting that wrong means the panel pops at EVERY boot forever. Getting it wrong the
    /// other way — a user who launches QuickText by hand within a minute of logging in — costs one panel
    /// that doesn't open, on one launch, and they can double-click again.</para>
    /// </summary>
    private static bool StartedWithSession()
    {
        try
        {
            var self = System.Diagnostics.Process.GetCurrentProcess();
            DateTime? shellStart = null;
            foreach (var p in System.Diagnostics.Process.GetProcessesByName("explorer"))
                using (p)
                {
                    // Other sessions' shells are both inaccessible and irrelevant; several explorer.exe
                    // can run in ours (file windows), so the EARLIEST is the shell itself.
                    try
                    {
                        if (p.SessionId == self.SessionId && (shellStart == null || p.StartTime < shellStart))
                            shellStart = p.StartTime;
                    }
                    catch { /* exited between enumeration and read, or access denied */ }
                }
            if (shellStart == null) return false;   // no shell (kiosk/服务器 session): assume manual
            return Math.Abs((self.StartTime - shellStart.Value).TotalSeconds) < 60;
        }
        catch { return false; }   // never let this classification block startup
    }

    /// <summary>
    /// Second launch: tell the instance that already owns the mutex to pop the search panel, then die.
    /// Addressed to its message window BY NAME, not PostMessage(HWND_BROADCAST): a broadcast is
    /// silently dropped before it ever reaches our hidden window (measured — a direct post to the very
    /// same hwnd shows the panel, the broadcast does nothing), which is why double-clicking the exe of
    /// a running instance appeared to do nothing at all. Polls briefly because the winner of the mutex
    /// race creates that window a moment AFTER taking the mutex — without this, launching twice in
    /// quick succession would find no window and drop the request. The wait is short and the poll
    /// fast: this process has no UI, so every millisecond here is just a phantom entry sitting in the
    /// task list, and the running instance creates that window immediately after taking the mutex
    /// (CreateMessageWindow) — well before its slow startup work — so the poll rarely runs twice.
    /// There is deliberately NO fallback: the only other channel is the broadcast that measurably
    /// never arrives, so "trying" it would just be a slower way to give up.
    /// </summary>
    private static void HandOffToRunningInstance()
    {
        for (int i = 0; i < 20; i++)   // ~1s; the window exists within a few ms of the mutex
        {
            var hwnd = Interop.NativeMethods.FindWindow(null, MessageWindowTitle);
            if (hwnd != IntPtr.Zero)
            {
                Interop.NativeMethods.PostMessage(hwnd, ShowPanelMsg, IntPtr.Zero, IntPtr.Zero);
                return;
            }
            System.Threading.Thread.Sleep(50);
        }
    }

    /// <summary>The hidden 0-size window that owns the global-hotkey message pump and, via its title,
    /// is the address a second launch posts ShowPanelMsg to (see <see cref="HandOffToRunningInstance"/>).</summary>
    private void CreateMessageWindow()
    {
        _hidden = new Window { Width = 0, Height = 0, WindowStyle = WindowStyle.None,
            ShowInTaskbar = false, Left = -10000, Top = -10000, Title = MessageWindowTitle };
        _hidden.Show();
        _hidden.Hide();
        _hotkeyHwnd = new WindowInteropHelper(_hidden).EnsureHandle();
        HwndSource.FromHwnd(_hotkeyHwnd)!.AddHook(WndProc);
        // Let the show-panel message through UIPI. Without it, an instance running elevated (a common
        // setup for this app — pasting into an elevated window needs it) silently drops the request
        // from a normal double-click, since the second launch is NOT elevated: "does nothing" again.
        Interop.NativeMethods.ChangeWindowMessageFilterEx(
            _hotkeyHwnd, ShowPanelMsg, Interop.NativeMethods.MSGFLT_ALLOW, IntPtr.Zero);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Decide installed-vs-portable before any path is read: a "QuickText.portable" file
        // next to the exe redirects settings/usage/backups under <exeDir>\Data.
        Core.AppPaths.SetExeDir(System.IO.Path.GetDirectoryName(Environment.ProcessPath) ?? Core.AppPaths.ExeDir);
        // Freeze that decision for the whole session so toggling portable in Settings can't move
        // the running app's paths mid-run (it applies on the next start); then, on the first start
        // after a switch to portable, carry the installed config/favorites into Data\ once. Skip the
        // seed under --smoke: it writes a persistent .seeded marker, and a smoke self-check must not
        // seal that one-time gate before the user's real first launch.
        Core.AppPaths.PinPortableState();
        if (!e.Args.Contains("--smoke")) Core.AppPaths.SeedPortableMachineState();

        // Last-resort net for a tray utility: a stray UI-thread exception (e.g. a sync drive
        // locking a data file mid-write) should surface as a balloon and keep the app running,
        // not silently kill the process (which reads to the user as "卡住 then it vanished").
        DispatcherUnhandledException += (_, ex) =>
        {
            Core.Log.Error("ui thread", ex.Exception);
            try { Balloon(ex.Exception.Message, NotificationIcon.Warning); }
            catch { }
            ex.Handled = true;
        };
        // The other two entry points. Only the handler above can RESCUE anything — `Handled = true`
        // keeps the app running; by the time these fire the process (or the task) is already
        // unwinding and all that is left to do is leave a trace. Without them a background failure
        // — the daily backup, a chained index build, anything on the thread pool — vanishes with
        // nothing written down anywhere, which is exactly the report that cannot be acted on.
        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
            Core.Log.Error("unhandled", ex.ExceptionObject as Exception);
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, ex) =>
        {
            Core.Log.Error("unobserved task", ex.Exception);
            ex.SetObserved();
        };

        bool firstRun = !System.IO.File.Exists(Core.Settings.SettingsStore.DefaultPath);

        var state = AppState.Current;
        state.Settings = state.SettingsStore.Load();
        LocalizationService.Instance.SetCulture(state.Settings.Language);
        // Before any window is created, so the first paint is already in the chosen theme.
        Ui.ThemeService.Apply(state.Settings.Theme);
        // Subscribed BEFORE the first Build (inside ReloadData, below) — the index builds on a
        // background thread, so a fast failure could otherwise land before anyone is listening.
        // BeginInvoke rather than a direct call: the event arrives on a thread-pool thread, and at
        // this point in startup the tray does not exist yet, so the handler has to run later on the
        // UI thread anyway.
        state.Search.BuildCompleted += err => Dispatcher.BeginInvoke(() => OnIndexBuilt(err));

        bool dataFolderUnavailable = false;
        try
        {
            state.SeedStarterLibraryIfEmpty();
            state.MigrateVarsOptInOnce();   // legacy snippets with {…} keep expanding after the opt-in change
            state.ReloadData();
        }
        catch
        {
            // Configured data folder unreachable at launch (unplugged USB / offline share /
            // deleted). Come up empty rather than throwing before the tray, mutex and hotkey
            // exist — that would leave an invisible zombie process, and every relaunch (mutex
            // never acquired) would spawn another. The user can re-point it in Settings.
            dataFolderUnavailable = true;
            state.InitEmpty();
        }

        // Dev/CI smoke: parse every window's XAML (they load lazily in normal runs), then exit.
        if (e.Args.Contains("--smoke")) { RunSmoke(); return; }
        if (e.Args.Contains("--shots")) { RunShots(e.Args); return; }

        // Single instance: a second launch would double-install the keyboard hook and expand
        // every abbreviation twice. Hand off to the running instance and bow out.
        _instanceMutex = new System.Threading.Mutex(true, "QuickText.SingleInstance.9C41", out bool isFirst);
        if (!isFirst)
        {
            // An autostart launch that loses the race (the user's own Startup-folder shortcut on top
            // of our entry, a login script, a second session) must stay silent — otherwise fixing the
            // delivery would newly pop the panel over the desktop at every login, which is precisely
            // what the flag exists to prevent.
            if (!IsAutostartLaunch(e)) HandOffToRunningInstance();
            Shutdown();
            return;
        }

        // FIRST thing after winning the mutex: the hidden window a second launch addresses. Everything
        // below can be slow — the tray icon, watching a data folder that may be an offline share or a
        // sleeping USB disk, scanning it for conflict files — and until this window exists, a second
        // launch has nothing to find and its request is dropped, which reads to the user as the very
        // "double-click does nothing" bug this messaging exists to fix. Posted messages just queue
        // until the dispatcher runs (after OnStartup returns), so nothing is handled half-initialized.
        CreateMessageWindow();

        _tray = (TaskbarIcon)FindResource("Tray");   // icon comes from IconSource (Assets/quicktext.ico)
        // ForceCreate is REQUIRED here, unlike under the old Hardcodet package, which added the
        // shell icon from the constructor. H.NotifyIcon defers that to the Loaded event — and this
        // TaskbarIcon lives in App.xaml's resources, so it is never in a visual tree and never
        // loads. Without this the whole app runs with no tray icon at all: the only UI is a hotkey.
        if (!_tray.IsCreated) _tray.ForceCreate();
        // Clicking the "new version available" balloon opens the release page. _updateUrl is armed
        // only while that balloon is current — the Balloon() helper clears it whenever any other
        // balloon shows — and consumed on click, so a click on an unrelated balloon never opens it.
        _tray.TrayBalloonTipClicked += (_, _) =>
        {
            if (_updateUrl is { } u)
            {
                _updateUrl = null;
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(u) { UseShellExecute = true }); } catch { }
            }
        };

        state.StartWatching();
        if (dataFolderUnavailable)
            Balloon(LocalizationService.Instance["Msg.DataFolderUnavailable"], NotificationIcon.Warning);
        var conflicts = state.Store.FindConflictFiles();
        if (conflicts.Count > 0)
            Balloon(string.Format(LocalizationService.Instance["Msg.ConflictFiles"], conflicts.Count), NotificationIcon.Warning);
        if (firstRun)
            Balloon(string.Format(LocalizationService.Instance["Msg.FirstRunHint"], state.Settings.Hotkey), NotificationIcon.Info);

        ApplyMenu();
        LocalizationService.Instance.PropertyChanged += (_, _) => Dispatcher.Invoke(ApplyMenu);

        RegisterHotkey(_hotkeyHwnd);
        SetupTapHook();

        _hook = new KeyboardHook(state.Abbr, () => state.Settings.AbbrEnabled, state.Settings.TerminatorChars,
            () => state.Settings.RestoreClipboard, state.Settings.AbbrBlacklist,
            id => AppState.Current.RecordUse(id));
        if (state.Settings.AbbrEnabled) _hook.Install();

        // Pre-create the search panel so the first hotkey summon is instant
        // (XAML inflation happens now instead of on first use).
        _panel = new SearchPanel();

        // Off the startup path: purge expired trash (LoadTrash is otherwise only called on
        // user action, so the 30-day cleanup needs this daily nudge), then the daily backup.
        System.Threading.Tasks.Task.Run(() =>
        {
            // Two try blocks, not one: the purge failing must not cost the user that day's backup.
            // And both are logged — the backup zips the whole data folder, so "disk full" and "file
            // locked by the sync client" are ordinary outcomes here, and swallowing them silently
            // meant a user could go weeks with no backups and no way to find out.
            try { state.MarkSelfWrite(); state.Store.LoadTrash(); }
            catch (Exception ex) { Core.Log.Error("trash purge", ex); }
            try { state.AutoBackupIfDue(); }
            catch (Exception ex) { Core.Log.Error("auto backup", ex); }
        });

        // Duplicate abbreviations are silent last-wins in the matcher (case-insensitive, and
        // images expand now too) — tell the user which triggers are shadowed.
        if (state.AbbrConflicts.Count > 0)
            Balloon(string.Format(LocalizationService.Instance["Msg.AbbrConflicts"],
                string.Join("、", state.AbbrConflicts)), NotificationIcon.Warning);

        // Opt-in, off by default (the ONLY network call the app ever makes): notify if GitHub has a
        // newer release. Fire-and-forget so a slow/absent network never delays the tray coming up.
        if (state.Settings.CheckUpdates) CheckForUpdatesAsync(manual: false);

        // A hand-launched exe (double-click) opens search, same as double-clicking the tray or
        // relaunching while we're already running — otherwise the app just "does nothing visible".
        // Boot-time autostart stays silent, via the flag Autostart writes into its entry.
        if (!IsAutostartLaunch(e))
            Dispatcher.BeginInvoke(new Action(() => ShowSearch(toggle: false, captureTarget: false)),
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    /// <summary>
    /// Opt-in update check: query the GitHub Releases API — the single network request QuickText
    /// makes — and, if a newer tag exists, show a balloon whose click opens the release page. A
    /// <paramref name="manual"/> check (the Settings button) also reports the up-to-date and failed
    /// cases; the silent startup check stays quiet unless there's actually something to download.
    /// </summary>
    public async void CheckForUpdatesAsync(bool manual)
    {
        var loc = LocalizationService.Instance;
        try
        {
            using var req = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get,
                "https://api.github.com/repos/rockbenben/QuickText/releases/latest");
            req.Headers.UserAgent.ParseAdd("QuickText-update-check");   // GitHub API rejects a missing UA
            req.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var resp = await _http.SendAsync(req);   // resumes on the UI thread (WPF SyncContext)
            resp.EnsureSuccessStatusCode();
            using var doc = System.Text.Json.JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            string? tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
            string? url = root.TryGetProperty("html_url", out var h) ? h.GetString() : null;
            string current = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
            if (Core.UpdateCheck.IsNewer(tag, current))
            {
                var link = string.IsNullOrWhiteSpace(url) ? "https://github.com/rockbenben/QuickText/releases" : url;
                Balloon(string.Format(loc["Msg.UpdateAvailable"], tag), NotificationIcon.Info, link);   // arms the click-to-download link
            }
            else if (manual)
                Balloon(loc["Msg.UpToDate"], NotificationIcon.Info);
        }
        catch
        {
            if (manual) Balloon(loc["Msg.UpdateCheckFailed"], NotificationIcon.Warning);
        }
    }

    /// <summary>A background index build finished. Two things hang off it.
    /// <para>A FAILURE has to be visible. An index that answers every query with nothing is
    /// indistinguishable from an empty library, except that the Manager still lists every snippet
    /// and abbreviations still expand — "my text is gone from search but fine everywhere else",
    /// with no error to act on. While the build was synchronous this reached OnStartup's catch and
    /// warned; the balloon is that warning, kept.</para>
    /// <para>A SUCCESS may need a re-query. Readers only join an unfinished build for
    /// SearchIndex.ReaderJoinTimeout, so a panel summoned during a cold start can have answered a
    /// keystroke from a half-built index. Refreshing a visible panel here is what turns that into a
    /// brief flicker instead of a wrong "no results" the user has to type through.</para></summary>
    private void OnIndexBuilt(Exception? error)
    {
        if (error != null)
        {
            Balloon(LocalizationService.Instance["Msg.SearchIndexFailed"], NotificationIcon.Warning);
            return;
        }
        if (_panel is { IsVisible: true } p) p.RefreshAfterIndexBuild();
    }

    /// <summary>Every tray balloon goes through here so that showing any balloon OTHER than "update
    /// available" disarms its click-to-download link (only that one passes a <paramref name="url"/>).
    /// Otherwise an ignored update balloon would leave the link live, and the next click on an
    /// unrelated balloon (captured / conflict / up-to-date) would open the browser.</summary>
    private void Balloon(string message, NotificationIcon icon, string? url = null)
    {
        _updateUrl = url;
        _tray?.ShowNotification(LocalizationService.Instance["App.Name"], message, icon);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        AppState.Current.Usage.Flush();   // persist any debounced usage/favorite changes
        // Hand the shell icon back explicitly. It is removed on process exit either way, but a
        // lingering icon that only disappears when the user mouses over it is the classic symptom
        // of skipping this, and ForceCreate above means we own the lifetime now.
        _tray?.Dispose();
        base.OnExit(e);
    }

    private void ApplyMenu()
    {
        var loc = LocalizationService.Instance;
        bool paused = !AppState.Current.Settings.AbbrEnabled;
        var menu = _tray.ContextMenu!;
        // Popups don't inherit the app's mirrored layout (they hang off no window), and a right-to-left
        // menu with the icon column still on the left reads as a broken render — set it explicitly.
        menu.FlowDirection = loc.Culture.TextInfo.IsRightToLeft
            ? System.Windows.FlowDirection.RightToLeft : System.Windows.FlowDirection.LeftToRight;
        ((MenuItem)menu.Items[0]).Header = loc["Tray.OpenSearch"];
        ((MenuItem)menu.Items[1]).Header = loc["Tray.OpenManager"];
        ((MenuItem)menu.Items[2]).Header = loc["Tray.NewFromClipboard"];
        ((MenuItem)menu.Items[3]).Header = loc["Tray.Settings"];
        var pauseItem = (MenuItem)menu.Items[4];
        pauseItem.Header = loc[paused ? "Tray.ResumeAbbr" : "Tray.PauseAbbr"];
        // The icon follows the ACTION: offering "resume" while still showing the pause bars
        // contradicted the label one pixel away. E769 pause, E768 play.
        if (pauseItem.Icon is System.Windows.Controls.TextBlock ico)
            ico.Text = paused ? "\uE768" : "\uE769";
        ((MenuItem)menu.Items[6]).Header = loc["Tray.Exit"];
        _tray.ToolTipText = paused ? loc["App.Name"] + " — " + loc["Tray.PausedTip"] : loc["App.Name"];
    }

    /// <summary>Tray toggle: pause/resume abbreviation expansion (same flag as Settings).</summary>
    private void OnTogglePause(object s, RoutedEventArgs e)
    {
        var state = AppState.Current;
        state.Settings.AbbrEnabled = !state.Settings.AbbrEnabled;
        state.SettingsStore.Save(state.Settings);
        if (state.Settings.AbbrEnabled) _hook.Install();
        else _hook.Uninstall();
        ApplyMenu();
        // A Settings window opened earlier holds a stale checkbox snapshot; saving it later
        // would silently undo this toggle — keep any open one in sync.
        foreach (Window w in Windows)
            if (w is SettingsWindow sw) sw.SyncAbbrEnabled(state.Settings.AbbrEnabled);
    }

    private ModifierTapHook? _tapHook;

    /// <summary>
    /// True when tap-to-summon is the ACTIVE trigger: mode is "tap" AND a valid modifier is set.
    /// If tap mode is chosen but no key is set, this is false so the combo hotkey stays as a
    /// fallback — the user is never left with no way to summon from the keyboard.
    /// </summary>
    private static bool UseTapSummon(Core.Settings.AppSettings s) =>
        Core.Interop.ModifierTapKeys.IsValidTap(s.SummonMode, s.SummonTapKey);

    // Ref-count of open Settings windows. The summon triggers are OFF whenever any Settings
    // window is open, so pressing the current hotkey inside a capture box reaches the box instead
    // of firing the panel. Ref-counted so two Settings windows don't strand each other.
    private int _settingsOpenCount;

    /// <summary>
    /// (Re)arm all summon triggers (combo hotkeys + modifier-tap hook) from the CURRENT settings —
    /// but leave them OFF while any Settings window is open. Idempotent: always tears down first.
    /// </summary>
    private void ArmSummonTriggers()
    {
        _hotkey?.Dispose(); _hotkey = null!;
        _captureHotkey?.Dispose(); _captureHotkey = null;
        _tapHook?.Dispose(); _tapHook = null;
        if (_settingsOpenCount > 0 || _hotkeyHwnd == IntPtr.Zero) return;   // suspended, or not started (--smoke)
        RegisterHotkey(_hotkeyHwnd);
        SetupTapHook();
    }

    /// <summary>A Settings window opened — turn summon triggers off so its capture boxes get the keys.</summary>
    public void SuspendSummonTriggers() { _settingsOpenCount++; ArmSummonTriggers(); }

    /// <summary>A Settings window closed — re-arm from current settings once the LAST one is gone.</summary>
    public void ResumeSummonTriggers() { if (_settingsOpenCount > 0) _settingsOpenCount--; ArmSummonTriggers(); }

    /// <summary>(Re)install the "tap a lone modifier to summon" hook from the current settings.</summary>
    private void SetupTapHook()
    {
        _tapHook?.Dispose();
        _tapHook = null;
        var s = AppState.Current.Settings;
        if (!UseTapSummon(s)) return;   // same rule as the combo-hotkey gate — one predicate
        var vk = Core.Interop.ModifierTapKeys.VkOf(s.SummonTapKey)!.Value;   // non-null: UseTapSummon checked it
        _tapHook = new ModifierTapHook(vk, s.SummonTapDouble,
            () => Dispatcher.BeginInvoke(new Action(() => ShowSearch())));
        _tapHook.Install();
    }

    private void RegisterHotkey(IntPtr hwnd)
    {
        var settings = AppState.Current.Settings;
        try
        {
            // The combo hotkey unless a VALID tap-summon is active (which replaces it). Tap mode
            // without a key falls back here, so there's always a working keyboard summon.
            if (!UseTapSummon(settings) && !string.IsNullOrWhiteSpace(settings.Hotkey))
            {
                var def = HotkeyDefinition.Parse(settings.Hotkey);
                _hotkey = new GlobalHotkey(hwnd, def);
                _hotkey.Pressed += () => ShowSearch();   // keyboard summon toggles
                // TryRegister's message is a developer string (win32 error codes); the user gets
                // the localized one, naming the combo and saying what it costs them — a silently
                // dead summon hotkey otherwise looks like the app itself is broken.
                if (!_hotkey.TryRegister(out _))
                    Balloon(string.Format(LocalizationService.Instance["Msg.HotkeyTaken"], settings.Hotkey),
                        NotificationIcon.Warning);
            }
        }
        catch (FormatException)
        {
            Balloon(string.Format(LocalizationService.Instance["Msg.HotkeyInvalid"], settings.Hotkey),
                NotificationIcon.Warning);
        }

        // Optional second hotkey: save the clipboard as a snippet without any window.
        try
        {
            if (!string.IsNullOrWhiteSpace(settings.CaptureHotkey))
            {
                var def = HotkeyDefinition.Parse(settings.CaptureHotkey);
                _captureHotkey = new GlobalHotkey(hwnd, def, GlobalHotkey.DefaultId + 1);
                _captureHotkey.Pressed += CaptureClipboard;
                if (!_captureHotkey.TryRegister(out _))
                    Balloon(string.Format(LocalizationService.Instance["Msg.HotkeyTaken"], settings.CaptureHotkey),
                        NotificationIcon.Warning);
            }
        }
        catch (FormatException)
        {
            Balloon(string.Format(LocalizationService.Instance["Msg.HotkeyInvalid"], settings.CaptureHotkey),
                NotificationIcon.Warning);
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (_hotkey != null && _hotkey.HandleMessage(msg, wParam)) handled = true;
        else if (_captureHotkey != null && _captureHotkey.HandleMessage(msg, wParam)) handled = true;
        else if (msg == ShowPanelMsg && ShowPanelMsg != 0)
        {
            // A second launch says "the user wants QuickText" — activate and summon the panel.
            // Never toggle here: double-clicking the exe must open search, not close an open panel.
            ShowSearch(toggle: false, captureTarget: false);
            handled = true;
        }
        return IntPtr.Zero;
    }

    /// <summary>
    /// --smoke: construct and lay out every window off-screen so their XAML (parsed lazily in normal
    /// runs) is exercised — this catches XAML parse errors and missing StaticResources, which throw
    /// at template inflation. It does NOT verify data bindings: WPF only trace-logs binding failures,
    /// and telling a genuine one apart from the spurious transients WPF emits during a synthetic
    /// off-screen layout isn't reliable — that's left to real UI use. Writes the verdict to
    /// %TEMP%\quicktext-smoke.txt and exits 0/1; used by CI and dev checks. No windows are shown.
    /// </summary>
    private void RunSmoke()
    {
        InSmoke = true;   // WindowTheming placement skips the off-screen windows this parks
        var report = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "quicktext-smoke.txt");
        try
        {
            // Show off-screen so the visual tree builds — control templates (DarkComboBox,
            // list items) only instantiate on layout, not on construction.
            static void Exercise(Window w)
            {
                w.WindowStartupLocation = WindowStartupLocation.Manual;
                w.Left = -32000; w.Top = -32000;
                w.ShowActivated = false;
                w.Show();
                w.UpdateLayout();
                w.Close();
            }
            var panel = new SearchPanel();
            panel.SmokeFill();       // seed a row so SearchPanel's own SnippetRowTemplate inflates on layout
            Exercise(panel);         // ...and lay the panel out, like the other windows
            Exercise(new ManagerWindow());
            var settings = new SettingsWindow();
            // The blank-data-folder field now carries a placeholder showing the resolved default —
            // if the box is empty and the placeholder isn't up, the user sees a broken-looking hole.
            if (string.IsNullOrEmpty(settings.DataFolder.Text) && settings.DataFolderPlaceholder.Visibility != Visibility.Visible)
                throw new InvalidOperationException("data folder box is empty but its placeholder is not visible.");
            Exercise(settings);
            Exercise(new TrashDialog());
            Exercise(new BodyEditorWindow());
            // Also exercise the CODE path: the parameterless ctor above resolves to plain text and
            // never inflates CodeEditor.xaml, so a missing StaticResource there would reach users.
            // This one line also covers SwapSurface's code branch, HighlightingCatalog.Get,
            // SyntaxTheme.ApplyDark and PlaceholderColorizer end to end.
            Exercise(new BodyEditorWindow("", "", false, 0, 0, "json"));
            // The tray icon is the app's ONLY persistent UI, and it is the one resource that has to
            // be created by hand: it lives in App.xaml's resources, so it never joins a visual tree
            // and never fires Loaded — the event H.NotifyIcon creates the shell icon from. A missing
            // ForceCreate, or an IconSource that stops resolving, leaves an app with no tray icon at
            // all and nothing but the hotkey, which no other check here would notice.
            var tray = (TaskbarIcon)FindResource("Tray");
            try
            {
                tray.ForceCreate();
                if (!tray.IsCreated)
                    throw new InvalidOperationException("Tray icon did not materialize (TaskbarIcon.IsCreated == false).");
            }
            finally { tray.Dispose(); }

            // An embedded .xshd that didn't get embedded is invisible at build time — the app
            // starts fine and only the user who picks that one format ever finds out. CI runs
            // this on every commit, so it's the right place to catch it.
            var missing = Ui.Syntax.HighlightingCatalog.MissingDefinitions();
            if (missing.Count > 0)
                throw new InvalidOperationException(
                    "highlighting definitions missing: " + string.Join(", ", missing));
            // BOTH palettes, not just the active one: the light theme's syntax colours are a second
            // hand-tuned table, and a table nobody checks is a table that rots. Auditing only the
            // running theme would have let a light value ship at 1.2:1 with the build still green.
            foreach (var light in new[] { false, true })
            {
                var unreadable = Ui.Syntax.HighlightingCatalog.UnreadableColors(light);
                if (unreadable.Count > 0)
                    throw new InvalidOperationException(
                        $"syntax colours below 3:1 contrast on the {(light ? "light" : "dark")} editor background: "
                        + string.Join(", ", unreadable));
            }
            // Leave the catalog painted for the theme the app is actually running in — the audit
            // above repainted the shared definitions as a side effect.
            Ui.Syntax.HighlightingCatalog.Get("JSON");
            RequireDisabledState();
            var vd = new VariablesDialog();
            vd.Populate(new[] { new Core.Snippets.Placeholders.VariableSpec("测试", "默认", new[] { "a", "b" }) });
            Exercise(vd);
            System.IO.File.WriteAllText(report, "OK");
            Shutdown(0);
        }
        catch (Exception ex)
        {
            System.IO.File.WriteAllText(report, ex.ToString());
            Shutdown(1);
        }
    }

    /// <summary>
    /// --smoke: a style that redefines Template also drops the stock control's built-in disabled
    /// AND focus looks — the trash dialog's Restore rendered pixel-identical enabled while
    /// disabled, keyboard focus was invisible on everything but the text box, and the settings
    /// abbreviation gate left a whole panel of bright-but-deaf fields. Both rules have teeth now:
    /// every custom Button/TextBox/ComboBox/CheckBox/RadioButton template must dim on IsEnabled
    /// and show something on keyboard focus (a FocusVisualStyle or an IsKeyboardFocused trigger).
    /// Pure text over the loaded styles — no window needed.
    /// </summary>
    private static void RequireDisabledState()
    {
        var kinds = new[] { typeof(System.Windows.Controls.Primitives.ButtonBase), typeof(TextBox),
                            typeof(ComboBox), typeof(CheckBox), typeof(RadioButton) };
        foreach (var dict in Current.Resources.MergedDictionaries)
            foreach (var key in dict.Keys.Cast<object>())
        {
            if (dict[key] is not Style st || st.TargetType == null) continue;
            if (!kinds.Any(k => k.IsAssignableFrom(st.TargetType))) continue;
            var setters = st.Setters.OfType<Setter>().ToList();
            var tpl = setters.FirstOrDefault(s => s.Property == Control.TemplateProperty)?.Value as ControlTemplate;
            if (tpl == null) continue;   // no custom template — the stock disabled/focus looks survive
            var triggers = tpl.Triggers.OfType<Trigger>().ToList();
            if (!triggers.Any(t => t.Property == UIElement.IsEnabledProperty))
                throw new InvalidOperationException(
                    $"'{key}' redefines Template without an IsEnabled trigger — disabled instances would look enabled.");
            // FocusVisualStyle is inherited through BasedOn (Setters holds only the style's own
            // entries), so a derived style that just swaps Template still has the base ring.
            bool focusShown = triggers.Any(t => t.Property == UIElement.IsKeyboardFocusedProperty);
            for (Style? s2 = st; s2 != null && !focusShown; s2 = s2.BasedOn)
                focusShown = s2.Setters.OfType<Setter>().Any(x => x.Property == FrameworkElement.FocusVisualStyleProperty);
            if (!focusShown)
                throw new InvalidOperationException(
                    $"'{key}' redefines Template with no focus affordance — keyboard focus would be invisible.");
        }
    }

    /// <summary>
    /// Dev design check, sibling of <see cref="RunSmoke"/>: render every window to PNG at the
    /// work-area heights real setups produce, in both themes, so layout that only breaks on a short
    /// screen or at a window's own MinWidth is visible without owning that hardware. Off-screen via
    /// RenderTargetBitmap — it never takes focus, and it runs before the single-instance mutex, so
    /// it cannot disturb a running QuickText. Usage: <c>QuickText.exe --shots &lt;dir&gt;</c>.
    /// <para>Renders the CLIENT area only (WPF doesn't draw the native title bar), which is where
    /// all of this app's chrome lives anyway.</para>
    /// </summary>
    private void RunShots(string[] args)
    {
        InSmoke = true;
        string dir = args.SkipWhile(a => a != "--shots").Skip(1).FirstOrDefault()
                     ?? System.IO.Path.GetTempPath();
        System.IO.Directory.CreateDirectory(dir);

        // Work-area height (DIP) = screen height / scale - taskbar. The taskbar scales with the DPI,
        // so in DIP it is a constant: ~40 on Win10, ~48 on Win11, 0 when auto-hidden.
        // Subtracting it is the whole point of this array. A window's height cap comes from the WORK
        // area, not the screen — and two of these three used to be raw screen heights (614 = 768/1.25,
        // 720 = 1080/1.5) while only the third had the taskbar taken off. That made the tightest row,
        // the one that exists to catch overflow, 40 DIP MORE forgiving than any real laptop.
        const double taskbar = 40;
        var waHeights = new[]
        {
            768 / 1.25 - taskbar,    // 1366x768  @125% -> ~574, the row that catches things
            1080 / 1.50 - taskbar,   // 1920x1080 @150% -> ~680
            1080 / 1.00 - taskbar,   // 1920x1080 @100% -> 1040
        };

        void Capture(Window w, string file)
        {
            w.UpdateLayout();
            Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
            w.UpdateLayout();
            int pw = (int)Math.Ceiling(w.ActualWidth), ph = (int)Math.Ceiling(w.ActualHeight);
            if (pw <= 0 || ph <= 0) return;
            var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(
                pw, ph, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            rtb.Render(w);
            var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
            enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
            using var fs = System.IO.File.Create(System.IO.Path.Combine(dir, file + ".png"));
            enc.Save(fs);
        }

        // Menus are the app's other visual surface, and no window shot reaches them: the tray
        // menu, the search row menu and the Manager selection menu are Popups over their own
        // top-level. Open each against an off-screen host and render the menu itself.
        void ShotMenu(ContextMenu? menu, string file)
        {
            if (menu is null) return;
            try { ShotMenuInner(menu, file); }
            catch (Exception ex) { System.IO.File.WriteAllText(System.IO.Path.Combine(dir, file + ".err.txt"), ex.ToString()); }
        }
        void ShotMenuInner(ContextMenu menu, string file)
        {
            var host = new Window
            {
                WindowStyle = WindowStyle.None, Left = -32000, Top = -32000,
                Width = 40, Height = 40, ShowActivated = false, ShowInTaskbar = false,
            };
            host.Show();
            menu.PlacementTarget = host;
            // Warm cycle: a menu whose theme changed while it sat closed resolves its
            // DynamicResources on the NEXT open, not the first — rendering that first open
            // captures the previous palette (measured: light pass rendered dark until a
            // second open). Open, close, then open again for the capture.
            menu.IsOpen = true;
            menu.UpdateLayout();
            Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
            menu.IsOpen = false;
            menu.UpdateLayout();
            menu.IsOpen = true;
            menu.UpdateLayout();
            Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
            menu.UpdateLayout();
            int pw = (int)Math.Ceiling(menu.ActualWidth), ph = (int)Math.Ceiling(menu.ActualHeight);
            if (pw > 0 && ph > 0)
            {
                var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(
                    pw, ph, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                rtb.Render(menu);
                var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                using var fs = System.IO.File.Create(System.IO.Path.Combine(dir, file + ".png"));
                enc.Save(fs);
            }
            menu.IsOpen = false;
            host.Close();
        }

        string activeTheme = Ui.ThemeService.Dark;
        void Shot(string name, Func<Window> make, Action<Window>? tweak = null, bool allHeights = true)
        {
            foreach (var wa in allHeights ? waHeights : new[] { waHeights[^1] })
            {
                // Per window, not per pass: SettingsWindow's theme radio applies the SAVED theme as
                // a side effect of initialising, which would repaint every window built after it.
                Ui.ThemeService.Apply(activeTheme);
                Window w;
                try { w = make(); } catch (Exception ex) { System.IO.File.WriteAllText(System.IO.Path.Combine(dir, name + ".err.txt"), ex.ToString()); return; }
                w.WindowStartupLocation = WindowStartupLocation.Manual;
                w.Left = -32000; w.Top = -32000;
                w.ShowActivated = false;
                w.Show();
                // AFTER Show: PlaceOnActiveMonitor sets MaxHeight from the REAL monitor in
                // SourceInitialized, so a pre-Show cap would be overwritten. This is the same
                // assignment it makes, just with the simulated work area.
                w.MaxHeight = wa;
                try { tweak?.Invoke(w); }
                catch (Exception ex) { System.IO.File.WriteAllText(System.IO.Path.Combine(dir, name + ".tweak.err.txt"), ex.ToString()); }
                Capture(w, $"{name}@{wa:0}");
                w.Close();
            }
        }

        // AppDialog is shown through static helpers that block on ShowDialog(), so the harness has to
        // build it by hand — these mirror those helpers. Kept as named functions used by BOTH passes
        // rather than inline lambdas: the theme pass and the language pass must render the same
        // dialog, or one of them is checking something the app never shows.
        void DressConfirm(Window w)
        {
            var loc = LocalizationService.Instance;
            var d = (AppDialog)w;
            d.MessageText.Text = loc["Trash.EmptyConfirm"];
            d.InputBox.Visibility = Visibility.Collapsed;
            d.OkButton.Style = (Style)d.FindResource("DarkButtonDanger");   // as AppDialog.Confirm does
            d.OkButton.Content = loc["Dialog.OK"];
            d.CancelButton.Content = loc["Dialog.Cancel"];
        }
        // The three-button case: the widest row this fixed-width NoResize window ever has to fit, and
        // the one that overflowed 352 DIP in the longest translations.
        void DressSaveDiscard(Window w)
        {
            var loc = LocalizationService.Instance;
            var d = (AppDialog)w;
            d.MessageText.Text = loc["Manager.UnsavedConfirm"];
            d.InputBox.Visibility = Visibility.Collapsed;
            d.OkButton.Content = loc["Manager.Save"];
            d.DiscardButton.Content = loc["Manager.DontSave"];
            d.DiscardButton.Visibility = Visibility.Visible;
            d.CancelButton.Content = loc["Dialog.Cancel"];
        }
        // The single-button message — the shape every Alert (save failed, import result, portable
        // restart) takes, and the one AppDialog layout no other fixture renders.
        void DressAlert(Window w)
        {
            var loc = LocalizationService.Instance;
            var d = (AppDialog)w;
            d.MessageText.Text = loc["Manager.SaveFailed"];
            d.InputBox.Visibility = Visibility.Collapsed;
            d.CancelButton.Visibility = Visibility.Collapsed;
            d.OkButton.Content = loc["Dialog.OK"];
        }

        // ApplyMenu writes into _tray.ContextMenu — the field the startup path assigns AFTER the
        // --shots branch returns, so the harness has to adopt the resource itself or the menu
        // shots throw an NRE that the explicit-Shutdown app swallows into a silent hang.
        _tray ??= (TaskbarIcon)FindResource("Tray");
        var trayMenu = _tray.ContextMenu;

        foreach (var theme in new[] { Ui.ThemeService.Dark, Ui.ThemeService.Light })
        {
            activeTheme = theme;
            string t = theme == Ui.ThemeService.Light ? "light-" : "dark-";

            Shot(t + "panel-browse", () => new SearchPanel(), w => ((SearchPanel)w).ShotsFill(""));
            Shot(t + "panel-search", () => new SearchPanel(), w => ((SearchPanel)w).ShotsFill("a"), allHeights: false);
            Shot(t + "panel-pinned", () => new SearchPanel(), w => { ((SearchPanel)w).ShotsFill(""); ((SearchPanel)w).ShotsPin(); }, allHeights: false);
            Shot(t + "panel-nomatch", () => new SearchPanel(), w => ((SearchPanel)w).ShotsFill("zzqqxx"), allHeights: false);
            Shot(t + "panel-min", () => new SearchPanel(), w =>
            {
                w.SizeToContent = SizeToContent.Manual; w.Width = w.MinWidth; w.Height = w.MinHeight;
                ((SearchPanel)w).ShotsFill("");
            }, allHeights: false);

            Shot(t + "manager", () => new ManagerWindow());
            Shot(t + "manager-narrow", () => new ManagerWindow(), w => { w.Width = w.MinWidth; }, allHeights: false);
            // Re-apply AFTER Show: the window's theme radio applies the saved theme on init.
            Shot(t + "settings", () => new SettingsWindow(), _ => Ui.ThemeService.Apply(activeTheme));
            Shot(t + "trash", () => new TrashDialog(), allHeights: false);
            Shot(t + "editor-text", () => new BodyEditorWindow("欢迎语", "你好 {姓名}，\n感谢你的来信。\n\n祝好\n{光标}", true, 0, 0, null), allHeights: false);
            Shot(t + "editor-code", () => new BodyEditorWindow("配置", "{\n  \"name\": \"quicktext\",\n  \"version\": 1\n}", false, 0, 0, "json"), allHeights: false);
            Shot(t + "variables", () => new VariablesDialog(), w => ((VariablesDialog)w).Populate(new[]
            {
                new Core.Snippets.Placeholders.VariableSpec("姓名", "张三", Array.Empty<string>()),
                new Core.Snippets.Placeholders.VariableSpec("称呼", "您", new[] { "您", "你" }),
            }), allHeights: false);
            // allHeights on purpose — this is the one view whose height is driven by DATA rather than
            // by layout, so the short work areas are the whole point. One row per {variable}, and they
            // accumulate across three levels of {snippet:x} nesting, so a dozen is reachable; the
            // window is SizeToContent="Height" NoResize, which used to mean OK simply left the screen.
            Shot(t + "variables-many", () => new VariablesDialog(), w => ((VariablesDialog)w).Populate(
                Enumerable.Range(1, 12).Select(i =>
                    new Core.Snippets.Placeholders.VariableSpec($"变量{i}", $"默认值 {i}", Array.Empty<string>())).ToArray()));
            Shot(t + "dialog-confirm", () => new AppDialog(), DressConfirm, allHeights: false);
            Shot(t + "dialog-discard", () => new AppDialog(), DressSaveDiscard, allHeights: false);
            Shot(t + "dialog-alert", () => new AppDialog(), DressAlert, allHeights: false);

            // Second-round state matrix: the screens no window shot reached before —
            // first-run empty library, populated trash (the enabled side of the S1 pair),
            // inline abbreviation conflict, the saved flash, the image section, the hotkey
            // capture transient, and every menu surface.
            Shot(t + "panel-empty", () => new SearchPanel(), w => ((SearchPanel)w).ShotsEmpty(), allHeights: false);
            Shot(t + "trash-full", () => new TrashDialog(), w => ((TrashDialog)w).ShotsPopulate(), allHeights: false);
            Shot(t + "trash-full-narrow", () => new TrashDialog(), w => { ((TrashDialog)w).ShotsPopulate(); w.Width = w.MinWidth; }, allHeights: false);
            Shot(t + "manager-conflict", () => new ManagerWindow(), w => ((ManagerWindow)w).ShotsAbbrConflict(), allHeights: false);
            // The abbr row is a horizontal StackPanel — infinite width, no wrap. At the window's own
            // MinWidth the conflict hint is the error text that gets clipped first.
            Shot(t + "manager-conflict-narrow", () => new ManagerWindow(), w => { w.Width = w.MinWidth; ((ManagerWindow)w).ShotsAbbrConflict(); }, allHeights: false);
            Shot(t + "manager-saved", () => new ManagerWindow(), w => ((ManagerWindow)w).ShotsSaved(), allHeights: false);
            Shot(t + "manager-image", () => new ManagerWindow(), w => ((ManagerWindow)w).ShotsImageSection(), allHeights: false);
            // Re-apply AFTER the fixture too: SettingsWindow's theme radio repaints the saved
            // theme during init (the same side effect the plain settings shot guards against),
            // which otherwise renders the light pass in dark.
            Shot(t + "settings-capture", () => new SettingsWindow(), w => { Ui.ThemeService.Apply(activeTheme); ((SettingsWindow)w).ShotsCapturing(); }, allHeights: false);

            // Menus after settings need the theme re-asserted for the same reason.
            Ui.ThemeService.Apply(activeTheme);
            Shot(t + "manager-focus", () => new ManagerWindow(), w => { w.Focus(); ((ManagerWindow)w).ShotsFocusGhost(); }, allHeights: false);
            Shot(t + "settings-focus", () => new SettingsWindow(), w => { w.Focus(); ((SettingsWindow)w).ShotsFocusSave(); Ui.ThemeService.Apply(activeTheme); }, allHeights: false);
            Shot(t + "settings-chip", () => new SettingsWindow(), w => { w.Focus(); ((SettingsWindow)w).ShotsFocusChip(); Ui.ThemeService.Apply(activeTheme); }, allHeights: false);
            Shot(t + "settings-box", () => new SettingsWindow(), w => { w.Focus(); ((SettingsWindow)w).ShotsFocusBox(); Ui.ThemeService.Apply(activeTheme); }, allHeights: false);
            Ui.ThemeService.Apply(activeTheme);
            ApplyMenu();
            ShotMenu(trayMenu, t + "menu-tray");
            var abbrWas = AppState.Current.Settings.AbbrEnabled;
            AppState.Current.Settings.AbbrEnabled = false;   // in-memory only; --shots never persists settings
            ApplyMenu();
            ShotMenu(trayMenu, t + "menu-tray-paused");
            AppState.Current.Settings.AbbrEnabled = abbrWas;
            ApplyMenu();
            {
                var p = new SearchPanel();
                ShotMenu(p.ShotsRowMenu(), t + "menu-row");
                p.Close();
                var m = new ManagerWindow();
                ShotMenu(m.ShotsSelectionMenu(), t + "menu-manager");
                m.Close();
            }
        }

        // Text-density pass: the longest translations, not just the authoring language. A fixed-width
        // NoResize window (Settings is 860 DIP) has no way to absorb a label that grew 40% in German
        // or Russian, so those are where clipped or wrapped copy shows up first.
        Ui.ThemeService.Apply(Ui.ThemeService.Dark);
        activeTheme = Ui.ThemeService.Dark;
        // ar is RIGHT-TO-LEFT: WindowTheming.ApplyFlowDirection mirrors the whole layout, which is a
        // second layout mode nothing else exercises. th/hi/bn are the tall-glyph scripts — their
        // diacritics stack above and below the baseline and are what overflows a fixed-height row.
        foreach (var lang in new[] { "en", "de", "ru", "ar", "th", "hi", "ja", "fr" })
        {
            LocalizationService.Instance.SetCulture(lang);
            Shot($"{lang}-settings", () => new SettingsWindow(), _ => Ui.ThemeService.Apply(activeTheme), allHeights: false);
            Shot($"{lang}-panel", () => new SearchPanel(), w => ((SearchPanel)w).ShotsFill(""), allHeights: false);
            // Search mode too: the footer count has its own key (Search.Count.Hits) since the
            // browse/hits split, and browse-only language shots could not show it.
            Shot($"{lang}-panel-search", () => new SearchPanel(), w => ((SearchPanel)w).ShotsFill("a"), allHeights: false);
            Shot($"{lang}-panel-nomatch", () => new SearchPanel(), w => ((SearchPanel)w).ShotsFill("zzqqxx"), allHeights: false);
            Shot($"{lang}-trash", () => new TrashDialog(), allHeights: false);
            Shot($"{lang}-manager-narrow", () => new ManagerWindow(), w => { w.Width = w.MinWidth; }, allHeights: false);
            // AppDialog is 400 DIP and NoResize — the narrowest fixed window in the app, so it has
            // the least room to absorb a grown label. It used to be shot in the theme pass only,
            // i.e. never in a language that could overflow it.
            Shot($"{lang}-dialog-confirm", () => new AppDialog(), DressConfirm, allHeights: false);
            Shot($"{lang}-dialog-discard", () => new AppDialog(), DressSaveDiscard, allHeights: false);
            // The tray menu is localized by ApplyMenu — the real app re-runs it via the culture
            // PropertyChanged subscription wired AFTER the --shots branch, so the harness calls it
            // directly. ar proves its RTL mirroring, de the longest item labels.
            ApplyMenu();
            ShotMenu(trayMenu, $"{lang}-menu-tray");
            // Contrast fixture: the row menu is owned by the RTL-mirrored panel, the tray menu by
            // nobody — ar shows which of the two actually mirrors.
            {
                var p = new SearchPanel();
                ShotMenu(p.ShotsRowMenu(), $"{lang}-menu-row");
                p.Close();
            }
            // Longest translations at the panel's narrowest allowed size — where the footer legend
            // and the result count compete for the same strip.
            Shot($"{lang}-panel-min", () => new SearchPanel(), w =>
            {
                w.SizeToContent = SizeToContent.Manual; w.Width = w.MinWidth; w.Height = w.MinHeight;
                ((SearchPanel)w).ShotsFill("");
            }, allHeights: false);
        }

        System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "_done.txt"), "OK");
        // Not Shutdown(0): with menus opened by ShotMenu (fading popups) and an adopted tray
        // resource, Shutdown sometimes leaves the process alive holding bin\QuickText.exe —
        // the next build then fails on a file lock nobody asked for. The harness has no state
        // worth an orderly exit; every failure path already wrote its .err.txt by now.
        System.Environment.Exit(0);
    }

    /// <summary>Apply changed settings live so nothing needs a restart.</summary>
    public void ReapplySettings()
    {
        var state = AppState.Current;

        // Data folder may have changed — reload from it and re-watch. Keep the
        // current data if the new folder is unusable rather than crashing.
        try { state.ReloadData(); state.StartWatching(); }
        catch { /* bad folder: retain existing data */ }

        // Re-arm the summon triggers with the new combos. Stays OFF while a Settings window is
        // still open (this runs from OnSave before the window closes); the close re-arms it.
        ArmSummonTriggers();
        ApplyMenu();   // AbbrEnabled may have changed in Settings — sync the pause item

        // Recreate the abbreviation hook so it picks up terminator-char changes too
        // (they are captured at construction), then match the enabled state.
        _hook?.Dispose();
        _hook = new KeyboardHook(state.Abbr, () => state.Settings.AbbrEnabled, state.Settings.TerminatorChars,
            () => state.Settings.RestoreClipboard, state.Settings.AbbrBlacklist,
            id => AppState.Current.RecordUse(id));
        if (state.Settings.AbbrEnabled) _hook.Install();
    }

    /// <summary>
    /// Summon the search panel. <paramref name="toggle"/> is the launcher convention for the keyboard
    /// triggers — press the hotkey again to dismiss. Every OTHER entry point (tray double-click, tray
    /// menu, a second launch) is an explicit "show me the panel", so it forces the panel visible: for
    /// those, toggling would answer a request to open with a close.
    /// </summary>
    /// <param name="captureTarget">False for a summon caused by LAUNCHING the exe (cold start, or a
    /// second launch handed off to us): the foreground then is the Explorer window the user
    /// double-clicked in, not a place to paste into. See SearchPanel.ShowForCurrentForeground.</param>
    private void ShowSearch(bool toggle = true, bool captureTarget = true)
    {
        if (toggle && _panel is { IsVisible: true }) { _panel.Hide(); return; }
        _panel ??= new SearchPanel();
        _panel.ShowForCurrentForeground(captureTarget);
    }

    private void OnOpenSearch(object s, RoutedEventArgs e) => ShowSearch(toggle: false);
    private void OnOpenManager(object s, RoutedEventArgs e) => ShowSingleton(() => new ManagerWindow());

    /// <summary>Double-clicking the tray icon opens search — the app's primary action, and the same
    /// thing double-clicking the exe of an already-running instance does. The Manager stays one click
    /// away in the tray menu.</summary>
    private void OnTrayDoubleClick(object s, RoutedEventArgs e) => ShowSearch(toggle: false);

    /// <summary>Save the current clipboard text as a new snippet in the first category; null if no text.</summary>
    /// <summary>Build a snippet from the clipboard (name = first line trimmed to a listable length,
    /// body = full text), or null when it holds no usable text. Shared so the silent capture hotkey
    /// and the tray "new from clipboard" derive it identically.</summary>
    private static (string Name, string Body)? ClipboardSnippet()
    {
        string text = "";
        try { if (System.Windows.Clipboard.ContainsText()) text = System.Windows.Clipboard.GetText(); } catch { }
        if (string.IsNullOrWhiteSpace(text)) return null;

        var name = Core.SnippetNaming.FromFirstLine(text);
        if (name.Length == 0) name = LocalizationService.Instance["Manager.NewSnippetName"];
        return (name, text);
    }

    /// <summary>Silent capture (hotkey): no Manager is involved, so save straight to disk.</summary>
    private static Core.Models.Snippet? SaveClipboardSnippet()
    {
        if (ClipboardSnippet() is not { } cs) return null;
        var state = AppState.Current;
        var cats = state.Store.LoadAll();
        var cat = cats.Count > 0 ? cats[0]
            : new Core.Models.Category { Name = LocalizationService.Instance["Manager.Categories"] };
        var sn = new Core.Models.Snippet { Name = cs.Name, Body = cs.Body };
        cat.Snippets.Add(sn);
        state.MarkSelfWrite();
        state.Store.SaveCategory(cat);
        state.ReloadData();
        return sn;
    }

    /// <summary>Tray: capture the clipboard as a new snippet and open the Manager to finish it —
    /// added to the Manager's own model (not written behind it), so a concurrent edit can't drop it.</summary>
    private void OnNewFromClipboard(object s, RoutedEventArgs e)
    {
        if (ClipboardSnippet() is not { } cs) return;
        ShowSingleton(() => new ManagerWindow()).AddSnippet(cs.Name, cs.Body);
    }

    /// <summary>Capture hotkey: save the clipboard silently — a balloon is the only feedback.</summary>
    private void CaptureClipboard()
    {
        var loc = LocalizationService.Instance;
        if (SaveClipboardSnippet() is { } sn)
            Balloon(string.Format(loc["Msg.Captured"], sn.Name), NotificationIcon.Info);
        else
            Balloon(loc["Msg.CaptureEmpty"], NotificationIcon.Warning);
    }
    private void OnSettings(object s, RoutedEventArgs e) => ShowSingleton(() => new SettingsWindow());
    private void OnExit(object s, RoutedEventArgs e)
    {
        _hook?.Dispose();
        _hotkey?.Dispose();
        _captureHotkey?.Dispose();
        _tapHook?.Dispose();
        _tray.Dispose();
        Shutdown();
    }
}
