namespace QuickText.Core.Settings;

public sealed class AppSettings
{
    // How the panel is summoned — the two are mutually exclusive: "hotkey" uses Hotkey (a key
    // combo via RegisterHotKey), "tap" uses SummonTapKey (tap/double-tap a lone modifier).
    public string SummonMode { get; set; } = "hotkey";
    public string Hotkey { get; set; } = "Ctrl+Shift+8";
    // The lone modifier for "tap" mode (RegisterHotKey can't express a single modifier):
    // one of RCtrl/LCtrl/RShift/LShift (Alt/Win excluded — they have OS side effects).
    public string SummonTapKey { get; set; } = "";
    public bool SummonTapDouble { get; set; }   // true = double-tap, false = single-tap
    public string CaptureHotkey { get; set; } = "";   // save clipboard as a snippet; empty = disabled
    public string DataFolder { get; set; } = "";
    public bool Autostart { get; set; }
    public bool AbbrEnabled { get; set; } = true;
    public string TerminatorChars { get; set; } = " \t\r\n";
    public string AbbrPrefix { get; set; } = ";";   // auto-prepended to every abbreviation when matching
    public string AbbrBlacklist { get; set; } = "";   // process names where expansion is disabled, ";"-separated
    public bool RestoreClipboard { get; set; } = true;
    // One-time upgrade marker: placeholder processing used to be always-on; on first launch
    // after the per-snippet opt-in landed, snippets whose body contains {…} get opted in.
    public bool VarsOptInMigrated { get; set; }
    public bool AutoSend { get; set; }
    public bool ClickToSend { get; set; }
    public bool CopyToClipboardOnly { get; set; }   // true = just copy; false = paste into the active app
    public string Language { get; set; } = "";
    public bool EditorWrap { get; set; } = true;   // Manager body editor: wrap vs horizontal scroll (for code)
    // Manager body editor: show a logical-line-number gutter (off by default — plain-text users
    // don't need it; it's for snippets that hold code/JSON).
    public bool EditorLineNumbers { get; set; }
    // Manager: whether the image section is expanded. Collapsed by default so the body editor
    // gets the height; a snippet that HAS an image expands it regardless.
    public bool EditorImageExpanded { get; set; }

    // Remembered "enlarge body" window bounds (0 = unset -> 80% of the active monitor's work area).
    /// <summary>No longer read or written by the app. The enlarged body editor used to reopen at
    /// this remembered absolute position, but on a multi-monitor desktop that meant it always
    /// reopened on whichever monitor it was last closed on, regardless of where the Manager (its
    /// owner) currently is — so it now always centers on the owner's monitor instead, and only the
    /// size (<see cref="BodyWinW"/>/<see cref="BodyWinH"/>) is remembered. Left in place (not
    /// removed) only so existing users' settings.json keeps round-tripping without churn — do not
    /// wire this back up.</summary>
    public double BodyWinX { get; set; }
    /// <summary>No longer read or written — see <see cref="BodyWinX"/>.</summary>
    public double BodyWinY { get; set; }
    public double BodyWinW { get; set; }
    public double BodyWinH { get; set; }
    // Opt-in, OFF by default: when true the app makes ONE network request to the GitHub Releases API
    // at startup to see if a newer version exists (everything else stays fully offline). Off keeps the
    // "no network" promise intact.
    public bool CheckUpdates { get; set; }

    // Where the summon panel appears: "window" (active window's monitor, default),
    // "caret" (near the text caret, falls back to window), "fixed" (remembered position).
    public string PanelPlacement { get; set; } = "window";

    // Remembered search-panel bounds (0 = unset -> center with default size).
    public double PanelX { get; set; }
    public double PanelY { get; set; }
    public double PanelW { get; set; }
    public double PanelH { get; set; }
}
