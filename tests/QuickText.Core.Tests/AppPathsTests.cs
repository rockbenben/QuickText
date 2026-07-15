using QuickText.Core;

namespace QuickText.Core.Tests;

public class AppPathsTests
{
    // A fresh empty dir standing in for the exe directory.
    private static string TempExeDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "qt_exe_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void Marker(string exeDir) =>
        File.WriteAllText(Path.Combine(exeDir, AppPaths.PortableMarkerName), "");

    [Fact]
    public void No_marker_is_installed_mode_using_appdata()
    {
        var exe = TempExeDir();
        try
        {
            Assert.False(AppPaths.IsPortableAt(exe));

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            Assert.Equal(Path.Combine(appData, "QuickText"), AppPaths.MachineStateDirAt(exe));

            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            Assert.Equal(Path.Combine(docs, "QuickText"), AppPaths.DefaultDataFolderAt(exe));
        }
        finally { Directory.Delete(exe, recursive: true); }
    }

    [Fact]
    public void Marker_switches_to_portable_paths_under_exe_dir()
    {
        var exe = TempExeDir();
        try
        {
            Marker(exe);
            Assert.True(AppPaths.IsPortableAt(exe));

            var data = Path.Combine(exe, AppPaths.PortableDataDirName);
            Assert.Equal(data, AppPaths.MachineStateDirAt(exe));
            Assert.Equal(Path.Combine(data, "library"), AppPaths.DefaultDataFolderAt(exe));
        }
        finally { Directory.Delete(exe, recursive: true); }
    }

    [Fact]
    public void SetExeDir_makes_ambient_properties_follow_the_marker()
    {
        var exe = TempExeDir();
        try
        {
            Marker(exe);
            AppPaths.SetExeDir(exe);

            Assert.True(AppPaths.IsPortable);
            var data = Path.Combine(exe, AppPaths.PortableDataDirName);
            Assert.Equal(Path.Combine(data, "settings.json"), AppPaths.SettingsPath);
            Assert.Equal(Path.Combine(data, "backups"), AppPaths.BackupDir);
        }
        finally
        {
            // Restore the ambient dir so other tests aren't affected by the mutable static.
            AppPaths.SetExeDir(AppContext.BaseDirectory);
            Directory.Delete(exe, recursive: true);
        }
    }

    [Fact]
    public void SetExeDir_ignores_blank_input()
    {
        var before = AppPaths.ExeDir;
        AppPaths.SetExeDir("");
        Assert.Equal(before, AppPaths.ExeDir);
    }

    [Fact]
    public void SetPortable_creates_then_removes_the_marker_and_never_deletes_data()
    {
        var exe = TempExeDir();
        try
        {
            Assert.False(AppPaths.IsPortableAt(exe));
            Assert.True(AppPaths.SetPortableAt(exe, enabled: true));
            Assert.True(AppPaths.IsPortableAt(exe));

            // portable data living under Data\ must survive a disable — toggling off removes the
            // marker only, never the user's settings/usage/library.
            var stamp = Path.Combine(exe, AppPaths.PortableDataDirName, AppPaths.SettingsFileName);
            Directory.CreateDirectory(Path.GetDirectoryName(stamp)!);
            File.WriteAllText(stamp, "keep me");

            Assert.True(AppPaths.SetPortableAt(exe, enabled: false));
            Assert.False(AppPaths.IsPortableAt(exe));
            Assert.True(File.Exists(stamp));
            Assert.Equal("keep me", File.ReadAllText(stamp));
        }
        finally { Directory.Delete(exe, recursive: true); }
    }

    [Fact]
    public void PinPortableState_freezes_the_mode_against_a_later_marker_change()
    {
        var exe = TempExeDir();
        try
        {
            AppPaths.SetExeDir(exe);              // clears any prior pin
            AppPaths.PinPortableState();          // snapshot: not portable (no marker)
            Assert.False(AppPaths.IsPortable);

            AppPaths.SetPortableAt(exe, enabled: true);   // marker written now
            Assert.True(AppPaths.IsPortableAt(exe));       // a live read sees it...
            Assert.False(AppPaths.IsPortable);             // ...but the pinned ambient mode does NOT flip
        }
        finally
        {
            AppPaths.SetExeDir(AppContext.BaseDirectory);   // restore + clear pin for other tests
            Directory.Delete(exe, recursive: true);
        }
    }

    [Fact]
    public void SeedStateFiles_copies_missing_files_only_never_overwriting()
    {
        var from = TempExeDir();
        var to = TempExeDir();
        try
        {
            File.WriteAllText(Path.Combine(from, AppPaths.SettingsFileName), "new-config");
            File.WriteAllText(Path.Combine(from, AppPaths.UsageFileName), "new-usage");
            File.WriteAllText(Path.Combine(to, AppPaths.SettingsFileName), "existing");   // must be kept

            AppPaths.SeedStateFiles(from, to);

            Assert.Equal("existing", File.ReadAllText(Path.Combine(to, AppPaths.SettingsFileName)));  // not overwritten
            Assert.Equal("new-usage", File.ReadAllText(Path.Combine(to, AppPaths.UsageFileName)));    // copied in
        }
        finally { Directory.Delete(from, true); Directory.Delete(to, true); }
    }

    [Fact]
    public void SeedStateOnce_carries_over_once_then_never_resurrects_a_deleted_file()
    {
        var from = TempExeDir();
        var to = TempExeDir();
        try
        {
            File.WriteAllText(Path.Combine(from, AppPaths.SettingsFileName), "installed-config");

            Assert.True(AppPaths.SeedStateOnce(from, to));   // first run: carry the config over + mark seeded
            Assert.Equal("installed-config", File.ReadAllText(Path.Combine(to, AppPaths.SettingsFileName)));

            // User deletes it to reset to defaults; the gate must NOT bring the stale copy back.
            File.Delete(Path.Combine(to, AppPaths.SettingsFileName));
            Assert.False(AppPaths.SeedStateOnce(from, to));  // already seeded → no-op
            Assert.False(File.Exists(Path.Combine(to, AppPaths.SettingsFileName)));
        }
        finally { Directory.Delete(from, true); Directory.Delete(to, true); }
    }

    [Fact]
    public void SeedStateOnce_leaves_the_gate_open_until_there_is_something_to_carry()
    {
        var from = TempExeDir();
        var to = TempExeDir();
        try
        {
            // Portable enabled before installed mode was ever configured: nothing to carry, so the
            // gate must NOT seal — otherwise config created later never migrates.
            Assert.False(AppPaths.SeedStateOnce(from, to));
            Assert.False(File.Exists(Path.Combine(to, AppPaths.SeededMarkerName)));

            // Config appears later; now it carries over and seals.
            File.WriteAllText(Path.Combine(from, AppPaths.SettingsFileName), "later-config");
            Assert.True(AppPaths.SeedStateOnce(from, to));
            Assert.Equal("later-config", File.ReadAllText(Path.Combine(to, AppPaths.SettingsFileName)));
            Assert.True(File.Exists(Path.Combine(to, AppPaths.SeededMarkerName)));
        }
        finally { Directory.Delete(from, true); Directory.Delete(to, true); }
    }

    [Fact]
    public void SeedStateOnce_seals_on_retry_even_after_the_files_were_already_copied()
    {
        var from = TempExeDir();
        var to = TempExeDir();
        try
        {
            File.WriteAllText(Path.Combine(from, AppPaths.SettingsFileName), "cfg");
            // Simulate a prior start that copied the file but crashed before writing the marker.
            File.WriteAllText(Path.Combine(to, AppPaths.SettingsFileName), "cfg");
            Assert.False(File.Exists(Path.Combine(to, AppPaths.SeededMarkerName)));

            // Source still has config, so the retry must seal even though the copy is now a no-op —
            // otherwise the gate never closes and a deleted state file is resurrected every start.
            Assert.True(AppPaths.SeedStateOnce(from, to));
            Assert.True(File.Exists(Path.Combine(to, AppPaths.SeededMarkerName)));
        }
        finally { Directory.Delete(from, true); Directory.Delete(to, true); }
    }
}
