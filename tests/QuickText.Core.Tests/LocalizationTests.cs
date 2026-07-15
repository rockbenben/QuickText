using System.Xml.Linq;
using QuickText.Core.Localization;

namespace QuickText.Core.Tests;

public class LocalizationTests
{
    [Fact]
    public void Switches_language_and_resolves_key()
    {
        var loc = LocalizationService.Instance;
        loc.SetCulture("en");
        Assert.Equal("Settings", loc["Tray.Settings"]);
        loc.SetCulture("ja");
        Assert.Equal("設定", loc["Tray.Settings"]);
        loc.SetCulture("ko");                       // a language added in the 18-language expansion
        Assert.Equal("설정", loc["Tray.Settings"]);
        loc.SetCulture("zh-Hans");
    }

    [Fact]
    public void Unknown_culture_falls_back_to_neutral()
    {
        var loc = LocalizationService.Instance;
        loc.SetCulture("fr-FR"); // unsupported
        Assert.Equal("QuickText", loc["App.Name"]); // neutral (brand name, same in every locale)
        loc.SetCulture("zh-Hans");
    }

    [Fact]
    public void Missing_key_returns_the_key()
    {
        Assert.Equal("No.Such.Key", LocalizationService.Instance["No.Such.Key"]);
    }

    // Every shipped satellite (Strings.<culture>.resx) must carry every neutral key — enforced
    // dynamically, so adding a language file is automatically covered without editing this test.
    [Fact]
    public void Every_satellite_has_all_neutral_keys()
    {
        var srcDir = Path.Combine(FindRepoRoot(), "src", "QuickText.Core", "Localization");
        var neutral = Keys(Path.Combine(srcDir, "Strings.resx"));
        var satellites = Directory.GetFiles(srcDir, "Strings.*.resx");   // excludes the neutral Strings.resx
        // 18 UI languages = the neutral (zh-Hans, Strings.resx) + 17 satellite files.
        Assert.True(satellites.Length >= 17, $"expected ≥17 satellite files, found {satellites.Length}");
        foreach (var path in satellites)
        {
            var missing = neutral.Except(Keys(path)).ToList();
            Assert.True(missing.Count == 0,
                $"{Path.GetFileName(path)} is missing keys: {string.Join(", ", missing)}");
        }
    }

    private static HashSet<string> Keys(string resxPath) =>
        XDocument.Load(resxPath).Root!.Elements("data")
            .Select(d => d.Attribute("name")!.Value).ToHashSet();

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "QuickText.sln")))
            dir = dir.Parent;
        return dir!.FullName;
    }
}
