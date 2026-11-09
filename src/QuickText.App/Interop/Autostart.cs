using Microsoft.Win32;

namespace QuickText.App.Interop;

/// <summary>Outcome of <see cref="Autostart.Apply"/>. <b>Declined</b> means the user dismissed the
/// one-time UAC prompt: nothing changed, which is not an error to report but still must not be
/// persisted as a mode. The reason behind a <b>Failed</b> apply is in the log, not in the UI.</summary>
public enum AutostartResult { Applied, Declined, Failed }

/// <summary>
/// "Start with Windows" toggle. Two mutually exclusive mechanisms share one settings switch:
/// <list type="bullet">
///   <item><b>Normal</b>: installed mode uses the HKCU Run registry value; portable mode (see
///   <see cref="QuickText.Core.AppPaths"/>) uses a Startup-folder shortcut instead, so a USB copy
///   leaves no registry trace. Both launch unelevated.</item>
///   <item><b>Elevated</b> (opt-in): a Task Scheduler logon task with
///   <c>RunLevel=HighestAvailable</c>, so the app comes up as administrator and can paste into
///   elevated windows — see <see cref="ElevatedAutostart"/>. The Run/Shortcut entry is removed.</item>
/// </list>
/// Enabling one mechanism clears the other, and disabling clears both, so switching never strands
/// a second autostart entry.
/// </summary>
public static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "QuickText";

    /// <summary>Marks a launch as "Windows started us at login", so it comes up silently in the tray
    /// instead of popping the search panel the way a hand-launched exe does.</summary>
    public const string Flag = "--autostart";

    // Environment.ProcessPath is the real exe path even in a single-file build; don't fall back
    // to Assembly.Location, which is empty for single-file (IL3000).
    private static string ExePath => Environment.ProcessPath ?? "";

    /// <summary>Which mechanism is live right now, read from the OS rather than from settings — a
    /// user (or an uninstaller, or Task Scheduler cleanup) may have removed an entry behind our
    /// back. Elevated wins when both exist, because that is the one Windows will actually run
    /// with elevation. A logon task that still points at an OLD exe location counts as elevated:
    /// its name is ours, so the toggle must offer to fix it rather than pretend nothing is on.</summary>
    public static (bool Enabled, bool Elevated) CurrentState()
    {
        var state = Read();
        if (!state.Known) return (AppState.Current.Settings.Autostart, AppState.Current.Settings.AutostartElevated);
        return state.ElevatedTask ? (true, true)
            : state.ModeEntry ? (true, false) : (false, false);
    }

    private static bool ModeEnabled() =>
        Core.AppPaths.IsPortable ? StartupShortcut.Exists() : RegistryEnabled();

    private static (bool ElevatedTask, bool TaskFresh, bool ModeEntry, bool Known) Read()
    {
        try
        {
            var (exists, fresh, known) = ElevatedAutostart.Probe();
            return (exists, fresh, exists ? false : ModeEnabled(), known);
        }
        catch { return (false, false, false, false); }
    }

    /// <summary>
    /// Apply the saved (enabled, elevated) pair. On anything but <see cref="AutostartResult.Applied"/>
    /// nothing was changed, so the caller must re-read the OS instead of persisting a mode that is
    /// not live. A declined UAC prompt is <see cref="AutostartResult.Declined"/> — not an error to
    /// show, but not a change either; it must NOT let us delete the old mechanism, or one cancelled
    /// prompt leaves the user with no autostart at all.
    /// </summary>
    public static AutostartResult Apply(bool enabled, bool elevated)
    {
        var (currentElevated, taskFresh, currentMode, known) = Read();
        if (!known) return AutostartResult.Failed;
        bool currentEnabled = currentElevated || currentMode;
        bool targetElevated = enabled && elevated;

        // "Nothing to do" — except an elevated task left pointing at an old location (a portable
        // copy that was moved). Answering that with "already applied" is what strands the stale
        // path forever, because the toggle already reads as on and no later save repairs it.
        if (currentElevated == targetElevated && currentEnabled == enabled &&
            (!targetElevated || taskFresh)) return AutostartResult.Applied;

        if (currentElevated != targetElevated || !taskFresh)
        {
            var taskRes = ElevatedAutostart.Apply(targetElevated);
            if (taskRes != AutostartResult.Applied) return taskRes;
        }

        try
        {
            if (enabled && !elevated)
            {
                if (Core.AppPaths.IsPortable)
                {
                    RegistryDisable();          // clear any stale installed-mode entry
                    StartupShortcut.Create();
                }
                else
                {
                    StartupShortcut.Delete();   // clear any stale portable-mode shortcut
                    RegistryEnable();
                }
            }
            else
            {
                StartupShortcut.Delete();
                RegistryDisable();
            }
        }
        catch (Exception ex)
        {
            // A read-only USB stick or a denied HKCU write: the log carries the reason, the UI gets
            // its one localized sentence. Threading this out of an async void Save would instead
            // take the whole tray down.
            QuickText.Core.Log.Error("autostart entry", ex);
            return AutostartResult.Failed;
        }
        return AutostartResult.Applied;
    }

    /// <summary>The stored Run command, or null when the value doesn't exist.</summary>
    private static string? RegistryCommand()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) as string;
    }

    // The stored command is "<exe>" [args] — compare only the exe part, so adding arguments (or a
    // later flag) never makes the toggle read as "off" and strand the entry.
    private static bool RegistryEnabled()
    {
        // Without this, a missing value ("" once defaulted) would compare equal to an empty ExePath
        // (Environment.ProcessPath null) and report autostart as ON with no entry present.
        if (ExePath.Length == 0) return false;
        return RegistryCommand() is { } cmd && PointsAtUs(cmd);
    }

    /// <summary>
    /// Does this Run command launch OUR exe? Handles the quoted form we write ourselves plus the
    /// unquoted forms a third-party installer, a group policy or regedit can leave behind — including
    /// an unquoted path that CONTAINS SPACES followed by arguments (<c>C:\Program Files\…\QuickText.exe
    /// --autostart</c>), which no "split at the first space" rule can parse. Matching the known exe
    /// path as a prefix does, because we are asking "is this us?", not "what does this launch?".
    /// </summary>
    internal static bool PointsAtUs(string command)
    {
        var c = command.Trim();
        if (c.StartsWith('"'))
        {
            int end = c.IndexOf('"', 1);
            return end > 0 && Eq(c[1..end], ExePath);
        }
        // Unquoted: our path, alone or followed by arguments (the space guard keeps
        // "…\QuickTextPro.exe" from matching a "…\QuickText.exe" install).
        return Eq(c, ExePath) ||
               (c.StartsWith(ExePath, StringComparison.OrdinalIgnoreCase) && c[ExePath.Length] == ' ');

        static bool Eq(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    private static void RegistryEnable()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        key.SetValue(ValueName, $"\"{ExePath}\" {Flag}");
    }

    private static void RegistryDisable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
