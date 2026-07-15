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
}
