using System.Text.Json;
using QuickText.Core.Persistence;

namespace QuickText.Core.Settings;

public sealed class SettingsStore
{
    private readonly string _path;
    public SettingsStore(string path) => _path = path;

    // %APPDATA%\QuickText\settings.json normally; <exeDir>\Data\settings.json in portable mode.
    public static string DefaultPath => AppPaths.SettingsPath;

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_path)) return new AppSettings();
            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonConfig.Read) ?? new AppSettings();
        }
        catch { return new AppSettings(); }
    }

    public void Save(AppSettings s)
    {
        var dir = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(s, JsonConfig.Write);
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, json);
        if (File.Exists(_path)) File.Replace(tmp, _path, null);
        else File.Move(tmp, _path);
    }
}
