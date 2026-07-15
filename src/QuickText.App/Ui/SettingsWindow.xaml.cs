using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using QuickText.App;
using QuickText.App.Interop;
using QuickText.Core.Localization;

namespace QuickText.App.Ui;

public partial class SettingsWindow : Window
{
    private sealed record LangItem(string Code, string Name);

    // Single source of truth for the shipped UI languages (culture code + native display name).
    private static readonly LangItem[] Languages =
    {
        new("zh-Hans", "简体中文"), new("en", "English"), new("zh-Hant", "繁體中文"),
        new("ja", "日本語"), new("ko", "한국어"), new("es", "Español"),
        new("pt", "Português"), new("hi", "हिन्दी"), new("id", "Bahasa Indonesia"),
        new("vi", "Tiếng Việt"), new("th", "ไทย"), new("fr", "Français"),
        new("de", "Deutsch"), new("it", "Italiano"), new("ru", "Русский"),
        new("ar", "العربية"), new("tr", "Türkçe"), new("bn", "বাংলা"),
    };

    // Accepted Settings.Language values: the 18 codes plus "" (follow the OS). Derived from the
    // list above so the picker and this guard can't drift apart.
    private static readonly HashSet<string> KnownLanguages =
        Languages.Select(l => l.Code).Append("").ToHashSet();

    // Picker items: follow-system (localized) first, then the languages by native name.
    private static LangItem[] LanguageChoices() =>
        Languages.Prepend(new LangItem("", LocalizationService.Instance["Settings.FollowSystem"])).ToArray();

    private string _hotkey = "";
    private string _captureHotkey = "";
    private string _summonMode = "hotkey";
    private string _summonTapKey = "";
    private bool _capturingTap;
    private string _language = "";
    private Border? _capBox;   // which hotkey box is currently capturing (null = none)
    private bool _clipboardOnly;
    private string _placement = "window";

    public SettingsWindow()
    {
        InitializeComponent();
        WindowTheming.UseDarkChrome(this);
        WindowTheming.ApplyFlowDirection(this);
        WindowTheming.PlaceOnActiveMonitor(this);   // shared with Manager: on the user's monitor, capped, on-screen

        var s = AppState.Current.Settings;
        _hotkey = s.Hotkey;
        HotkeyText.Text = HotkeyDisplay(_hotkey);
        _captureHotkey = s.CaptureHotkey;
        CaptureText.Text = HotkeyDisplay(_captureHotkey);
        _summonTapKey = s.SummonTapKey;
        SummonTapText.Text = TapDisplay(_summonTapKey);
        SummonTapDouble.IsChecked = s.SummonTapDouble;
        _summonMode = s.SummonMode == "tap" ? "tap" : "hotkey";
        foreach (var rb in SummonModePanel.Children.OfType<RadioButton>())
            if ((string?)rb.Tag == _summonMode) rb.IsChecked = true;   // fires OnSummonModeChecked → visibility
        DataFolder.Text = s.DataFolder;
        _language = KnownLanguages.Contains(s.Language) ? s.Language : "";
        LangCombo.ItemsSource = LanguageChoices();
        LangCombo.SelectedValue = _language;   // "" selects "follow system"
        AbbrBlacklist.Text = s.AbbrBlacklist;
        AbbrPrefixBox.Text = s.AbbrPrefix;
        AbbrEnabled.IsChecked = s.AbbrEnabled;
        RestoreClipboard.IsChecked = s.RestoreClipboard;
        AutoSend.IsChecked = s.AutoSend;
        ClickToSend.IsChecked = s.ClickToSend;
        Autostart.IsChecked = QuickText.App.Interop.Autostart.IsEnabled();
        CheckUpdates.IsChecked = s.CheckUpdates;
        PortableMode.IsChecked = Core.AppPaths.IsPortable;   // Click-wired, so this set won't fire the handler
        VersionText.Text = LocalizationService.Instance["App.Name"] + " v" +
            (System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0");

        _clipboardOnly = s.CopyToClipboardOnly;
        foreach (var rb in OutputPanel.Children.OfType<RadioButton>())
            if ((string?)rb.Tag == (_clipboardOnly ? "clip" : "paste")) rb.IsChecked = true;

        _placement = s.PanelPlacement is "caret" or "fixed" ? s.PanelPlacement : "window";
        foreach (var rb in PlacementPanel.Children.OfType<RadioButton>())
            if ((string?)rb.Tag == _placement) rb.IsChecked = true;

        // Suspend the summon triggers while this window is open so pressing the current hotkey
        // (or tap key) reaches the capture boxes instead of firing the panel; the ref-counted
        // resume re-arms them once the last Settings window closes (OnSave re-arms via the same
        // path, held off until this window's close decrements the count). Suspend/resume are
        // strictly paired via _suspended: we resume iff THIS window actually suspended, so a
        // window constructed-but-never-shown (no OnSourceInitialized) can neither strand the
        // count nor spuriously decrement it.
        Closed += (_, _) => { if (_suspended) (Application.Current as App)?.ResumeSummonTriggers(); };
    }

    private bool _suspended;   // this window incremented the summon-trigger ref-count

    protected override void OnSourceInitialized(System.EventArgs e)
    {
        base.OnSourceInitialized(e);
        // Sizing/placement (cap to the work area so the docked Save button stays on-screen; the
        // card area scrolls instead) is handled by WindowTheming.PlaceOnActiveMonitor.
        (Application.Current as App)?.SuspendSummonTriggers();
        _suspended = true;
    }

    private static string HotkeyDisplay(string h) => string.IsNullOrWhiteSpace(h) ? "—" : h;

    /// <summary>Called by the tray pause toggle so an already-open window can't save a stale value back.</summary>
    public void SyncAbbrEnabled(bool on) => AbbrEnabled.IsChecked = on;

    private void OnOutputChecked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb) _clipboardOnly = (string?)rb.Tag == "clip";
    }

    private void OnPlacementChecked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb) _placement = (string?)rb.Tag ?? "window";
    }

    private void OnSummonModeChecked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb) { _summonMode = (string?)rb.Tag ?? "hotkey"; ApplySummonModeVisibility(); }
    }

    // Show only the chosen mode's config so it's unambiguous which one is active.
    private void ApplySummonModeVisibility()
    {
        bool tap = _summonMode == "tap";
        HotkeyModePanel.Visibility = tap ? Visibility.Collapsed : Visibility.Visible;
        TapModePanel.Visibility = tap ? Visibility.Visible : Visibility.Collapsed;
    }

    // ---------- hotkey capture (shared by the summon and capture boxes) ----------
    private (TextBlock Text, Func<string> Get, Action<string> Set) Target(Border box) =>
        box == HotkeyBox
            ? (HotkeyText, () => _hotkey, v => _hotkey = v)
            : (CaptureText, () => _captureHotkey, v => _captureHotkey = v);

    private void OnHotkeyBoxClick(object sender, MouseButtonEventArgs e)
    {
        BeginCapture(HotkeyBox);
        e.Handled = true;
    }

    private void OnCaptureBoxClick(object sender, MouseButtonEventArgs e)
    {
        BeginCapture(CaptureBox);
        e.Handled = true;
    }

    private void BeginCapture(Border box)
    {
        _capBox = box;
        Keyboard.Focus(box);
        Target(box).Text.Text = LocalizationService.Instance["Settings.HotkeyPrompt"];
        box.BorderBrush = (Brush)FindResource("Brush.Accent");
    }

    private void EndCapture()
    {
        if (_capBox is not { } box) return;
        var t = Target(box);
        t.Text.Text = HotkeyDisplay(t.Get());
        box.BorderBrush = (Brush)FindResource("Brush.InputBorder");
        _capBox = null;
    }

    private void OnHotkeyLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (_capBox != null && ReferenceEquals(sender, _capBox)) EndCapture();
    }

    private void OnHotkeyKeyDown(object sender, KeyEventArgs e)
    {
        if (_capBox is not { } box || !ReferenceEquals(sender, box)) return;
        e.Handled = true;
        var t = Target(box);

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape) { EndCapture(); return; }
        if (key is Key.Delete or Key.Back) { t.Set(""); EndCapture(); return; }   // clear = disabled
        if (IsModifier(key)) return;                 // still holding modifiers — wait for the real key

        var token = KeyToken(key);
        var mods = Keyboard.Modifiers;
        // Function keys (F1–F24) are safe on their own — they don't type — so allow them with no
        // modifier. Every other key would hijack normal typing globally, so it still needs one.
        bool bareKeyOk = key is >= Key.F1 and <= Key.F24;
        if (token == null || (mods == ModifierKeys.None && !bareKeyOk))
        {
            // not a usable combo yet; keep prompting (the hint states the rule)
            t.Text.Text = LocalizationService.Instance["Settings.HotkeyPrompt"];
            return;
        }

        var combo = "";
        if (mods.HasFlag(ModifierKeys.Control)) combo += "Ctrl+";
        if (mods.HasFlag(ModifierKeys.Shift)) combo += "Shift+";
        if (mods.HasFlag(ModifierKeys.Alt)) combo += "Alt+";
        if (mods.HasFlag(ModifierKeys.Windows)) combo += "Win+";
        t.Set(combo + token);
        EndCapture();
    }

    // ---------- summon-by-tap capture (a LONE modifier, unlike the combo boxes above) ----------
    private void OnSummonTapClick(object sender, MouseButtonEventArgs e)
    {
        _capturingTap = true;
        Keyboard.Focus(SummonTapBox);
        SummonTapText.Text = LocalizationService.Instance["Settings.HotkeyPrompt"];
        SummonTapBox.BorderBrush = (Brush)FindResource("Brush.Accent");
        e.Handled = true;
    }

    private void OnSummonTapKeyDown(object sender, KeyEventArgs e)
    {
        if (!_capturingTap) return;
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape) { EndTapCapture(); return; }
        if (key is Key.Delete or Key.Back) { _summonTapKey = ""; EndTapCapture(); return; }   // clear = off
        if (ModifierName(key) is not { } name)
        {
            SummonTapText.Text = LocalizationService.Instance["Settings.HotkeyPrompt"];   // wants a modifier
            return;
        }
        _summonTapKey = name;
        EndTapCapture();
    }

    private void OnSummonTapLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (_capturingTap) EndTapCapture();
    }

    private void EndTapCapture()
    {
        _capturingTap = false;
        SummonTapText.Text = TapDisplay(_summonTapKey);
        SummonTapBox.BorderBrush = (Brush)FindResource("Brush.InputBorder");
    }

    // Only left/right Ctrl/Shift — no OS side effect when tapped alone (Alt activates the menu
    // bar, Win opens Start, so both are excluded).
    private static string? ModifierName(Key k) => k switch
    {
        Key.LeftCtrl => "LCtrl", Key.RightCtrl => "RCtrl",
        Key.LeftShift => "LShift", Key.RightShift => "RShift",
        _ => null,
    };

    private static string TapDisplay(string name) => name switch
    {
        "LCtrl" => "L-Ctrl", "RCtrl" => "R-Ctrl",
        "LShift" => "L-Shift", "RShift" => "R-Shift",
        _ => "—",
    };

    private static bool IsModifier(Key k) => k is Key.LeftCtrl or Key.RightCtrl
        or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt
        or Key.LWin or Key.RWin or Key.System;

    private static string? KeyToken(Key k)
    {
        if (k >= Key.A && k <= Key.Z) return ((char)('A' + (k - Key.A))).ToString();
        if (k >= Key.D0 && k <= Key.D9) return ((char)('0' + (k - Key.D0))).ToString();
        if (k >= Key.NumPad0 && k <= Key.NumPad9) return ((char)('0' + (k - Key.NumPad0))).ToString();
        if (k >= Key.F1 && k <= Key.F24) return "F" + (k - Key.F1 + 1);
        return k switch
        {
            Key.Space => "Space",
            Key.Oem3 => "`",
            Key.Enter => "Enter",
            Key.Tab => "Tab",
            _ => null,
        };
    }

    private void OnBrowse(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog();
        if (dlg.ShowDialog() == true) DataFolder.Text = dlg.FolderName;
    }

    // Toggle portable mode immediately (create/remove the marker beside the exe) rather than on
    // Save — the marker is a filesystem side effect, not a saved field, and it only takes hold on
    // the next startup. Click fires on user interaction only, so the ctor's IsChecked set is quiet.
    private void OnPortableToggle(object sender, RoutedEventArgs e)
    {
        var loc = LocalizationService.Instance;
        bool enable = PortableMode.IsChecked == true;
        if (!Core.AppPaths.SetPortable(enable))
        {
            PortableMode.IsChecked = !enable;   // exe folder not writable — undo the visual toggle
            AppDialog.Alert(this, loc["App.Name"], loc["Settings.PortableFailed"]);
            return;
        }
        AppDialog.Alert(this, loc["App.Name"], loc["Settings.PortableRestart"]);
    }

    // Check GitHub for a newer release right now (explicit user action, so it runs regardless of the
    // saved opt-in flag). The App shows the result as a tray balloon: newer → click to download,
    // otherwise "up to date" / "check failed".
    private void OnCheckUpdatesNow(object sender, RoutedEventArgs e)
        => (Application.Current as App)?.CheckForUpdatesAsync(manual: true);

    private void OnOpenRepo(object sender, RoutedEventArgs e)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/rockbenben/QuickText") { UseShellExecute = true }); }
        catch { }
    }

    private void OnOpenFolder(object sender, RoutedEventArgs e)
    {
        try
        {
            // Prefer the folder typed in the box (if it exists), else the active one.
            var typed = DataFolder.Text.Trim();
            var folder = typed.Length > 0 && System.IO.Directory.Exists(typed)
                ? typed
                : AppState.Current.ResolveDataFolder();
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{folder}\"")
            { UseShellExecute = true });
        }
        catch { }
    }

    private void OnOpenBackups(object sender, RoutedEventArgs e)
    {
        try
        {
            System.IO.Directory.CreateDirectory(AppState.BackupDir);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{AppState.BackupDir}\"")
            { UseShellExecute = true });
        }
        catch { }
    }

    private void OnExportBackup(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Zip (*.zip)|*.zip",
            FileName = $"QuickText-backup-{DateTime.Now:yyyyMMdd-HHmm}.zip",
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var folder = AppState.Current.ResolveDataFolder();
            if (System.IO.File.Exists(dlg.FileName)) System.IO.File.Delete(dlg.FileName);
            System.IO.Compression.ZipFile.CreateFromDirectory(folder, dlg.FileName);
            AppDialog.Alert(this, LocalizationService.Instance["App.Name"], LocalizationService.Instance["Msg.Exported"]);
        }
        catch (Exception ex)
        {
            AppDialog.Alert(this, LocalizationService.Instance["App.Name"], ex.Message);
        }
    }

    private void OnImportBackup(object sender, RoutedEventArgs e)
    {
        var loc = LocalizationService.Instance;
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Zip (*.zip)|*.zip" };
        if (dlg.ShowDialog() != true) return;

        // Extract to a temp dir first so a bad zip never touches the live library.
        var tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "QuickText-import-" + Guid.NewGuid().ToString("N"));
        try
        {
            System.IO.Compression.ZipFile.ExtractToDirectory(dlg.FileName, tmp);

            // A QuickText backup is the data folder zipped: index.json must exist and parse.
            var idxPath = System.IO.Path.Combine(tmp, "index.json");
            var idx = System.IO.File.Exists(idxPath)
                ? System.Text.Json.JsonSerializer.Deserialize<QuickText.Core.Models.LibraryIndex>(
                    System.IO.File.ReadAllText(idxPath), QuickText.Core.Persistence.JsonConfig.Read)
                : null;
            if (idx?.Categories == null)
            {
                AppDialog.Alert(this, loc["App.Name"], loc["Msg.ImportInvalid"]);
                return;
            }

            if (!AppDialog.Confirm(this, loc["App.Name"], loc["Msg.ImportConfirm"],
                    loc["Settings.ImportBackup"])) return;

            var dest = AppState.Current.ResolveDataFolder();
            AppState.Current.MarkSelfWrite();
            CopyTree(tmp, dest);
            AppState.Current.MarkSelfWrite();   // extend suppression past the last copied file
            AppState.Current.ReloadData();
            AppDialog.Alert(this, loc["App.Name"], loc["Msg.Imported"]);
        }
        catch (Exception ex)
        {
            AppDialog.Alert(this, loc["App.Name"], ex.Message);
        }
        finally
        {
            try { System.IO.Directory.Delete(tmp, recursive: true); } catch { }
        }
    }

    private static void CopyTree(string from, string to)
    {
        System.IO.Directory.CreateDirectory(to);
        foreach (var file in System.IO.Directory.GetFiles(from))
            System.IO.File.Copy(file, System.IO.Path.Combine(to, System.IO.Path.GetFileName(file)), overwrite: true);
        foreach (var dir in System.IO.Directory.GetDirectories(from))
            CopyTree(dir, System.IO.Path.Combine(to, System.IO.Path.GetFileName(dir)));
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var s = AppState.Current.Settings;
        // "tap" mode needs a VALID modifier to work; if the captured key isn't one recognized
        // (empty, an old-build name, a hand-edited value), save as "hotkey" so the mode shown next
        // time matches the trigger that actually fires. Same predicate the runtime arming uses, so
        // display and trigger can't disagree.
        s.SummonMode = Core.Interop.ModifierTapKeys.IsValidTap(_summonMode, _summonTapKey) ? "tap" : "hotkey";
        s.Hotkey = _hotkey.Trim();
        s.CaptureHotkey = _captureHotkey.Trim();
        s.SummonTapKey = _summonTapKey;
        s.SummonTapDouble = SummonTapDouble.IsChecked == true;
        s.DataFolder = DataFolder.Text.Trim();
        s.Language = (LangCombo.SelectedValue as string) ?? "";
        s.AbbrBlacklist = AbbrBlacklist.Text.Trim();
        s.AbbrPrefix = AbbrPrefixBox.Text.Trim();
        s.AbbrEnabled = AbbrEnabled.IsChecked == true;
        s.RestoreClipboard = RestoreClipboard.IsChecked == true;
        s.AutoSend = AutoSend.IsChecked == true;
        s.ClickToSend = ClickToSend.IsChecked == true;
        s.CopyToClipboardOnly = _clipboardOnly;
        s.PanelPlacement = _placement;
        s.Autostart = Autostart.IsChecked == true;
        s.CheckUpdates = CheckUpdates.IsChecked == true;

        AppState.Current.SettingsStore.Save(s);

        // Guard: only apply a known culture (SetCulture throws on bad input).
        if (KnownLanguages.Contains(s.Language))
        {
            try { LocalizationService.Instance.SetCulture(s.Language); }
            catch (CultureNotFoundException) { /* keep current culture */ }
        }

        try
        {
            if (s.Autostart) QuickText.App.Interop.Autostart.Enable();
            else QuickText.App.Interop.Autostart.Disable();
        }
        catch (Exception ex)
        {
            AppDialog.Alert(this, LocalizationService.Instance["App.Name"],
                $"Could not update the autostart setting: {ex.Message}");
        }

        // Apply hotkey / data folder / abbreviation changes immediately — no restart. The summon
        // triggers stay suspended until this window's Close decrements the ref-count and re-arms
        // them, so Close MUST run even if ReapplySettings throws — otherwise the window stays open,
        // the ref-count never drops, and the global hotkey + tap-summon stay dead for the session.
        try { (Application.Current as App)?.ReapplySettings(); }
        finally { Close(); }
    }
}
