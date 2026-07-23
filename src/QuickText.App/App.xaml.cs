using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Hardcodet.Wpf.TaskbarNotification;
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
    internal static void BringToFront(Window w)
    {
        var hwnd = new WindowInteropHelper(w).Handle;
        if (hwnd == IntPtr.Zero) return;
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
            if (live == IntPtr.Zero || !w.IsVisible || DateTime.UtcNow > deadline)
            {
                timer.Stop();
                return;
            }
            if (Interop.NativeMethods.GetForegroundWindow() == live)
            {
                if (++stable >= ForegroundStableTicks) timer.Stop();
                return;
            }
            stable = 0;
            Interop.NativeMethods.StealForeground(live);
        };
        timer.Start();
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
            try { Balloon(ex.Exception.Message, BalloonIcon.Warning); }
            catch { }
            ex.Handled = true;
        };

        bool firstRun = !System.IO.File.Exists(Core.Settings.SettingsStore.DefaultPath);

        var state = AppState.Current;
        state.Settings = state.SettingsStore.Load();
        LocalizationService.Instance.SetCulture(state.Settings.Language);
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
            Balloon(LocalizationService.Instance["Msg.DataFolderUnavailable"], BalloonIcon.Warning);
        if (state.Store.FindConflictFiles().Count > 0)
            Balloon(LocalizationService.Instance["Msg.ConflictFiles"], BalloonIcon.Warning);
        if (firstRun)
            Balloon(string.Format(LocalizationService.Instance["Msg.FirstRunHint"], state.Settings.Hotkey), BalloonIcon.Info);

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
            try { state.MarkSelfWrite(); state.Store.LoadTrash(); } catch { }
            state.AutoBackupIfDue();
        });

        // Duplicate abbreviations are silent last-wins in the matcher (case-insensitive, and
        // images expand now too) — tell the user which triggers are shadowed.
        if (state.AbbrConflicts.Count > 0)
            Balloon(string.Format(LocalizationService.Instance["Msg.AbbrConflicts"],
                string.Join("、", state.AbbrConflicts)), BalloonIcon.Warning);

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
                Balloon(string.Format(loc["Msg.UpdateAvailable"], tag), BalloonIcon.Info, link);   // arms the click-to-download link
            }
            else if (manual)
                Balloon(loc["Msg.UpToDate"], BalloonIcon.Info);
        }
        catch
        {
            if (manual) Balloon(loc["Msg.UpdateCheckFailed"], BalloonIcon.Warning);
        }
    }

    /// <summary>Every tray balloon goes through here so that showing any balloon OTHER than "update
    /// available" disarms its click-to-download link (only that one passes a <paramref name="url"/>).
    /// Otherwise an ignored update balloon would leave the link live, and the next click on an
    /// unrelated balloon (captured / conflict / up-to-date) would open the browser.</summary>
    private void Balloon(string message, BalloonIcon icon, string? url = null)
    {
        _updateUrl = url;
        _tray?.ShowBalloonTip(LocalizationService.Instance["App.Name"], message, icon);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        AppState.Current.Usage.Flush();   // persist any debounced usage/favorite changes
        base.OnExit(e);
    }

    private void ApplyMenu()
    {
        var loc = LocalizationService.Instance;
        bool paused = !AppState.Current.Settings.AbbrEnabled;
        var menu = _tray.ContextMenu!;
        ((MenuItem)menu.Items[0]).Header = loc["Tray.OpenSearch"];
        ((MenuItem)menu.Items[1]).Header = loc["Tray.OpenManager"];
        ((MenuItem)menu.Items[2]).Header = loc["Tray.NewFromClipboard"];
        ((MenuItem)menu.Items[3]).Header = loc["Tray.Settings"];
        ((MenuItem)menu.Items[4]).Header = loc[paused ? "Tray.ResumeAbbr" : "Tray.PauseAbbr"];
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
                if (!_hotkey.TryRegister(out var err))
                    Balloon(err, BalloonIcon.Warning);
            }
        }
        catch (FormatException)
        {
            Balloon("Invalid hotkey in settings", BalloonIcon.Warning);
        }

        // Optional second hotkey: save the clipboard as a snippet without any window.
        try
        {
            if (!string.IsNullOrWhiteSpace(settings.CaptureHotkey))
            {
                var def = HotkeyDefinition.Parse(settings.CaptureHotkey);
                _captureHotkey = new GlobalHotkey(hwnd, def, GlobalHotkey.DefaultId + 1);
                _captureHotkey.Pressed += CaptureClipboard;
                if (!_captureHotkey.TryRegister(out var err))
                    Balloon(err, BalloonIcon.Warning);
            }
        }
        catch (FormatException)
        {
            Balloon("Invalid capture hotkey in settings", BalloonIcon.Warning);
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
            Exercise(new SettingsWindow());
            Exercise(new TrashDialog());
            Exercise(new BodyEditorWindow());
            // Also exercise the CODE path: the parameterless ctor above resolves to plain text and
            // never inflates CodeEditor.xaml, so a missing StaticResource there would reach users.
            // This one line also covers SwapSurface's code branch, HighlightingCatalog.Get,
            // SyntaxTheme.ApplyDark and PlaceholderColorizer end to end.
            Exercise(new BodyEditorWindow("", "", false, 0, 0, "json"));
            // An embedded .xshd that didn't get embedded is invisible at build time — the app
            // starts fine and only the user who picks that one format ever finds out. CI runs
            // this on every commit, so it's the right place to catch it.
            var missing = Ui.Syntax.HighlightingCatalog.MissingDefinitions();
            if (missing.Count > 0)
                throw new InvalidOperationException(
                    "highlighting definitions missing: " + string.Join(", ", missing));
            var unreadable = Ui.Syntax.HighlightingCatalog.UnreadableColors();
            if (unreadable.Count > 0)
                throw new InvalidOperationException(
                    "syntax colours below 3:1 contrast on the editor background: " + string.Join(", ", unreadable));
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
            Balloon(string.Format(loc["Msg.Captured"], sn.Name), BalloonIcon.Info);
        else
            Balloon(loc["Msg.CaptureEmpty"], BalloonIcon.Warning);
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
