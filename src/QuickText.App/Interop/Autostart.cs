using Microsoft.Win32;

namespace QuickText.App.Interop;

/// <summary>
/// "Start with Windows" toggle. Installed mode uses the HKCU Run registry value; portable
/// mode (see <see cref="QuickText.Core.AppPaths"/>) uses a Startup-folder shortcut instead,
/// so a USB copy leaves no registry trace. Enabling one mechanism clears the other, and
/// Disable clears both, so switching modes never strands a second autostart entry.
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

    public static bool IsEnabled() =>
        Core.AppPaths.IsPortable ? StartupShortcut.Exists() : RegistryEnabled();

    public static void Enable()
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

    public static void Disable()
    {
        StartupShortcut.Delete();
        RegistryDisable();
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
