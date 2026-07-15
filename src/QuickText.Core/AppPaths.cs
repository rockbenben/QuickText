namespace QuickText.Core;

/// <summary>
/// Resolves where machine-level state (settings, usage, backups) and the default text
/// library live. Two modes, chosen by a marker file next to the exe:
///
/// <list type="bullet">
///   <item><b>Installed</b> (default): settings/usage/backups in %APPDATA%\QuickText,
///   library defaults to Documents\QuickText. Survives moving the exe; the right choice
///   when the data folder is a sync drive (machine-local state must NOT be synced).</item>
///   <item><b>Portable</b> (a <c>QuickText.portable</c> file sits beside the exe): everything
///   lives under <c>&lt;exeDir&gt;\Data</c> so the whole tool travels on a USB stick and
///   leaves no trace on the host. Autostart then uses a Startup-folder shortcut, not the
///   registry (see <c>Interop.Autostart</c>).</item>
/// </list>
///
/// The <c>*At(exeDir)</c> methods are pure (no ambient state) so they can be unit-tested;
/// the parameterless properties resolve against the process exe dir set at startup.
/// </summary>
public static class AppPaths
{
    /// <summary>Empty marker file that, when present next to the exe, switches on portable mode.</summary>
    public const string PortableMarkerName = "QuickText.portable";

    /// <summary>Sub-folder of the exe dir that holds all portable state.</summary>
    public const string PortableDataDirName = "Data";

    /// <summary>Machine-state file names (one place, so a rename can't miss the portable seed).</summary>
    public const string SettingsFileName = "settings.json";
    public const string UsageFileName = "usage.stats";

    /// <summary>Marker inside Data\ recording that the one-time portable carry-over already ran.</summary>
    public const string SeededMarkerName = ".seeded";

    // Defaults to the app base directory, which already equals the exe's folder in a
    // single-file build; App overrides it from Environment.ProcessPath at startup so it
    // matches Autostart's target exactly.
    private static string _exeDir = AppContext.BaseDirectory;

    // Portable state snapshotted at startup (PinPortableState). The ambient properties resolve
    // against THIS, not a live marker read, so toggling the marker mid-session never moves the
    // running app's paths — a switch only takes hold on the next startup. null = not pinned yet
    // (tests read the marker live); SetExeDir clears it so a new exe dir re-derives cleanly.
    private static bool? _portablePinned;

    /// <summary>Directory containing the running exe (portable-marker + shortcut target live here).</summary>
    public static string ExeDir => _exeDir;

    /// <summary>Pin the exe directory once at startup. Call before any path property is read.</summary>
    public static void SetExeDir(string dir)
    {
        if (!string.IsNullOrWhiteSpace(dir)) { _exeDir = dir; _portablePinned = null; }
    }

    /// <summary>Freeze the installed-vs-portable decision for this session (call once at startup,
    /// after <see cref="SetExeDir"/>). Everything ambient reads this snapshot, so a later
    /// <see cref="SetPortable"/> only changes what the NEXT startup resolves — never this one.</summary>
    public static void PinPortableState() => _portablePinned = IsPortableAt(_exeDir);

    // --- pure resolvers (unit-tested) ------------------------------------------------

    public static bool IsPortableAt(string exeDir) =>
        File.Exists(Path.Combine(exeDir, PortableMarkerName));

    // One place each for the portable/installed layout, so the pure *At forms (what tests verify)
    // and the ambient properties (what the running app reads AND writes) can never diverge.
    private static string MachineStateDirFor(bool portable, string exeDir) =>
        portable
            ? Path.Combine(exeDir, PortableDataDirName)
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QuickText");
    private static string DefaultDataFolderFor(bool portable, string exeDir) =>
        portable
            ? Path.Combine(exeDir, PortableDataDirName, "library")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "QuickText");

    /// <summary>Root for settings.json / usage.stats / backups\.</summary>
    public static string MachineStateDirAt(string exeDir) => MachineStateDirFor(IsPortableAt(exeDir), exeDir);

    /// <summary>Default text-library folder when the user hasn't set one in Settings.</summary>
    public static string DefaultDataFolderAt(string exeDir) => DefaultDataFolderFor(IsPortableAt(exeDir), exeDir);

    // --- ambient convenience (resolve against the process exe dir + the pinned mode) -----------

    // Use the startup snapshot when pinned (the running app), else a live marker read (tests).
    public static bool IsPortable => _portablePinned ?? IsPortableAt(_exeDir);
    public static string MachineStateDir => MachineStateDirFor(IsPortable, _exeDir);
    public static string DefaultDataFolder => DefaultDataFolderFor(IsPortable, _exeDir);
    public static string SettingsPath => Path.Combine(MachineStateDir, SettingsFileName);
    public static string BackupDir => Path.Combine(MachineStateDir, "backups");

    // --- toggle portable mode (Settings does this for the user, instead of hand-creating a file) --

    /// <summary>
    /// Turn portable mode on/off by creating or deleting the marker file — the only place the flag
    /// can live, since it decides where <c>settings.json</c> itself is read from (a setting couldn't
    /// bootstrap that). This writes ONLY the marker (an all-or-nothing change), so on failure it
    /// returns <c>false</c> having changed nothing; the exe folder being read-only (e.g. an install
    /// under Program Files) is the usual cause. It takes hold on the NEXT startup — the running
    /// session's paths are pinned (<see cref="PinPortableState"/>). The config/favorites carry over
    /// then via <see cref="SeedPortableMachineState"/>; the text library moves via export/import.
    /// </summary>
    public static bool SetPortableAt(string exeDir, bool enabled)
    {
        try
        {
            var marker = Path.Combine(exeDir, PortableMarkerName);
            if (enabled) File.WriteAllText(marker, "");        // also the writable-folder test
            else if (File.Exists(marker)) File.Delete(marker);
            return true;
        }
        catch { return false; }
    }

    /// <summary>Toggle portable mode against the process exe dir. See <see cref="SetPortableAt"/>.</summary>
    public static bool SetPortable(bool enabled) => SetPortableAt(_exeDir, enabled);

    /// <summary>Copy the machine-state files (settings + usage) that are MISSING in <paramref name="toDir"/>
    /// from <paramref name="fromDir"/> — copy-only, never overwriting. Pure/testable core of the seed.</summary>
    public static void SeedStateFiles(string fromDir, string toDir)
    {
        Directory.CreateDirectory(toDir);
        foreach (var name in new[] { SettingsFileName, UsageFileName })
        {
            var src = Path.Combine(fromDir, name);
            var dst = Path.Combine(toDir, name);
            if (File.Exists(src) && !File.Exists(dst)) File.Copy(src, dst);
        }
    }

    private static bool HasStateFiles(string dir) =>
        File.Exists(Path.Combine(dir, SettingsFileName)) || File.Exists(Path.Combine(dir, UsageFileName));

    /// <summary>Carry the machine-state files from <paramref name="fromDir"/> into
    /// <paramref name="toDir"/> exactly ONCE: no-op (returns false) if the <c>.seeded</c> marker is
    /// already there; otherwise copy the missing files and drop the marker so a later delete of a
    /// state file resets to defaults instead of being repopulated. Sealing keys off whether the
    /// SOURCE has config, not on how many files were copied — so a retry after a failed marker write
    /// (files already present, nothing left to copy) still seals, while a genuinely empty source
    /// leaves the gate OPEN so config that appears later still carries over on a future start.
    /// Pure/testable core of <see cref="SeedPortableMachineState"/>.</summary>
    public static bool SeedStateOnce(string fromDir, string toDir)
    {
        if (File.Exists(Path.Combine(toDir, SeededMarkerName))) return false;
        if (!HasStateFiles(fromDir)) return false;   // nothing to carry yet — leave the gate open
        SeedStateFiles(fromDir, toDir);              // copy-only (a no-op when a prior run already copied them)
        File.WriteAllText(Path.Combine(toDir, SeededMarkerName), "");
        return true;
    }

    /// <summary>Once — on the first startup after switching to portable — carry the installed-mode
    /// config and favorites into <c>Data\</c> so portable doesn't start from defaults. Runs at
    /// startup, before any store opens (nothing races the copy), rather than at toggle time (which
    /// would snapshot a stale state). Best-effort; see <see cref="SeedStateOnce"/> for the gate.</summary>
    public static void SeedPortableMachineState()
    {
        if (!IsPortable) return;
        try
        {
            var installed = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QuickText");
            SeedStateOnce(installed, MachineStateDir);   // MachineStateDir = <exeDir>\Data in portable mode
        }
        catch { /* best-effort — retry next start, or portable just starts with defaults */ }
    }
}
