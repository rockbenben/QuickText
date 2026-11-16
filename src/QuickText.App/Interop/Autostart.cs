using System;
using System.IO;
using System.Threading.Tasks;

namespace QuickText.App.Interop;

/// <summary>Outcome of <see cref="Autostart.Apply"/> for backward compatibility.</summary>
public enum AutostartResult { Applied, Declined, Failed }

/// <summary>
/// "Start with Windows" toggle. Two mutually exclusive mechanisms share one settings switch:
/// <list type="bullet">
///   <item><b>Normal</b>: installed mode uses the HKCU Run registry value; portable mode uses a
///   Startup-folder shortcut instead, so a USB copy leaves no registry trace. Both launch unelevated.</item>
///   <item><b>Elevated</b> (opt-in): a Task Scheduler logon task with <c>RunLevel=HighestAvailable</c>,
///   so the app comes up as administrator and can paste into elevated windows — see <see cref="ElevatedStartup"/>.
///   The Run/Shortcut entry is removed in the same transaction.</item>
/// </list>
/// Enabling one mechanism clears the other, and disabling clears both with full transactional rollback.
/// </summary>
public static class Autostart
{
    /// <summary>Marks a launch as "Windows started us at login", so it comes up silently in the tray
    /// instead of popping the search panel the way a hand-launched exe does.</summary>
    public const string Flag = "--autostart";

    public static StartupOptions DefaultOptions => new(
        AppId: "QuickText",
        ExecutableName: "QuickText.exe",
        TrayArguments: Flag,
        LegacyRunValueName: null,
        PlainArguments: null,
        LogonDelaySeconds: 3,
        UseStartupShortcut: Core.AppPaths.IsPortable,
        LegacyTaskNames: LegacyTasks
    );

    internal static string[] LegacyTasks
    {
        get
        {
            try
            {
                using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                string sid = identity.User?.Value ?? "";
                return string.IsNullOrEmpty(sid) ? ["QuickText"] : [$"QuickText.{sid}", "QuickText"];
            }
            catch { return ["QuickText"]; }
        }
    }

    internal static string ResolveExePath(string executableName)
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(processPath) &&
            Path.GetFileName(processPath).Equals(executableName, StringComparison.OrdinalIgnoreCase))
        {
            return processPath;
        }
        // Fallback when running inside unit test host (testhost.exe / QuickText.App.Tests.exe)
        return Path.Combine(AppContext.BaseDirectory, executableName);
    }

    /// <summary>Which mechanism is live right now, read from the OS rather than from settings.
    /// Elevated wins when both exist, because that is the one Windows will actually run with elevation.</summary>
    public static (bool Enabled, bool Elevated) CurrentState(IStartupStore? storeOverride = null, StartupOptions? optionsOverride = null)
    {
        var options = optionsOverride ?? DefaultOptions;
        var store = storeOverride ?? new WindowsStartupStore();
        try
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            string userSid = identity.User?.Value ?? "";
            if (string.IsNullOrEmpty(userSid)) return Fallback();

            string exePath = ResolveExePath(options.ExecutableName);
            var target = ElevatedStartup.Target(options, exePath, userSid);
            string? taskXml = store.ReadTask(target.TaskName);
            if (taskXml != null && ElevatedStartup.Owned(taskXml, target))
                return (true, true);

            if (options.LegacyTaskNames is { } legacyNames)
            {
                foreach (var legacy in legacyNames)
                {
                    string? legXml = store.ReadTask(legacy);
                    if (legXml != null && (ElevatedStartup.Owned(legXml, target) || ElevatedStartup.BelongsToLegacyQuickText(legXml, userSid)))
                        return (true, true);
                }
            }

            if (options.UseStartupShortcut)
            {
                var shortcut = store.ReadShortcut(options.AppId);
                if (shortcut != null && string.Equals(shortcut.Executable, exePath, StringComparison.OrdinalIgnoreCase))
                    return (true, false);
            }
            else
            {
                string? run = store.ReadRun(options.AppId);
                if (run != null && PointsAtUs(run, exePath))
                    return (true, false);
            }

            return (false, false);
        }
        catch
        {
            return Fallback();
        }

        static (bool, bool) Fallback() =>
            (AppState.Current.Settings.Autostart, AppState.Current.Settings.AutostartElevated);
    }

    internal static bool PointsAtUs(string command, string exePath)
    {
        if (string.IsNullOrEmpty(exePath)) return false;
        var c = command.Trim();
        if (c.StartsWith('"'))
        {
            int end = c.IndexOf('"', 1);
            return end > 0 && string.Equals(c[1..end], exePath, StringComparison.OrdinalIgnoreCase);
        }
        return string.Equals(c, exePath, StringComparison.OrdinalIgnoreCase) ||
               (c.StartsWith(exePath, StringComparison.OrdinalIgnoreCase) && c.Length > exePath.Length && c[exePath.Length] == ' ');
    }

    /// <summary>
    /// Apply the (enabled, elevated) setting with transactional rollback.
    /// Synchronous variant suitable for unit testing with an injected store or direct execution.
    /// </summary>
    public static StartupResult Apply(bool enabled, bool elevated, IStartupStore? storeOverride = null, StartupOptions? optionsOverride = null)
    {
        var options = optionsOverride ?? DefaultOptions;
        var store = storeOverride ?? new WindowsStartupStore();
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        string userSid = identity.User!.Value;
        string exePath = ResolveExePath(options.ExecutableName);
        var target = ElevatedStartup.Target(options, exePath, userSid);

        StartupOperation op = (!enabled)
            ? StartupOperation.Uninstall
            : (!elevated ? StartupOperation.Plain : StartupOperation.Install);

        return ElevatedStartup.Apply(op, target, store);
    }

    /// <summary>
    /// Apply the (enabled, elevated) setting asynchronously via <see cref="ElevatedStartup.ChangeAsync"/>.
    /// Handles UAC helper dispatch without blocking the WPF dispatcher.
    /// </summary>
    public static Task<StartupResult> ApplyAsync(bool enabled, bool elevated, StartupOptions? optionsOverride = null)
    {
        var options = optionsOverride ?? DefaultOptions;
        StartupOperation op = (!enabled)
            ? StartupOperation.Uninstall
            : (!elevated ? StartupOperation.Plain : StartupOperation.Install);

        return ElevatedStartup.ChangeAsync(op, options);
    }
}
