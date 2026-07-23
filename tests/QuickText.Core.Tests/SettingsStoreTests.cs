using QuickText.Core.Settings;

namespace QuickText.Core.Tests;

public class SettingsStoreTests
{
    private static string TempFile() =>
        Path.Combine(Path.GetTempPath(), "kh_" + Guid.NewGuid().ToString("N") + ".json");

    [Fact]
    public void Load_missing_file_returns_defaults()
    {
        var store = new SettingsStore(TempFile());
        var s = store.Load();
        Assert.Equal("Ctrl+Shift+8", s.Hotkey);
        Assert.True(s.AbbrEnabled);
        Assert.True(s.RestoreClipboard);
        Assert.Equal("", s.Language);
    }

    [Fact]
    public void Save_then_load_round_trips()
    {
        var path = TempFile();
        var store = new SettingsStore(path);
        store.Save(new AppSettings { Hotkey = "Ctrl+`", Language = "ja", AbbrEnabled = false });
        var s = store.Load();
        Assert.Equal("Ctrl+`", s.Hotkey);
        Assert.Equal("ja", s.Language);
        Assert.False(s.AbbrEnabled);
        File.Delete(path);
    }

    [Fact]
    public void Load_corrupt_file_returns_defaults()
    {
        var path = TempFile();
        File.WriteAllText(path, "{ not valid json");
        var s = new SettingsStore(path).Load();
        Assert.Equal("Ctrl+Shift+8", s.Hotkey);
        File.Delete(path);
    }

    [Fact]
    public void Editor_settings_default_off_and_survive_a_roundtrip()
    {
        var fresh = new AppSettings();
        Assert.False(fresh.EditorLineNumbers);
        Assert.False(fresh.EditorImageExpanded);
        Assert.Equal(0, fresh.BodyWinX);
        Assert.Equal(0, fresh.BodyWinY);
        Assert.Equal(0, fresh.BodyWinW);
        Assert.Equal(0, fresh.BodyWinH);

        var path = Path.Combine(Path.GetTempPath(), "quicktext-test-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new SettingsStore(path);
            store.Save(new AppSettings
            {
                EditorLineNumbers = true,
                EditorImageExpanded = true,
                BodyWinX = 120, BodyWinY = 80, BodyWinW = 1000, BodyWinH = 700,
            });
            var back = store.Load();
            Assert.True(back.EditorLineNumbers);
            Assert.True(back.EditorImageExpanded);
            Assert.Equal(120, back.BodyWinX);
            Assert.Equal(80, back.BodyWinY);
            Assert.Equal(1000, back.BodyWinW);
            Assert.Equal(700, back.BodyWinH);
        }
        finally { File.Delete(path); }
    }
}
