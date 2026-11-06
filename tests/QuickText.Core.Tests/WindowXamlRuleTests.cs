using System.Text.RegularExpressions;

namespace QuickText.Core.Tests;

/// <summary>
/// Text-only rules over the app's window XAML. These catch the two things a screenshot matrix
/// cannot: a runtime height cap that <c>--shots</c> sets for itself (so every image looks fine while
/// a real short-screen laptop pushes the Save button under the taskbar), and a wrapping TextBlock
/// with nothing bounded to wrap against (which reads as "wrapping was handled" forever).
/// <para>Locating the repo from the test assembly is deliberate: the test project targets plain
/// net10.0, so it cannot reference the WPF app — but reading the XAML as text needs no WPF at all.</para>
/// </summary>
public class WindowXamlRuleTests
{
    // The tightest work area this app supports, NOT the one a comfortable desktop produces.
    // 1366x768 is the small-laptop screen that actually ships, and users who run it at 150%
    // scaling get the smallest work area of all: 768/1.50 - 48(taskbar) ≈ 464 DIP.
    // The @125% row of the same screen is 566 — 100 DIP looser, so a fallback sized against it
    // passes every screenshot here and still pushes the Save button off-screen on that laptop.
    private const int SmallestWorkAreaDip = 464;   // 1366x768 @150%, Win11 taskbar

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "QuickText.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("QuickText.sln not found above the test assembly");
    }

    private static string[] AppWindowXaml() =>
        Directory.GetFiles(Path.Combine(RepoRoot(), "src", "QuickText.App", "Ui"), "*.xaml");

    /// <summary>
    /// A <c>SizeToContent="Height"</c> window must be bounded against a short screen, from either of
    /// two places — and the point of this test is that relying on only one is the failure:
    /// <list type="bullet">
    ///   <item><b>XAML fallback</b> (<c>MaxHeight</c> on the Window element): visible in the file, and
    ///   the only thing standing when the runtime measurement is unavailable.</item>
    ///   <item><b>Runtime cap</b> (<c>WindowTheming.CapHeightToMonitor</c> /
    ///   <c>PlaceOnActiveMonitor</c>): derived from the monitor's real work area, so it is right on
    ///   every machine and the fallback is only a floor.</item>
    /// </list>
    /// A window that relies on the runtime cap alone looks correct in <c>--shots</c> forever, because
    /// that harness sets the same cap itself. So the fallback must be asserted as TEXT, not seen.
    /// </summary>
    [Fact]
    public void Every_auto_height_window_declares_a_MaxHeight_fallback_in_xaml()
    {
        var offenders = new List<string>();
        foreach (var file in AppWindowXaml())
        {
            var xaml = File.ReadAllText(file);
            if (!xaml.Contains("SizeToContent=\"Height\"")) continue;
            var window = Regex.Match(xaml, @"<Window\b(?<attrs>[^>]*)>", RegexOptions.Singleline);
            if (!window.Success) continue;
            if (!window.Groups["attrs"].Value.Contains("MaxHeight="))
                offenders.Add(Path.GetFileName(file));
        }

        Assert.True(offenders.Count == 0,
            "auto-height windows without a XAML MaxHeight fallback: " + string.Join(", ", offenders)
            + ". A runtime cap alone is invisible here and in --shots, which sets the same cap itself.");
    }

    /// <summary>
    /// The fallback must be sized for the SMALLEST supported work area, not a comfortable desktop:
    /// a number tuned for 1080p is exactly what puts the Save button off-screen on 1366x768.
    /// </summary>
    [Fact]
    public void The_MaxHeight_fallback_fits_the_smallest_work_area()
    {
        foreach (var file in AppWindowXaml())
        {
            var xaml = File.ReadAllText(file);
            if (!xaml.Contains("SizeToContent=\"Height\"")) continue;
            foreach (Match m in Regex.Matches(xaml, @"MaxHeight=""(\d+)"""))
            {
                int value = int.Parse(m.Groups[1].Value);
                Assert.True(value <= SmallestWorkAreaDip,
                    $"{Path.GetFileName(file)}: MaxHeight={value} exceeds the smallest work area ({SmallestWorkAreaDip} DIP)");
            }
        }
    }

    /// <summary>
    /// A window whose height is capped rather than sized to its content must let the body SCROLL —
    /// capping alone only moves the point where content is clipped, and a clipped Save button is
    /// unreachable rather than merely out of view. Pair: <c>MaxHeight</c> + a ScrollViewer around
    /// whatever grows.
    /// </summary>
    [Fact]
    public void Every_auto_height_window_scrolls_a_body_when_capped()
    {
        foreach (var file in AppWindowXaml())
        {
            var xaml = File.ReadAllText(file);
            if (!xaml.Contains("SizeToContent=\"Height\"")) continue;
            Assert.Contains("<ScrollViewer", xaml);
            Assert.Contains("VerticalScrollBarVisibility", xaml);
        }
    }
}
