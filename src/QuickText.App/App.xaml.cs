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

    /// <summary>Reliably raise a window to the foreground. Opened from the search panel (a topmost
    /// tool window summoned by a hook), a plain Show/Activate is foreground-locked and the window can
    /// come up behind others; steal the foreground the same way the panel does for itself.</summary>
    internal static void BringToFront(Window w)
    {
        var hwnd = new WindowInteropHelper(w).Handle;
        if (hwnd != IntPtr.Zero) Interop.NativeMethods.StealForeground(hwnd);
    }
    private IntPtr _hotkeyHwnd;
    private System.Threading.Mutex? _instanceMutex;   // held for the app's lifetime

    // Broadcast by a second launch so the running instance pops the search panel.
    private static readonly uint ShowPanelMsg =
        Interop.NativeMethods.RegisterWindowMessage("QuickText.ShowPanel.9C41");

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
            Interop.NativeMethods.PostMessage(Interop.NativeMethods.HWND_BROADCAST, ShowPanelMsg, IntPtr.Zero, IntPtr.Zero);
            Shutdown();
            return;
        }

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

        // hidden 0-size window to own the global hotkey message pump
        _hidden = new Window { Width = 0, Height = 0, WindowStyle = WindowStyle.None,
            ShowInTaskbar = false, Left = -10000, Top = -10000 };
        _hidden.Show();
        _hidden.Hide();
        _hotkeyHwnd = new WindowInteropHelper(_hidden).EnsureHandle();
        HwndSource.FromHwnd(_hotkeyHwnd)!.AddHook(WndProc);

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
            () => Dispatcher.BeginInvoke(new Action(ShowSearch)));
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
                _hotkey.Pressed += ShowSearch;
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
            ShowSearch();   // a second launch says "the user wants QuickText" — summon the panel
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

    private void ShowSearch()
    {
        // Launcher convention: the hotkey toggles — press again to dismiss.
        if (_panel is { IsVisible: true }) { _panel.Hide(); return; }
        _panel ??= new SearchPanel();
        _panel.ShowForCurrentForeground();
    }

    private void OnOpenSearch(object s, RoutedEventArgs e) => ShowSearch();
    private void OnOpenManager(object s, RoutedEventArgs e) => ShowSingleton(() => new ManagerWindow());
    private void OnTrayDoubleClick(object s, RoutedEventArgs e) => ShowSingleton(() => new ManagerWindow());

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
