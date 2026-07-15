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

    private static bool RegistryEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string v && v.Trim('"') == ExePath;
    }

    private static void RegistryEnable()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        key.SetValue(ValueName, $"\"{ExePath}\"");
    }

    private static void RegistryDisable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
