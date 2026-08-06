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

    // Placeholder parity. A value formatted with string.Format must carry the SAME {N} slots in
    // every language: drop {0} from one translation and that language silently loses the only
    // concrete detail in the sentence (which hotkey, which snippet) with no crash to notice it.
    // Enforced per key against the neutral file, so a new formatted string is covered on arrival.
    [Fact]
    public void Every_satellite_keeps_the_neutral_placeholders()
    {
        var srcDir = Path.Combine(FindRepoRoot(), "src", "QuickText.Core", "Localization");
        var neutral = Values(Path.Combine(srcDir, "Strings.resx"));
        foreach (var path in Directory.GetFiles(srcDir, "Strings.*.resx"))
        {
            var theirs = Values(path);
            foreach (var (key, value) in neutral)
            {
                if (!theirs.TryGetValue(key, out var other)) continue;   // covered by the keys test
                var want = Slots(value);
                var got = Slots(other);
                Assert.True(want.SetEquals(got),
                    $"{Path.GetFileName(path)} [{key}]: placeholders {Fmt(got)} ≠ neutral {Fmt(want)}");
            }
        }
    }

    // {0} / {1} … but not the escaped literal braces {{ }} that the placeholder-syntax strings use.
    private static HashSet<int> Slots(string value) =>
        System.Text.RegularExpressions.Regex
            .Matches(value.Replace("{{", "").Replace("}}", ""), @"\{(\d+)")
            .Select(m => int.Parse(m.Groups[1].Value)).ToHashSet();

    private static string Fmt(IEnumerable<int> s) =>
        s.Any() ? "{" + string.Join("},{", s.OrderBy(i => i)) + "}" : "(none)";

    private static Dictionary<string, string> Values(string resxPath) =>
        XDocument.Load(resxPath).Root!.Elements("data")
            .ToDictionary(d => d.Attribute("name")!.Value, d => d.Element("value")?.Value ?? "");

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
